/*
 * CUSensorArray / Serial.cs
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
 * License:     GNU GENERAL PUBLIC LICENSE, Version 3, 29 June 2007
 * 
 *  // Use next for possible debugging purposes
 *  // private readonly string[] PortNames;
 *  // PortNames = SerialPort.GetPortNames();
 *  // foreach (string name in PortNames) Console.WriteLine($"Possible Portnames: {name}");
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace zeroWsensors
{
  public enum SerialSensorsSupported : int { Dummy, PMS1003 }

  #region PMSensorData
  public class PMSensordata
  {
    // We only use the atmospheric calibrated values, not the standardised
    public double Pm1_atm { get; set; }
    public double Pm25_atm { get; set; }
    public double Pm10_atm { get; set; }
  }

  #endregion

  #region Serial
  public class Serial
  {
    readonly Support Sup;

    private readonly SerialSensorDevice Sensor;
    private readonly SerialSensorsSupported SensorUsed;

    internal bool IsAirLinkSensor;
    internal volatile PMSensordata MinuteValues = new PMSensordata();             // We communicate the average over the minute
    public List<PMSensordata> ObservationList = new List<PMSensordata>();         // The list to create the minutevalues

    public Serial(Support s, string Name)
    {
      Sup = s;

      Sup.LogDebugMessage($"Serial: Constructor...{Name}");
      try
      {
        SensorUsed = (SerialSensorsSupported)Enum.Parse(typeof(SerialSensorsSupported), Name, true);

        switch (SensorUsed)
        {
          case SerialSensorsSupported.PMS1003:
            Sup.LogDebugMessage("Serial Constructor: Creating PMS1003 sensor");
            Sensor = new PMS1003Device(Sup, Name);
            break;
          default:
            Sup.LogDebugMessage($"Serial Constructor: Serial sensor not implemented {SensorUsed}");
            Sensor = new SerialDummyDevice(Sup, Name);
            break;
        }

        Sensor.Open(); // Also called Open in some circles
      }
      catch (Exception e) when (e is ArgumentException || e is ArgumentNullException)
      {
        // We arrive here if the sensor does not exist in the Enum definition
        Sup.LogTraceErrorMessage($"Serial Constructor: Exception on parsing Serial Device Name : {e.Message}");
        Sup.LogTraceErrorMessage("Either an error in naming or driver not implemented.");
        Sup.LogTraceErrorMessage("Replacing this device by a Dummy Driver. Continuing...");
        Sensor = new SerialDummyDevice(Sup, Name);
      }
    }// Serial Constructor

    public void Stop()
    {
      Sensor.Close();
    }

    public void Start()
    {
      Sensor.Open();
    }

    public void DoWork()
    {
      Sensor.DoWork(this);
    }

    public void SetMinuteValuesFromObservations()
    {
      if (Sensor.Valid)
      {
        lock (MinuteValues)
        {
          Sup.LogTraceInfoMessage($"SetMinuteValuesFromObservations: Creating minutevalues as average of the 10 second observations... {SensorUsed}");

          if (Program.CUSensorsSwitch.TraceInfo)
          {
            int i = 0;
            foreach (PMSensordata entry in ObservationList) Sup.LogTraceInfoMessage($"Observationlist data {i++}: {entry.Pm1_atm:F1}; {entry.Pm25_atm:F1}; {entry.Pm10_atm:F1};");
          }

          MinuteValues.Pm1_atm = ObservationList.Select(x => x.Pm1_atm).Average();
          MinuteValues.Pm25_atm = ObservationList.Select(x => x.Pm25_atm).Average();
          MinuteValues.Pm10_atm = ObservationList.Select(x => x.Pm10_atm).Average();

          Sup.LogTraceInfoMessage($"Minutevalues data: {MinuteValues.Pm1_atm:F1}; {MinuteValues.Pm25_atm:F1}; {MinuteValues.Pm10_atm:F1};");
        }

        // Renew the observationlist 
        ObservationList.Clear();
        // ObservationList = new List<PMSensordata>();  // The old list disappears through the garbage collector.
      }

      return;
    }
  } // end Class Serial

  #endregion

  #region SerialSensorDevice (baseclass)

  // The base class for all devices
  internal abstract class SerialSensorDevice
  {
    internal Support Sup;

    internal SerialSensorsSupported SensorUsed;    // The enum version of the Name!
    internal DefinitionSerialPort Port;            // The corresponding Port definition
    internal SerialPort thisSerial;               // The actual port definition: devicename, baud, stopbits etc...
    internal bool Valid;                          // Is the device defined by the user actually connected??

    internal byte[] buffer = new byte[32];

    public abstract void Open();
    public abstract void Close();
    public abstract void DoWork(Serial thisSensor);

    public SerialSensorDevice()
    {
    }// Constructor

    // Create a nested class for the Port definitions

    internal class DefinitionSerialPort
    {
      readonly Support Sup;

      internal string SerialPortUsed { get; set; }
      internal int SerialBaudRate { get; set; }
      internal Parity SerialParity { get; set; }
      internal int SerialDataBits { get; set; }
      internal StopBits SerialNrOfStopBits { get; set; }

      internal DefinitionSerialPort(Support s, string Name)
      {
        Sup = s;

        Sup.LogDebugMessage($"DefinitionSerialPort Constructor...");

        try
        {
          SerialPortUsed = Sup.GetSensorsIniValue("PortDefinitions", $"{Name}_SerialPort", "/dev/ttyS0");
          SerialBaudRate = Convert.ToInt32(Sup.GetSensorsIniValue("PortDefinitions", $"{Name}_SerialBaudrate", "9600"));
          SerialParity = (Parity)Enum.Parse(typeof(Parity), Sup.GetSensorsIniValue("PortDefinitions", $"{Name}_SerialParity", "None"), true);
          SerialDataBits = Convert.ToInt32(Sup.GetSensorsIniValue("PortDefinitions", $"{Name}_SerialDataBits", "8"));
          SerialNrOfStopBits = (StopBits)Enum.Parse(typeof(StopBits), Sup.GetSensorsIniValue("PortDefinitions", $"{Name}_SerialStopBits", "One"), true);
        }
        catch (Exception e) when (e is ArgumentException || e is ArgumentNullException || e is OverflowException || e is FormatException)
        {
          Sup.LogTraceErrorMessage($"Serial: Exception on Serial Port definitions in Inifile : {e.Message}");
        }
      }
    } // class DefinitionSerialPort

  }

  #endregion

  #region Dummy
  internal class SerialDummyDevice : SerialSensorDevice
  {
    //Support Sup;

    public SerialDummyDevice(Support s, string Name)
    {
      Sup = s;
      Sup.LogDebugMessage($"Dummy Constructor...{Name}");
      SensorUsed = SerialSensorsSupported.Dummy;
      Valid = false;
    }

    public override void Open()
    {
    }

    public override void Close()
    {
    }

    public override void DoWork(Serial thisSensor)
    {
    }
  } // End DummyDevice
  #endregion

  #region PMS1003
  internal class PMS1003Device : SerialSensorDevice
  {
    public PMS1003Device(Support s, string Name)
    {
      Sup = s;
      Sup.LogTraceInfoMessage($"DoPMS1003Device: Constructor...{Name}");

      try
      {
        SensorUsed = (SerialSensorsSupported)Enum.Parse(typeof(SerialSensorsSupported), Name, true);
        Port = new DefinitionSerialPort(Sup, Name);
        Valid = true;
      }
      catch (Exception e) when (e is ArgumentException || e is ArgumentNullException)
      {
        // We arrive here if the sensor does not exist in the Enum definition
        Sup.LogTraceErrorMessage($"Serial: Exception on Serial Port definitions in Inifile : {e.Message}");
        Sup.LogTraceErrorMessage("No use continuing when the particle sensor is not there - trying anyway");
        Valid = false;
      }
    }// PMS1003 Constructor

    public override void Open()
    {
      thisSerial = new SerialPort(Port.SerialPortUsed, Port.SerialBaudRate, Port.SerialParity, Port.SerialDataBits, Port.SerialNrOfStopBits)
      {
        ReadTimeout = 500
      };

      try
      {
        thisSerial.Open();
        Sup.LogDebugMessage($"Serial: Opened the port {Port.SerialPortUsed}, {Port.SerialBaudRate}, {Port.SerialParity}, {Port.SerialDataBits}, {Port.SerialNrOfStopBits}");
      }
      catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArgumentException || e is IOException || e is InvalidOperationException)
      {
        Sup.LogTraceErrorMessage($"Serial: Exception on Open {SensorUsed}: {e.Message}");
        Sup.LogTraceErrorMessage("No use continuing when the particle sensor is not there, trying anyway...");
        Valid = false;
      }
    }

    public override void Close()
    {
      try
      {
        thisSerial.Close();
        Sup.LogDebugMessage($"Serial: Closed the port on {SensorUsed}");
      }
      catch (IOException e)
      {
        Sup.LogTraceErrorMessage($"Serial: Exception on Close {SensorUsed}: {e.Message}");
      }

      Valid = false;
    }

    public override void DoWork(Serial thisSensor)
    {
      PMSensordata thisReading = new PMSensordata();

      // Read the input buffer, throw away all other data in the buffer and read again
      // Do this as long as there is no 32 characters in the buffer (the minimum)
      // As it fills appr every second the 5 second read I implement should be enough
      try
      {
        int Count;

        if (thisSerial.BytesToRead > 0)
        {
          do
          {
            lock (buffer)
            {
              Count = thisSerial.Read(buffer, 0, 32);
              thisSerial.DiscardInBuffer();
            }

            // Below is for debugging, takes performance
            Sup.LogTraceInfoMessage($"Trying {Count} chars: {buffer[0]:x2}/{buffer[1]:x2}");
          } while (Count < 32 || (buffer[0] != 0x42 || buffer[1] != 0x4d));

          // Below is for debugging, takes performance
          // string _hex = Sup.ByteArrayToHexString(buffer);
          Sup.LogTraceInfoMessage($"Nr of Bytes {Count}: {Sup.ByteArrayToHexString(buffer)}");

          thisReading.Pm1_atm = buffer[10] * 255 + buffer[11];
          thisReading.Pm25_atm = buffer[12] * 255 + buffer[13];
          thisReading.Pm10_atm = buffer[14] * 255 + buffer[15];

          Sup.LogTraceInfoMessage($"Serial data read: {thisReading.Pm1_atm:F1}; {thisReading.Pm25_atm:F1}; {thisReading.Pm10_atm:F1};");

          thisSensor.ObservationList.Add(thisReading);
        }
      }
      catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArgumentException || e is TimeoutException || e is InvalidOperationException || e is ArgumentNullException || e is IOException)
      {
        Sup.LogTraceWarningMessage($"DoPMS1003: Exception on Serial Read => {e.Message}");
        // Continue reading 
      }
    }
  } // Class PMS1003
  #endregion
} // Namespace
