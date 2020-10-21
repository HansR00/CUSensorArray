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
 */

using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;

namespace zeroWsensors
{
  public enum PMSensorsSupported : int { PMS1003 }

  public class PMSensordata
  {
    // We only use the atmospheric calibrated values, not the standardised
    public double Pm1_atm { get; set; }
    public double Pm25_atm { get; set; }
    public double Pm10_atm { get; set; }
  }

  // Use next for debugging purposes
  // private readonly string[] PortNames;
  // PortNames = SerialPort.GetPortNames();
  // foreach (string name in PortNames) Console.WriteLine($"Possible Portnames: {name}");
  public class Serial
  {
    private readonly Support Sup;

    internal readonly SensorDevice Sensor;

    public Serial(Support s)
    {
      Sup = s;

      Sup.LogDebugMessage($"Serial: Constructor...");

      Sensor = new SensorDevice(Sup);
      Sensor.Open();

      return;
    }

    public void SerialStop()
    {
      Sup.LogDebugMessage($"Serial: Stop...");

      if (Sensor.Valid) Sensor.Close();

      return;
    }

    public void DoSerial()
    {
      if (Sensor.Valid)
      {
        switch(Sensor.SensorUsed)
        { // So far only the PMS1003 is supported
          case PMSensorsSupported.PMS1003:
            DoPMS1003(Sensor);
            break;
          default:
            break;
        }
      }
    }

    private void DoPMS1003(SensorDevice dev)
    {
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

        if (dev.thisSerial.BytesToRead > 0)
        {
          do
          {
            lock (buffer)
            {
              Count = dev.thisSerial.Read(buffer, 0, 32);
              dev.thisSerial.DiscardInBuffer();
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

          dev.ObservationList.Add(thisReading);
        }
      }
      catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArgumentException || e is TimeoutException || e is InvalidOperationException || e is ArgumentNullException || e is IOException)
      {
        Sup.LogTraceWarningMessage($"DoPMS1003: Exception on Serial Read => {e.Message}");
        // Continue reading 
      }

      return;
    } // DoPMS1003
  } // Class Serial

  internal class SensorDevice
  {
    readonly Support Sup;
    readonly DefinitionSerialPort Port;

    public PMSensorsSupported SensorUsed;
    public SerialPort thisSerial;
    public bool Valid;

    // The nest three variables belong actually to one sensor so they should be a sensor property (and will be at some time!)
    public volatile PMSensordata MinuteValues = new PMSensordata();         // We communicate the average over the minute
    public List<PMSensordata> ObservationList = new List<PMSensordata>();   // The list to create the minutevalues

    internal SensorDevice(Support s)
    {
      Sup = s;

      Sup.LogDebugMessage($"SensorDevice Constructor...");

      try
      {
        SensorUsed = (PMSensorsSupported)Enum.Parse(typeof(PMSensorsSupported), Sup.GetSensorsIniValue("Serial", $"SerialSensor", "PMS1003"), true);
        Port = new DefinitionSerialPort(Sup);
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
        Sup.LogDebugMessage($"Serial: Closed the port");
      }
      catch (IOException e)
      {
        Sup.LogTraceErrorMessage($"Serial: Exception on Close {SensorUsed}: {e.Message}");
        Valid = false;
      }
    } // End Close
  } // End SensorDevice

  internal class DefinitionSerialPort
  {
    readonly Support Sup;

    public string SerialPortUsed { get; set; }
    public int SerialBaudRate { get; set; }
    public Parity SerialParity { get; set; }
    public int SerialDataBits { get; set; }
    public StopBits SerialNrOfStopBits { get; set; }

    internal DefinitionSerialPort(Support s)
    {
      Sup = s;

      Sup.LogDebugMessage($"DefinitionSerialPort Constructor...");

      try
      {
        SerialPortUsed = Sup.GetSensorsIniValue("Serial", $"SerialPort", "/dev/ttyS0");
        SerialBaudRate = Convert.ToInt32(Sup.GetSensorsIniValue("Serial", $"SerialBaudrate", "9600"));
        SerialParity = (Parity)Enum.Parse(typeof(Parity), Sup.GetSensorsIniValue("Serial", $"SerialParity", "None"), true);
        SerialDataBits = Convert.ToInt32(Sup.GetSensorsIniValue("Serial", $"SerialDataBits", "8"));
        SerialNrOfStopBits = (StopBits)Enum.Parse(typeof(StopBits), Sup.GetSensorsIniValue("Serial", $"SerialStopBits", "One"), true);
      }
      catch (Exception e) when (e is ArgumentException || e is ArgumentNullException || e is OverflowException || e is FormatException)
      {
        Sup.LogTraceErrorMessage($"Serial: Exception on Serial Port definitions in Inifile : {e.Message}");
      }
    }
  } // class DefinitionSerialPort
} // Namespace
