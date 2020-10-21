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
// Cumulus forum threads on the subject
//      https://cumulus.hosiene.co.uk/viewtopic.php?f=40&t=17457
//      https://cumulus.hosiene.co.uk/viewtopic.php?f=6&t=18417
//      https://cumulus.hosiene.co.uk/viewtopic.php?f=44&t=18541
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace zeroWsensors
{
  // Clock definitions
  public class Program
  {
    public static TraceSwitch CUSensorsSwitch { get; set; }
    public static bool AirLinkEmulation { get; set; }

    Support Sup;
    I2C thisI2C;
    Serial thisSerial;
    EmulateAirLink thisEmulator;
    WebServer thisWebserver;

    bool Continue = true;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
    private static void Main() // string[] args
    {
      Program p = new Program();
      p.RealMain();
    }

    void RealMain()
    {
      #region Init

      // Setup logging and Ini
      Sup = new Support();

      Console.CancelKeyPress += new ConsoleCancelEventHandler(CtrlCHandler);
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

      Trace.Listeners.Add(new TextWriterTraceListener($"log/{DateTime.Now.ToString("yyMMddHHmm", CultureInfo.InvariantCulture)}sensors.log"));
      Trace.AutoFlush = true;
      CUSensorsSwitch = new TraceSwitch("CUSensorsSwitch", "Tracing switch for CUSensors")
      {
        Level = TraceLevel.Verbose
      };

      Sup.LogDebugMessage($"Initial {CUSensorsSwitch} => Error: {CUSensorsSwitch.TraceError}, Warning: {CUSensorsSwitch.TraceWarning}, Info: {CUSensorsSwitch.TraceInfo}, Verbose: {CUSensorsSwitch.TraceVerbose}, ");

      AirLinkEmulation = Sup.GetSensorsIniValue("General", "AirLinkEmulation", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
      string thisTrace = Sup.GetSensorsIniValue("General", "TraceInfo", "Warning");   // Verbose, Information, Warning, Error, Off

      // Now set the Trace level to the wanted value
      switch( thisTrace.ToLower() )
      {
        case "error":
          CUSensorsSwitch.Level = TraceLevel.Error;
          break;

        case "warning":
          CUSensorsSwitch.Level = TraceLevel.Warning;
          break;

        case "info":
          CUSensorsSwitch.Level = TraceLevel.Info;
          break;

        case "verbose":
          CUSensorsSwitch.Level = TraceLevel.Verbose;
          break;

        default:
          CUSensorsSwitch.Level = TraceLevel.Off;
          break;
      }

      Sup.LogDebugMessage($"According to Inifile {CUSensorsSwitch} => Error: {CUSensorsSwitch.TraceError}, Warning: {CUSensorsSwitch.TraceWarning}, Info: {CUSensorsSwitch.TraceInfo}, Verbose: {CUSensorsSwitch.TraceVerbose}, ");

      #endregion

      Sup.LogDebugMessage(message: "ZeroWsensors : ----------------------------");
      Sup.LogDebugMessage(message: "ZeroWsensors : Entering Main");

      Console.WriteLine($"{Sup.Version()} {Sup.Copyright}");
      Sup.LogDebugMessage($"{Sup.Version()} {Sup.Copyright}");

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

      if (AirLinkEmulation)
      {
        // Only do the emulator and Webserver if it is asked for (default = false)!!
        //
        thisEmulator = new EmulateAirLink(Sup, thisSerial);
        thisWebserver = new WebServer(Sup, thisI2C, thisSerial, thisEmulator);
        thisWebserver.Start();
      }

      // Start the loop
      using (StreamWriter of = new StreamWriter("CUSensorArray.txt", true))
      {
        int Clock = 0;
        string thisLine = "";

        do
        {
          // Do this condional because the ctrl-c interrupt can be given aanywhere.
          Sup.LogTraceInfoMessage(message: "ZeroWsensors : Getting sensor values from the Main 10 second loop");

          if (Continue)
          {
            thisSerial.DoSerial();
            thisI2C.DoI2C();

            Clock++;

            // So we came here 6 times every 10 seconds. Create the minute values and remove the existing list, create a new one
            // The average values are always real averages even if some fetches failed in which case the list is shorter 
            // This is the basic work of the sensor handler: fetch data and write to local logfile

            if (Clock == 6)
            {
              Clock = 0;

              Sup.LogTraceInfoMessage($"Serial: Creating minutevalues as average of the 10 second observations...");
              thisSerial.Sensor.MinuteValues.Pm1_atm = thisSerial.Sensor.ObservationList.Select(x => x.Pm1_atm).Average();
              thisSerial.Sensor.MinuteValues.Pm25_atm = thisSerial.Sensor.ObservationList.Select(x => x.Pm25_atm).Average();
              thisSerial.Sensor.MinuteValues.Pm10_atm = thisSerial.Sensor.ObservationList.Select(x => x.Pm10_atm).Average();

              thisSerial.Sensor.ObservationList = new List<PMSensordata>();  // The old list disappears through the garbage collector.

              thisLine = $"{thisI2C.SHT31current.TemperatureC:F1};{thisI2C.SHT31current.Humidity:F0};" +
                $"{thisSerial.Sensor.MinuteValues.Pm1_atm:F1};{thisSerial.Sensor.MinuteValues.Pm25_atm:F1};{thisSerial.Sensor.MinuteValues.Pm10_atm:F1};";
              of.WriteLine($"{DateTime.Now:dd-MM-yyyy HH:mm};{thisLine}");
              of.Flush();

              // Now we do the AirLink handling which is assumed to be called once per minute with the observation list to create 
              // all other necessary lists and calculated values from there

              if (AirLinkEmulation) thisEmulator.DoAirLink();
            }
          }

          // This is really hardcoded and should NOT change. The whole thing sis based on 6 measurements per minute, so loop every 10 seconds
          Thread.Sleep(10000);
        } while (Continue); // Do-While
      } // Using the datafile

      #endregion

      Sup.LogDebugMessage("SensorArray Gracefull exit... End");

      Trace.Flush();
      Trace.Close();

      return;
    } // Real main()

    #region CtrlC-Handler
    // Now set the handler and do the processing
    //
    void CtrlCHandler(object sender, ConsoleCancelEventArgs args)
    {
      Sup.LogDebugMessage("SensorArray Gracefull exit... Begin");

      ConsoleSpecialKey Key = args.SpecialKey;
      //Sup.LogDebugMessage($"Key Pressed: {Key}");
      //Console.WriteLine($"Key Pressed: {Key}");

      switch (Key)
      {
        // Maybe some time I do a Signal base dynamic errorlevel setting
        // but ctrl-break does  not work on my machine
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
          thisSerial.SerialStop();
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
