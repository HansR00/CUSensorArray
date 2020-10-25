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
  public enum SerialSensorsSupported : int { PMS1003 }

  #region PMSensorData
  public class PMSensordata
  {
    // We only use the atmospheric calibrated values, not the standardised
    public double Pm1_atm { get; set; }
    public double Pm25_atm { get; set; }
    public double Pm10_atm { get; set; }
  }

  #endregion

  #region Serial (to be rewritten analog to I2C)
  public class Serial
  {
    private readonly Support Sup;
    private readonly SerialSensorDevice Sensor;
    private List<PMSensordata> ObservationList = new List<PMSensordata>();   // The list to create the minutevalues

    // The nest three variables belong actually to one sensor so they should be a sensor property (and will be at some time!)
    internal volatile PMSensordata MinuteValues = new PMSensordata();         // We communicate the average over the minute
    internal bool IsAirLinkSensor;

    public Serial(Support s, string Name)
    {
      Sup = s;
      Sup.LogDebugMessage($"Serial: Constructor...{Name}");

      Sensor = new SerialSensorDevice(Sup, Name);
      if (Sensor.Valid) Sensor.Open();

      return;
    }

    public void Stop()
    {
      if (Sensor.Valid) Sensor.Close();
    }

    public void DoSerial()
    {
      if (Sensor.Valid)
      {
        switch (Sensor.SensorUsed)
        { // So far only the PMS1003 is supported
          case SerialSensorsSupported.PMS1003:
            DoPMS1003();
            break;
          default:
            break;
        }
      }

      return;
    }

    public void SetMinuteValuesFromObservations()
    {
      if (Sensor.Valid)
      {
        Sup.LogTraceInfoMessage($"SetMinuteValuesFromObservations: Creating minutevalues as average of the 10 second observations... {Sensor.SensorUsed}");
        lock (MinuteValues)
        {
          MinuteValues.Pm1_atm = ObservationList.Select(x => x.Pm1_atm).Average();
          MinuteValues.Pm25_atm = ObservationList.Select(x => x.Pm25_atm).Average();
          MinuteValues.Pm10_atm = ObservationList.Select(x => x.Pm10_atm).Average();
        }

        // Renew the observationlist 
        ObservationList = new List<PMSensordata>();  // The old list disappears through the garbage collector.
      }

      return;
    }

    private void DoPMS1003()
    {
      if (!Sensor.Valid) return;

      // https://www.instructables.com/id/Read-and-write-from-serial-port-with-Raspberry-Pi/
      // See also: https://www.google.com/search?client=firefox-b-d&q=name+of+serial+port+on+rpi+zero+w

      PMSensordata thisReading = new PMSensordata();
      byte[] buffer = new byte[32];

      Sup.LogTraceInfoMessage($"DoPMS1003: Start...");

      // Read the input buffer, throw away all other data in the buffer and read again
      // Do this as long as there is no 32 characters in the buffer (the minimum)
      // As it fills appr every second the 5 second read I implement should be enough
      try
      {
        int Count;

        if (Sensor.thisSerial.BytesToRead > 0)
        {
          do
          {
            lock (buffer)
            {
              Count = Sensor.thisSerial.Read(buffer, 0, 32);
              Sensor.thisSerial.DiscardInBuffer();
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

          ObservationList.Add(thisReading);
        }
      }
      catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArgumentException || e is TimeoutException || e is InvalidOperationException || e is ArgumentNullException || e is IOException)
      {
        Sup.LogTraceWarningMessage($"DoPMS1003: Exception on Serial Read => {e.Message}");
        // Continue reading 
      }

      return;
    } // DoPMS1003

    internal class SerialSensorDevice
    {
      readonly Support Sup;
      readonly DefinitionSerialPort Port;

      public SerialSensorsSupported SensorUsed;       // The enum version of the Name!
      public SerialPort thisSerial;               // The actual port definition: devicename, baud, stopbits etc...
      public bool Valid;                          // Is the device defined by the user actually connected??

      internal SerialSensorDevice(Support s, string Name)
      {
        Sup = s;

        Sup.LogDebugMessage($"Serial SensorDevice Constructor...{Name}");

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
      } // Constructor

      public void Open()
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
          Sup.LogTraceErrorMessage("No use continuing when the particle sensor is not there");
          Valid = false;
        }
      }// End Open

      public void Close()
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
      } // End Close
    } // End SensorDevice

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
  } // Class Serial

  #endregion
} // Namespace
