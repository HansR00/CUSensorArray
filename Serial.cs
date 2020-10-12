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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace zeroWsensors
{
  public class PMS1003data
  {
    // We only use the atmospheric calibrated values, not the standardised
    public double Pm1_atm { get; set; }
    public double Pm25_atm { get; set; }
    public double Pm10_atm { get; set; }
  }

  class Serial
  {
    public volatile PMS1003data MinuteValues = new PMS1003data();         // We communicate the average over the minute

    public List<double> PM25_last_1_hourList = new List<double>(); // The list to create the minutevalues
    public List<double> PM25_last_3_hourList = new List<double>(); // The list to create the minutevalues
    public List<double> PM25_last_24_hourList = new List<double>(); // The list to create the minutevalues
    public List<double> PM25_nowcast = new List<double>(); // The list to create the minutevalues
    public List<double> PM10_last_1_hourList = new List<double>(); // The list to create the minutevalues
    public List<double> PM10_last_3_hourList = new List<double>(); // The list to create the minutevalues
    public List<double> PM10_last_24_hourList = new List<double>(); // The list to create the minutevalues
    public List<double> PM10_nowcast = new List<double>(); // The list to create the minutevalues

    private List<PMS1003data> ObservationList = new List<PMS1003data>(); // The list to create the minutevalues

    #region Init

    private readonly Support Sup;
    private SerialPort thisSerial;
    private const string SerialPortUsed = "/dev/ttyS0";

    private readonly byte[] buffer = new byte[32];
    private int NrOfObservations;

    public Serial(Support s)
    {
      Sup = s;

      Sup.LogDebugMessage($"Serial: Constructor...");

      PMS1003Start();

      return;
    }

    #endregion

    #region PMS1003

    public void PMS1003Start()
    {
      // Init the serial port!
      //
      // Use next for debugging purposes
      // private readonly string[] PortNames;
      // PortNames = SerialPort.GetPortNames();
      // foreach (string name in PortNames) Console.WriteLine($"Possible Portnames: {name}");

      thisSerial = new SerialPort(SerialPortUsed, 9600, Parity.None, 8, StopBits.One)
      {
        ReadTimeout = 500
      };

      try
      {
        thisSerial.Open();
        Sup.LogDebugMessage($"Serial: Opened the port {SerialPortUsed}, {9600}, {Parity.None}, 8, {StopBits.One}");
      }
      catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArgumentException || e is IOException || e is InvalidOperationException)
      {
        Sup.LogDebugMessage($"Serial: Exception on Open : {e.Message}");
        Sup.LogDebugMessage("No use continuing when the particle sensor is not there - exit");
        //
        // No use continuing when the particle sensor is not there so exit
        //
        Environment.Exit(0);
      }

      return;
    }

    public void PMS1003Stop()
    {
      try
      {
        thisSerial.Close();
        Sup.LogDebugMessage($"Serial: Closed the port");
      }
      catch (IOException e)
      {
        Sup.LogDebugMessage($"Serial: Exception on Close : {e.Message}");
        // Continue
      }

      return;
    }

    public void DoPMS1003()
    {
      PMS1003data thisReading = new PMS1003data();

      // https://www.instructables.com/id/Read-and-write-from-serial-port-with-Raspberry-Pi/
      // See also: https://www.google.com/search?client=firefox-b-d&q=name+of+serial+port+on+rpi+zero+w

      Sup.LogTraceMessage($"DoPMS1003: Start...");

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
            Sup.LogTraceMessage($"Trying {Count} chars: {buffer[0]:x2}/{buffer[1]:x2}");
          } while (Count < 32 || (buffer[0] != 0x42 || buffer[1] != 0x4d));

          // Below is for debugging, takes performance
          // string _hex = Sup.ByteArrayToHexString(buffer);
          Sup.LogTraceMessage($"Nr of Bytes {Count}: {Sup.ByteArrayToHexString(buffer)}");

          thisReading.Pm1_atm = buffer[10] * 255 + buffer[11];
          thisReading.Pm25_atm = buffer[12] * 255 + buffer[13];
          thisReading.Pm10_atm = buffer[14] * 255 + buffer[15];

          ObservationList.Add(thisReading);
        }
      }
      catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArgumentException || e is TimeoutException || e is InvalidOperationException || e is ArgumentNullException || e is IOException)
      {
        Sup.LogDebugMessage($"DoPMS1003: Exception on Serial Read => {e.Message}");
        // Continue reading 
      }

      FakeAirLink();

      return;
    } // DoPMS1003

    #endregion

    #region FakeAirLink
    private void FakeAirLink()
    {
      // Actually if a read fails, this is not a true observation, it actually functions as 
      // the counter to keep track of the number of 10 second cycles we passed
      NrOfObservations++;
      Sup.LogTraceMessage($"DoPMS1003: FakeAirLink, start of function => NrOfObservations = {NrOfObservations}");

      // Handle the reads into the minute values and calculate all values for the AirLink simulation
      if (NrOfObservations == 6)
      {
        Sup.LogTraceMessage($"Serial: Creating minutevalues as average of the 10 second observations...");
        // So we came here 6 times every 10 seconds. Create the minute values and remove the existing list, create a new one
        // The average values are always real averages even if some fetches failed in which case the list is shorter 
        // Might remove the Valid attribute in the structure
        MinuteValues.Pm1_atm = ObservationList.Select(x => x.Pm1_atm).Average();
        MinuteValues.Pm25_atm = ObservationList.Select(x => x.Pm25_atm).Average();
        MinuteValues.Pm10_atm = ObservationList.Select(x => x.Pm10_atm).Average();

        ObservationList = new List<PMS1003data>();  // The old list disappears by the garbage collector.

        NrOfObservations = 0;

        Sup.LogTraceMessage($"Serial: Adding minutevalues to the averageslists...");

        if (PM25_last_1_hourList.Count == 60) PM25_last_1_hourList.RemoveAt(PM25_last_1_hourList.Count - 1);
        PM25_last_1_hourList.Add(MinuteValues.Pm25_atm);
        Sup.LogTraceMessage($"Serial: PM25_last_1_hourList - count: {PM25_last_1_hourList.Count} / Average: {PM25_last_1_hourList.Average():F1}");

        if (PM25_last_3_hourList.Count == 3 * 60) PM25_last_3_hourList.RemoveAt(PM25_last_3_hourList.Count - 1);
        PM25_last_3_hourList.Add(MinuteValues.Pm25_atm);
        Sup.LogTraceMessage($"Serial: PM25_last_3_hourList - count: {PM25_last_3_hourList.Count} / Average {PM25_last_3_hourList.Average():F1}");

        if (PM25_last_24_hourList.Count == 24 * 60) PM25_last_24_hourList.RemoveAt(PM25_last_24_hourList.Count - 1);
        PM25_last_24_hourList.Add(MinuteValues.Pm25_atm);
        Sup.LogTraceMessage($"Serial: PM25_last_24_hourList - count: {PM25_last_24_hourList.Count} / Average {PM25_last_24_hourList.Average():F1}");

        if (PM10_last_1_hourList.Count == 60) PM10_last_1_hourList.RemoveAt(PM10_last_1_hourList.Count - 1);
        PM10_last_1_hourList.Add(MinuteValues.Pm10_atm);
        Sup.LogTraceMessage($"Serial: PM10_last_1_hourList - count: {PM10_last_1_hourList.Count} / Average {PM10_last_1_hourList.Average():F1}");

        if (PM10_last_3_hourList.Count == 3 * 60) PM10_last_3_hourList.RemoveAt(PM10_last_3_hourList.Count - 1);
        PM10_last_3_hourList.Add(MinuteValues.Pm10_atm);
        Sup.LogTraceMessage($"Serial: PM10_last_3_hourList - count: {PM10_last_3_hourList.Count} / Average {PM10_last_3_hourList.Average():F1}");

        if (PM10_last_24_hourList.Count == 24 * 60) PM10_last_24_hourList.RemoveAt(PM10_last_24_hourList.Count - 1);
        PM10_last_24_hourList.Add(MinuteValues.Pm10_atm);
        Sup.LogTraceMessage($"Serial: PM10_last_24_hourList - count: {PM10_last_24_hourList.Count} / Average {PM10_last_24_hourList.Average():F1}");
      }

      Sup.LogTraceMessage($"DoPMS1003: End of function => NrOfObservations = {NrOfObservations}");

      return;
    }
    #endregion

  } // Class Serial
} // Namespace
