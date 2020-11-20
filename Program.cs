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
 *              EmulateAirLink.cs
 *              i2c.cs
 *              Inifile.cs
 *              OneWire.cs (nothing there)
 *              Serial.cs
 *              Support.cs
 *              
 * Participation: Participation is sought with the RIVM, luftdaten, etc...
 * 1) https://sensor.community/nl/ (via RIVM)
 * 2) https://www.samenmetenaanluchtkwaliteit.nl/
 * 3) https://www.samenmetenaanluchtkwaliteit.nl/dataportaal
 * 4) https://luftdaten.info/
 * 
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
// Serial Ports:
//    https://www.instructables.com/id/Read-and-write-from-serial-port-with-Raspberry-Pi/
//    See also: https://www.google.com/search?client=firefox-b-d&q=name+of+serial+port+on+rpi+zero+w
//
// I2C:
// Reference: 1) https://blog.mrgibbs.io/using-i2c-on-the-raspberry-pi-with-c/
//            2) https://jeremylindsayni.wordpress.com/2017/05/08/using-net-core-2-to-read-from-an-i2c-device-connected-to-a-raspberry-pi-3-with-ubuntu-16-04/
//
// Githubs:
//   https://github.com/gusmanb/BCM2835Managed  (Native C# code of the library)
//   https://github.com/frankhommers/LibBcm2835.Net  (wrapper)
//   https://www.raspberrypi.org/forums/viewtopic.php?t=41975  (Instruction to RaspberryPiDotNet)
//   https://stackoverflow.com/questions/23639895/include-bcm2853-lib-on-raspberry-pi (SPI on the RPi)
//   
// Specifics :
// DHT22 : http://www.uugear.com/portfolio/read-dht1122-temperature-humidity-sensor-from-raspberry-pi/
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
// Luftdaten / Sensor.Community site
//      https://github.com/opendata-stuttgart/meta/wiki/Eintrag-in-unsere-Datenbank
//      https://luftdaten.info/kontakt/
//      https://devices.sensor.community/
//

