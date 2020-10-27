/*
 * CUSensorArray / i2c.cs
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

using RaspberrySharp.IO.InterIntegratedCircuit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace zeroWsensors
{
  // Reference: 1) https://blog.mrgibbs.io/using-i2c-on-the-raspberry-pi-with-c/
  //            2) https://jeremylindsayni.wordpress.com/2017/05/08/using-net-core-2-to-read-from-an-i2c-device-connected-to-a-raspberry-pi-3-with-ubuntu-16-04/

  // So far only the SHT31 is supported
  public enum I2cSensorsSupported : int { Dummy, SHT31 }

  #region I2cSensorData
  public class I2cSensordata
  {
    public double TemperatureF { get; set; }    // in Fahrenheit
    public double TemperatureC { get; set; }    // in Celsius
    public double Humidity { get; set; }
  }

  #endregion

  #region I2C
  public class I2C
  {
    private readonly Support Sup;

    private readonly I2cSensorDevice Sensor;
    private readonly I2cSensorsSupported SensorUsed;

    internal bool IsAirLinkSensor;
    internal volatile I2cSensordata MinuteValues = new I2cSensordata();               // We communicate the average over the minute
    static public List<I2cSensordata> ObservationList = new List<I2cSensordata>();    // The list to create the minutevalues


    public I2C(Support s, string Name)
    {
      Sup = s;

      Sup.LogDebugMessage($"I2C Constructor {Name}");

      // Make all addresses
      i2cAddress[(int)I2cSensorsSupported.SHT31] = 0x44;

      // Now create the actual device
      try
      {
        SensorUsed = (I2cSensorsSupported)Enum.Parse(typeof(I2cSensorsSupported), Name, true);
        switch (SensorUsed)
        {
          case I2cSensorsSupported.SHT31:
            Sup.LogDebugMessage("I2C Constructor: Creating SHT31 sensor");
            Sensor = new SHT31Device(Sup, Name);
            break;
          default:
            Sup.LogDebugMessage($"I2C Constructor: I2c sensor not implemented {SensorUsed}");
            Sensor = new I2cDummyDevice(Sup, Name);
            break;
        }
        Sensor.Start();
      }
      catch (Exception e) when (e is ArgumentException || e is ArgumentNullException)
      {
        // We arrive here if the sensor does not exist in the Enum definition
        Sup.LogTraceErrorMessage($"I2C Constructor: Exception on parsing I2cDevice Name : {e.Message}");
        Sup.LogTraceErrorMessage("Either an error in naming or driver not implemented.");
        Sup.LogTraceErrorMessage("Replacing this device by a Dummy Driver. Continuing...");
        Sensor = new I2cDummyDevice(Sup, Name);
      }
    }// Constructor

    public void Stop()
    {
      Sensor.Stop();
    }

    public void Start()
    {
      Sensor.Start();
    }

    public void DoWork()
    {
      Sensor.DoWork();
    }

    public void SetMinuteValuesFromObservations()
    {
      if (Sensor.Valid)
      {
        Sup.LogTraceInfoMessage($"SetMinuteValuesFromObservations: Creating minutevalues as average of the 10 second observations... {SensorUsed}");
        MinuteValues.Humidity = ObservationList.Select(x => x.Humidity).Average();
        MinuteValues.TemperatureC = ObservationList.Select(x => x.TemperatureC).Average();
        MinuteValues.TemperatureF = ObservationList.Select(x => x.TemperatureF).Average();

        // Renew the observationlist 
        ObservationList = new List<I2cSensordata>();  // The old list disappears through the garbage collector.
      }

      return;
    }

    #region Methods DetectSensors

    // NOTE: This belongs to the I2C class logically but it is called on program level because it only needed once at initialisation
    // List of Sensor addresses/presence used in this interface
    static public int[] i2cAddress = new int[Enum.GetNames(typeof(I2cSensorsSupported)).Length];
    static public bool[] i2cAddressDetected = new bool[Enum.GetNames(typeof(I2cSensorsSupported)).Length];

    static private readonly int _rows = 8;
    static private readonly int _cols = 16;
    static private readonly byte[,] _nDevices = new byte[_rows, _cols];
    static private I2cDetect _I2CDetect;

    static public void DetectSensors(Support Sup)
    {
      Sup.LogDebugMessage("I2C Detect");
      Sup.LogDebugMessage("===============");

      _I2CDetect = new I2cDetect();
      var list = _I2CDetect.Detect();

      foreach (var i2c in list)
      {
        int r = i2c / 16;
        int c = i2c % 16;
        _nDevices[r, c] = 1;

        // Check if the devices we want are present
        for (int i = 0; i < i2cAddress.Length; i++)
        {
          if (i2c == i2cAddress[i])
          {
            i2cAddressDetected[i] = true;
            Sup.LogDebugMessage($"DetectSensors: found {Enum.GetName(typeof(I2cSensorsSupported), i)} at {i2c:x2}  ");
          }
        }
      }

      Sup.LogDebugMessage("     0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f");


      for (int i = 0; i < _rows; i++)
      {
        PrintRow(i, Sup);
      }
    }

    static private void PrintRow(int rowId, Support Sup)
    {
      string row = string.Format("{0:00}: ", rowId);
      for (int i = 0; i < _cols; i++)
      {
        if ((rowId == 0 && i < 3) || (rowId == 7 && i > 7))
          row += "   ";
        else
        if (_nDevices[rowId, i] == 0)
          row += string.Format("-- ");
        else
          row += string.Format("{0}{1:X} ", rowId, i);
      }

      Sup.LogDebugMessage(row);
    }

    #endregion
  }
  #endregion

  #region I2cSensorDevice (baseclass)
  // The base class for all devices
  internal abstract class I2cSensorDevice
  {
    internal I2cDeviceConnection thisConnect;
    internal I2cSensorsSupported SensorUsed;
    internal bool Valid;

    public abstract void Start();
    public abstract void Stop();
    public abstract void DoWork();

    public I2cSensorDevice()
    {
    }// Constructor
  }
  #endregion

  #region Dummy
  internal class I2cDummyDevice : I2cSensorDevice
  {
    readonly Support Sup;

    public I2cDummyDevice(Support s, string Name)
    {
      Sup = s;
      Sup.LogDebugMessage($"Dummy Constructor...{Name}");
      SensorUsed = I2cSensorsSupported.Dummy;
      Valid = false;
    }

    public override void Start()
    {
    }

    public override void Stop()
    {
    }

    public override void DoWork()
    {
    }
  } // End DummyDevice
  #endregion

  #region SHT31Device
  internal class SHT31Device : I2cSensorDevice
  {
    readonly Support Sup;

    // List of commands for SHT31
    readonly byte[] SHT31SoftResetCommand = { 0x30, 0xA2 }; // Soft Reset.
    readonly byte[] SHT31ReadStatusCommand = { 0xF3, 0x2D }; // Soft Reset.
                                                             // readonly byte[] SHT31BreakCommand = { 0x30, 0x93 }; // Soft Reset.

    readonly byte[] SHT31SingleShotCommand = { 0x2C, 0x0D }; // Single shot fetch; Clock stretching disabled, medium repeatability.

    //--------------------------------------------------------------------------------------------------------------------------------

    // General response  buffer, to enlarge of there are ICs which require that.
    byte[] Response = { 0, 0, 0, 0, 0, 0, 0, 0 };

    public SHT31Device(Support s, string Name)
    {
      Sup = s;
      SensorUsed = I2cSensorsSupported.SHT31;
      Valid = true;
      Sup.LogTraceInfoMessage($"I2cSensorDevice SHT31 Constructor...{Name}, Valid = {Valid}");
      Sup.LogTraceInfoMessage($"Print AddressDetected for SensorUsed: {I2C.i2cAddressDetected[(int)SensorUsed]} for {SensorUsed}");
    }

    public override void Start()
    {
      Sup.LogDebugMessage($"I2cSensorDevice {SensorUsed} Start on address {I2C.i2cAddress[(int)SensorUsed]:x2}");
      thisConnect = Program.thisDriver.Connect(I2C.i2cAddress[(int)SensorUsed]);
    }

    public override void Stop()
    {
      Sup.LogDebugMessage($"I2cSensorDevice {SensorUsed} stop");

      // Get and display the status before exiting
      lock (Response)
      {
        thisConnect.Write(SHT31ReadStatusCommand);
        Response = thisConnect.Read(3);
        thisConnect.Write(SHT31SoftResetCommand);
      }

      Sup.LogDebugMessage($"StatusWord {SensorUsed}: {Convert.ToString((ushort)(Response[0] * 256 + Response[1]), toBase: 2)} ");
    }

    public override void DoWork()
    {
      I2cSensordata thisReading = new I2cSensordata();

      Sup.LogTraceInfoMessage($"I2cSensorDevice {SensorUsed}: DoWork routine entry");

      try
      {
        lock (Response)
        {
          thisConnect.Write(SHT31SingleShotCommand);
          Response = thisConnect.Read(6);
        }

        // For debugging, normally not on
        Sup.LogTraceInfoMessage($"I2cSensorDevice {SensorUsed} data read: {Response[0]}; {Response[1]}; {Response[2]}; {Response[3]}; {Response[4]}; {Response[5]}; "); // {Response[6]}; {Response[7]};

        thisReading.Humidity = 100 * (double)(Response[3] * 0x100 + Response[4]) / 65535;             // BitConverter.ToUInt16(Response, 3) / 65535;
        thisReading.TemperatureF = -49 + 315 * (double)(Response[0] * 0x100 + Response[1]) / 65535;   // Must be in Fahrenheit for the Davis simulation
        thisReading.TemperatureC = -45 + 175 * (double)(Response[0] * 0x100 + Response[1]) / 65535;   // This is the same in Celsius

        I2C.ObservationList.Add(thisReading);
      }
      catch (Exception e)
      {
        Sup.LogTraceWarningMessage($"I2cSensorDevice Exception {SensorUsed}:...{e.Message}");
      }

      return;
    }
  } // End SHT31Device
  #endregion
}