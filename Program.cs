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
//      https://weatherlink.github.io/airlink-local-api/
//  Documentation on the calculations is found here:
//  USA:
//      https://www.airnow.gov/faqs/how-nowcast-algorithm-used-report/
//      https://www.airnow.gov/sites/default/files/2020-05/aqi-technical-assistance-document-sept2018.pdf
//      https://www.epa.gov/air-sensor-toolbox
//      https://en.wikipedia.org/wiki/NowCast_(air_quality_index)
//

//

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace zeroWsensors
{
  // Clock definitions
  public class Program
  {
    public static TraceSwitch CUSensorsSwitch;

    Support Sup;
    I2C thisI2C;
    Serial thisSerial;
    WebServer thisWebserver;

    bool Continue = true;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
    static void Main() // string[] args
    {

      Program p = new Program();
      p.RealMain();
    }

    void RealMain()
    {
      #region Init

      // Do the logging setup
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
      CUSensorsSwitch = new TraceSwitch("CUSensorsSwitch", "Tracing switch for CUSensors");
      Trace.Listeners.Add(new TextWriterTraceListener("sensors.log"));
      Trace.AutoFlush = true;
      Sup = new Support();

      Console.CancelKeyPress += new ConsoleCancelEventHandler(CtrlCHandler);

      #endregion

      Sup.LogDebugMessage(message: "ZeroWsensors : ----------------------------");
      Sup.LogDebugMessage(message: "ZeroWsensors : Entering Main");

      Console.WriteLine($"{Sup.Version()} {Sup.Copyright()}");
      Sup.LogDebugMessage($"{Sup.Version()} {Sup.Copyright()}");

      Console.WriteLine("CUSensorArray - Copyright (C) 2020  Hans Rottier\n" +
        "This program comes with ABSOLUTELY NO WARRANTY;\n" +
        "This is free software, and you are welcome to redistribute it under certain conditions.");
      Sup.LogDebugMessage("CUSensorArray - Copyright (C) 2020  Hans Rottier\n" +
        "This program comes with ABSOLUTELY NO WARRANTY;\n" +
        "This is free software, and you are welcome to redistribute it under certain conditions.");


      #region MainLoop

      // So, here we go...
      thisI2C = new I2C(Sup);
      thisSerial = new Serial(Sup);
      thisWebserver = new WebServer(Sup, thisI2C, thisSerial);
      thisWebserver.Start(WebServer.urlCumulus);


      using (StreamWriter of = new StreamWriter("CUSensorArray.txt", true))
      {
        int Clock = 0;
        string thisLine = "";

        CUSensorsSwitch.Level = TraceLevel.Error;

        do
        {
          // Do this condional because the ctrl-c interrupt can be given aanywhere.
          Sup.LogTraceInfoMessage(message: "ZeroWsensors : Getting sensor values from the Main 10 second loop");

          if (Continue) thisSerial.DoPMS1003();
          if (Continue) thisI2C.DoI2C();

          Clock++;

          if (Clock == 6)
          {
            thisLine = $"{thisI2C.SHT31current.TemperatureC:F1};{thisI2C.SHT31current.Humidity:F0};" +
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

      Trace.Flush();
      Trace.Close();

      // Thread.Sleep(1000); // Give some time to wind down all obligations

      return;
    } // Real main()

    #region CtrlC-Handler
    // Now set the handler and do the processing
    //
    void CtrlCHandler(object sender, ConsoleCancelEventArgs args)
    {
      Sup.LogDebugMessage("SensorArray Gracefull exit... Begin");

      ConsoleSpecialKey Key = args.SpecialKey;
      Sup.LogDebugMessage($"Key Pressed: {Key}");
      Console.WriteLine($"Key Pressed: {Key}");

      switch (Key)
      {
        case ConsoleSpecialKey.ControlBreak:
          // Do not immedialtely stop the process.
          args.Cancel = true;

          //switch (CUSensorsSwitch.Level)
          //{
          //  case TraceLevel.Off:
          //    CUSensorsSwitch.Level = TraceLevel.Error;
          //    Console.WriteLine($"Trace level set to {CUSensorsSwitch.Level}");
          //    Sup.LogDebugMessage($"Trace level set to {CUSensorsSwitch.Level}");
          //    break;
          //  case TraceLevel.Error:
          //    CUSensorsSwitch.Level = TraceLevel.Warning;
          //    Console.WriteLine($"Trace level set to {CUSensorsSwitch.Level}");
          //    Sup.LogDebugMessage($"Trace level set to {CUSensorsSwitch.Level}");
          //    break;
          //  case TraceLevel.Warning:
          //    CUSensorsSwitch.Level = TraceLevel.Info;
          //    Console.WriteLine($"Trace level set to {CUSensorsSwitch.Level}");
          //    Sup.LogDebugMessage($"Trace level set to {CUSensorsSwitch.Level}");
          //    break;
          //  case TraceLevel.Info:
          //    CUSensorsSwitch.Level = TraceLevel.Verbose;
          //    Console.WriteLine($"Trace level set to {CUSensorsSwitch.Level}");
          //    Sup.LogDebugMessage($"Trace level set to {CUSensorsSwitch.Level}");
          //    break;
          //  case TraceLevel.Verbose:
          //    CUSensorsSwitch.Level = TraceLevel.Off;
          //    Console.WriteLine($"Trace level set to {CUSensorsSwitch.Level}");
          //    Sup.LogDebugMessage($"Trace level set to {CUSensorsSwitch.Level}");
          //    break;
          //  default:
          //    Console.WriteLine($"Trace level set to {CUSensorsSwitch.Level}");
          //    Sup.LogDebugMessage($"Trace level set to {CUSensorsSwitch.Level}");
          //    break;
          //}
          break;

        case ConsoleSpecialKey.ControlC:
          thisI2C.StopI2C();
          thisSerial.PMS1003Stop();
          thisWebserver.Stop();
          args.Cancel = true;                                     // Do not immedialtely stop the process, handle it by the Continue loop control boolean.
          Continue = false;
          break;

        default:
          // Should be impossible
          break;
      }

      return;
    }

    #endregion

  } // Class Program
} // Namespace zeroWsensors