using RaspberrySharp.IO.GeneralPurpose;
using RaspberrySharp.IO.InterIntegratedCircuit;
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
    const int MaxNrSerialSensors = 2;
    const int MaxNrI2cSensors = 8;

    public static TraceSwitch CUSensorsSwitch { get; private set; }
    public static bool AirLinkEmulation { get; private set; }
    public static I2cDriver ThisDriver { get; private set; }

    bool Continue = true;

    readonly I2C[] thisI2C = new I2C[MaxNrI2cSensors];              // Max 8 I2C sensors, maybe make this configurable later
    readonly Serial[] thisSerial = new Serial[MaxNrSerialSensors];  // Max two serial sensors
    EmulateAirLink thisEmulator;
    WebServer thisWebserver;

    Support Sup;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
    private static void Main() // string[] args
    {
      Program p = new Program();
      p.RealMain();
    }

    void RealMain()
    {
      Init();

      Sup.LogDebugMessage(message: "ZeroWsensors : ----------------------------");
      Sup.LogDebugMessage(message: "ZeroWsensors : Entering Main");

      Console.WriteLine($"{Sup.Version()} {Sup.Copyright}");
      Sup.LogDebugMessage($"{Sup.Version()} {Sup.Copyright}");

      Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY;\n" +
        "This is free software, and you are welcome to redistribute it under certain conditions.");
      Sup.LogDebugMessage("This program comes with ABSOLUTELY NO WARRANTY;\n" +
        "This is free software, and you are welcome to redistribute it under certain conditions.");


     #region MainLoop

      // Start the loop
      using (StreamWriter of = new StreamWriter("CUSensorArray.txt", true))
      {
        int Clock = 0;

        do
        {
          string thisLine = "";

          // Do this condional because the ctrl-c interrupt can be given aanywhere.
          Sup.LogTraceInfoMessage(message: "ZeroWsensors : Getting sensor values from the Main 10 second loop");

          if (Continue)
          {
            Clock++;

            for (int i = 0; i < MaxNrSerialSensors; i++) thisSerial[i].DoWork();    // Takes care of the reading of the serial devices
            for (int i = 0; i < MaxNrI2cSensors; i++) thisI2C[i].DoWork();          // Takes care of the  reading of the I2C devices

            // So we came here 6 times every 10 seconds. Create the minute values and remove the existing list, create a new one
            // The average values are always real averages even if some fetches failed in which case the list is shorter 
            // This is the basic work of the sensor handler: fetch data and write to local logfile

            if (Clock == 6)
            {
              Clock = 0;

              for (int i = 0; i < MaxNrSerialSensors; i++) thisSerial[i].SetMinuteValuesFromObservations();
              for (int i = 0; i < MaxNrI2cSensors; i++) thisI2C[i].SetMinuteValuesFromObservations();
              if (AirLinkEmulation) thisEmulator.DoAirLink();

              // Write out to the logfile
              for (int i = 0; i < MaxNrSerialSensors; i++) thisLine += $";{thisSerial[i].MinuteValues.Pm1_atm:F1};{thisSerial[i].MinuteValues.Pm25_atm:F1};{thisSerial[i].MinuteValues.Pm10_atm:F1}";
              for (int i = 0; i < MaxNrI2cSensors; i++) thisLine += $";{thisI2C[i].MinuteValues.TemperatureC:F1};{thisI2C[i].MinuteValues.Humidity:F0}";

              Sup.LogTraceInfoMessage(message: "ZeroWsensors : Writing out the data to the logfile");
              Sup.LogTraceInfoMessage(message: $"{DateTime.Now:dd-MM-yyyy HH:mm}{thisLine}");
              of.WriteLine($"{DateTime.Now:dd-MM-yyyy HH:mm}{thisLine}");
              of.Flush();

              // Now we do the AirLink handling which is assumed to be called once per minute with the observation list to create 
              // all other necessary lists and calculated values from there
            }
          }

          // This is really hardcoded and should NOT change. The whole thing is based on 6 measurements per minute, so loop every 10 seconds
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
          for (int i = 0; i < MaxNrI2cSensors; i++) thisI2C[i].Stop();
          for (int i = 0; i < MaxNrSerialSensors; i++) thisSerial[i].Stop();
          if (AirLinkEmulation) thisWebserver.Stop();
          args.Cancel = true;   // Do not immedialtely stop the process, handle it by the Continue loop control boolean.
          Continue = false;
          break;

        default:
          // Should be impossible
          break;
      }

      return;
    }

    #endregion

    #region Init
    void Init()
    {
      bool NoSerialDevice = false;
      bool NoI2cDevice = false;

      // Setup logging and Ini
      Sup = new Support();

      Sup.LogDebugMessage(message: "Init() : Start");

      string AirLinkPMDevice = "";
      string AirLinkTHDevice = "";

      Console.CancelKeyPress += new ConsoleCancelEventHandler(CtrlCHandler);
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

      // Setup tracing
      Trace.Listeners.Add(new TextWriterTraceListener($"log/{DateTime.Now.ToString("yyMMddHHmm", CultureInfo.InvariantCulture)}sensors.log"));
      Trace.AutoFlush = true;
      CUSensorsSwitch = new TraceSwitch("CUSensorsSwitch", "Tracing switch for CUSensors")
      {
        Level = TraceLevel.Verbose
      };

      Sup.LogDebugMessage($"Initial {CUSensorsSwitch} => Error: {CUSensorsSwitch.TraceError}, Warning: {CUSensorsSwitch.TraceWarning}, Info: {CUSensorsSwitch.TraceInfo}, Verbose: {CUSensorsSwitch.TraceVerbose}, ");

      string thisTrace = Sup.GetSensorsIniValue("General", "TraceInfo", "Warning");   // Verbose, Information, Warning, Error, Off

      // Now set the Trace level to the wanted value
      switch (thisTrace.ToLower())
      {
        case "error": CUSensorsSwitch.Level = TraceLevel.Error; break;
        case "warning": CUSensorsSwitch.Level = TraceLevel.Warning; break;
        case "info": CUSensorsSwitch.Level = TraceLevel.Info; break;
        case "verbose": CUSensorsSwitch.Level = TraceLevel.Verbose; break;
        default: CUSensorsSwitch.Level = TraceLevel.Off; break;
      }

      Sup.LogDebugMessage($"According to Inifile {CUSensorsSwitch} => Error: {CUSensorsSwitch.TraceError}, Warning: {CUSensorsSwitch.TraceWarning}, Info: {CUSensorsSwitch.TraceInfo}, Verbose: {CUSensorsSwitch.TraceVerbose}, ");

      // Determine which sensor has to be for the AirLink:
      AirLinkEmulation = Sup.GetSensorsIniValue("General", "AirLinkEmulation", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
      AirLinkPMDevice = Sup.GetSensorsIniValue("AirLinkDevices", $"PMdevice", "");
      AirLinkTHDevice = Sup.GetSensorsIniValue("AirLinkDevices", $"THdevice", "");

      // Check if we want AirLinkEmulation and do accordingly
      //
      if (AirLinkEmulation)
      {
        Sup.LogDebugMessage(message: "Init : Creating the AirLink Emulator and the Webserver");
        thisEmulator = new EmulateAirLink(Sup);
        thisWebserver = new WebServer(Sup, thisEmulator);
      }

      // Do the Serial devices
      Sup.LogDebugMessage(message: "Init : Creating the Serial devices");
      for (int i = 0; i < MaxNrSerialSensors; i++)
      {
        string DeviceName = Sup.GetSensorsIniValue("SerialDevices", $"Serial{i}", "");
        thisSerial[i] = new Serial(Sup, DeviceName);
        if (AirLinkEmulation && $"Serial{i}" == AirLinkPMDevice)
        {
          Sup.LogDebugMessage(message: $"Init : Serial{i} is AirLink device");
          thisSerial[i].IsAirLinkSensor = true;
          thisEmulator.SetSerialDevice(thisSerial[i]);
        }

        if (i == 0 && string.IsNullOrEmpty(DeviceName))
        {
          NoSerialDevice = true;
          break;
        }
      }// Serial devices

      // Do the i2c devices
      // Define the driver on this level so we only have one driver in the system on which we open all connections
      Sup.LogTraceInfoMessage(message: "Init : Creating the I2C driver");
      ThisDriver = new I2cDriver(ProcessorPin.Gpio02, ProcessorPin.Gpio03, false);
      Sup.LogTraceInfoMessage(message: $"Init : created the I2cDriver {ThisDriver}");
      I2C.DetectSensors(Sup);

      for (int i = 0; i < MaxNrI2cSensors; i++)
      {
        string DeviceName = Sup.GetSensorsIniValue("I2CDevices", $"I2C{i}", "");
        thisI2C[i] = new I2C(Sup, DeviceName);
        if (AirLinkEmulation && $"I2C{i}" == AirLinkTHDevice)
        {
          Sup.LogDebugMessage(message: $"Init : I2C{i} is AirLink device");
          thisI2C[i].IsAirLinkSensor = true;
          thisEmulator.SetI2cDevice(thisI2C[i]);
        }

        if (i == 0 && string.IsNullOrEmpty(DeviceName))
        {
          NoI2cDevice = true;
          break;
        }
      }// i2c devices

      if (AirLinkEmulation && (NoI2cDevice || NoSerialDevice))
      {
        Sup.LogDebugMessage("Init: At leat one serial and one I2c device is required for PM and T/H in AirLink emulation");
        Environment.Exit(0);
      }

      if (AirLinkEmulation)
      {
        Sup.LogDebugMessage(message: $"Init : Starting the webserver");
        thisWebserver.Start();
      }
    } // Init

    #endregion

  } // Class Program
} // Namespace zeroWsensors
