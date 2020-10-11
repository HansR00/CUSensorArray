/*
 * CUSensorArray/Main
 *
 * © Copyright 2020 Hans Rottier <hans.rottier@gmail.com>
 *
 * 
 * Author:      Hans Rottier <hans.rottier@gmail.com>
 * Project:     CUSensorArray, part of CumulusUtils project meteo-wagenborgen.nl
 * Dates:       Startdate project : August 2020
 *              Initial release: 11 October 2020
 *              
 * Environment: Raspberry 3B+
 *              Raspbian / Linux 
 *              C# / Visual Studio 
 *              NET Core 3.1 
 *              (https://docs.microsoft.com/en-gb/dotnet/core/install/linux-debian)
 *              (https://docs.microsoft.com/en-gb/dotnet/core/install/how-to-detect-installed-versions?pivots=os-linux)
 *              
 * Dependent:   RaspberrySharp (1.4.0)
 *              System.IO.Ports (4.7.0)
 * 
 * License:     GNU GENERAL PUBLIC LICENSE, Version 3, 29 June 2007
 * 
 * Files:       Program.cs
 *              i2c.cs
 *              OneWire.cs (nothing there)
 *              Serial.cs
 *              Support.cs
 *              WebServer.cs
 */

// Documentation:
// Search : rpi gpio C library
// Search : c# bcm2835 c-library
// 
// HowTo : https://edi.wang/post/2019/9/29/setup-net-core-30-runtime-and-sdk-on-raspberry-pi-4
//
// General : https://www.bigmessowires.com/2018/05/26/raspberry-pi-gpio-programming-in-c/
// General : https://elinux.org/RPi_GPIO_Code_Samples
// General : http://www.airspayce.com/mikem/bcm2835/index.html
// General : https://projects.drogon.net/raspberry-pi/wiringpi/
// General : https://www.raspberrypi.org/forums/viewtopic.php?t=9729
// General : http://www.pieter-jan.com/node/15
//
// Githubs:
//   https://github.com/gusmanb/BCM2835Managed  (Native C# code of the library)
//   https://github.com/frankhommers/LibBcm2835.Net  (wrapper)
//   https://www.raspberrypi.org/forums/viewtopic.php?t=41975  (Instruction to RaspberryPiDotNet)
//   https://stackoverflow.com/questions/23639895/include-bcm2853-lib-on-raspberry-pi (SPI on the RPi)
//   
// Specifics :
// DHT22 : https://www.hackster.io/porrey/go-native-c-with-the-dht22-a8e8eb
// DHT11/21/22: https://broersa.github.io/dotnetcore/2017/12/29/raspberry-pi-thermometer-using-raspbian-dotnetcore-and-dht22-sensor.html
// DHT11 : https://stackoverflow.com/questions/36915677/working-with-raspberry-pi-3-windows-10-iot-core-dht22
// One Wire Microsoft : https://docs.microsoft.com/en-us/samples/microsoft/windows-iotcore-samples/gpio-onewire/
// WiringPi : http://wiringpi.com/
// NetCore and Raspbian to read DHT11 : https://havefuncoding.wordpress.com/2017/07/05/net-core-and-raspberrypi-raspbian-to-read-temperature-from-dht11-sensor/
//
// General sensors: 
//      https://tutorials-raspberrypi.com/configure-and-read-out-the-raspberry-pi-gas-sensor-mq-x/
//      https://tutorials-raspberrypi.com/raspberry-pi-sensors-overview-50-important-components/
//
// Interfacing with Python
//      https://medium.com/better-programming/running-python-script-from-c-and-working-with-the-results-843e68d230e5
//      https://ironpython.net/
//
// General on sensors and precision
//      https://www.hindawi.com/journals/js/2018/5096540/
//
// Davis AirLink things:
//      https://digitalrune.github.io/DigitalRune-Documentation/html/81cd4f27-5ce5-4439-9a6c-121f2942f175.htm //Exponential smoothing (the _nowcast values)
//      https://weatherlink.github.io/airlink-local-api/
//

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace zeroWsensors
{
  // Clock definitions
  class Program
  {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
    static void Main() // string[] args
    {
      #region Init
      // Do the logging setup
      //
      //if (!Directory.Exists("log")) Directory.CreateDirectory("log");

      //string[] files = Directory.GetFiles("log");

      //foreach (string file in files)
      //{
      //  FileInfo fi = new FileInfo(file);
      //  if (fi.CreationTime < DateTime.Now.AddDays(-2)) fi.Delete();
      //}

      // And set the thread culture to invariant
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

      //log/{DateTime.Now.ToString("yyMMddHHmm", CultureInfo.InvariantCulture)}
      Trace.Listeners.Add(new TextWriterTraceListener("sensors.log"));
      Trace.AutoFlush = true;

      // The only time Support is instantiated; Can't be in the different classes
      Support Sup = new Support();
      #endregion

      Sup.LogDebugMessage(message: "ZeroWsensors : ----------------------------");
      Sup.LogDebugMessage(message: "ZeroWsensors : Entering Main");

      Console.WriteLine($"{Sup.Version()} {Sup.Copyright()}");
      Sup.LogDebugMessage($"{Sup.Version()} {Sup.Copyright()}");

      Console.WriteLine("CUSensorArray - Copyright (C) 2020  Hans Rottier\n" +
        "This program comes with ABSOLUTELY NO WARRANTY;\n" +
        "This is free software, and you are welcome to redistribute it under certain conditions.");

      // So, here we go...
      I2C thisI2C = new I2C(Sup);
      Serial thisSerial = new Serial(Sup);
      WebServer thisWebserver = new WebServer(Sup, thisI2C, thisSerial);
      thisWebserver.Start(WebServer.urlCumulus);

      #region CtrlC-Handler
      // Now set the handler and do the processing
      //
      bool Continue = true;

      Console.CancelKeyPress += delegate {
        Sup.LogDebugMessage("SensorArray Gracefull exit... Begin");

        // Don't understand why this does not call the destructor but anyway...
        // It now works without the need to reinitialise the sensor.
        thisI2C.StopI2C();
        thisSerial.PMS1003Stop();
        thisWebserver.Stop();

        Continue = false;
      };

      #endregion

      #region MainLoop

      using (StreamWriter of = new StreamWriter("CUSensorArray.txt", true))
      {
        int Clock = 0;
        string thisLine = "";

        do
        {
          // Do this condional because the ctrl-c interrupt can be given aanywhere.
          Sup.LogDebugMessage(message: "ZeroWsensors : Getting sensor values from the Main 10 second loop");

          if (Continue) thisSerial.DoPMS1003();
          if (Continue) thisI2C.DoI2C();

          Clock++;

          if (Clock == 6)
          {
            thisLine = $"{thisI2C.SHT31current.Temperature:F1};{thisI2C.SHT31current.Humidity:F0};" +
              // $"{thisSerial.MinuteValues.Pm1_stand:F1};{thisSerial.MinuteValues.Pm25_stand:F1};{thisSerial.MinuteValues.Pm10_stand:F1};" +
              $"{thisSerial.MinuteValues.Pm1_atm:F1};{thisSerial.MinuteValues.Pm25_atm:F1};{thisSerial.MinuteValues.Pm10_atm:F1};";

            of.WriteLine($"{DateTime.Now:dd-MM-yyyy HH:mm};{thisLine}");
            of.Flush();

            Clock = 0;
          }

          Thread.Sleep(10000);
        } while (Continue); // while
      } // Using the datafile

      #endregion

      Sup.LogDebugMessage("SensorArray Gracefull exit... End");

      Thread.Sleep(1000); // Give some time to wind down all obligations
      Trace.Flush();
      Trace.Close();

      return;
    } // main()
  } // Class Program
} // Namespace zeroWsensors
