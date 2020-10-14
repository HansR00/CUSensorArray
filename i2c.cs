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

using System;
using RaspberrySharp.IO.InterIntegratedCircuit;
using RaspberrySharp.IO.GeneralPurpose;

namespace zeroWsensors
{
  // Reference: 1) https://blog.mrgibbs.io/using-i2c-on-the-raspberry-pi-with-c/
  //            2) https://jeremylindsayni.wordpress.com/2017/05/08/using-net-core-2-to-read-from-an-i2c-device-connected-to-a-raspberry-pi-3-with-ubuntu-16-04/

  public class SHT31data
  {
    public double TemperatureF { get; set; }    // in Fahrenheit
    public double TemperatureC { get; set; }    // in Celsius
    public double Humidity { get; set; }
  }

  class I2C
  {
    public volatile SHT31data SHT31current = new SHT31data();


    #region MainI2C

    readonly Support Sup;

    // List of Sensor addresses/presence used in this interface
    private const int SHT31 = 0x44; bool SHT31present;

    // General response  buffer, to enlarge of there are ICs which require that.
    byte[] Response = { 0, 0, 0, 0, 0, 0, 0, 0 };
    readonly I2cDriver ThisDriver;
    readonly I2cDeviceConnection SHT31Connect;

    // This needs to be made generic for more than one I2C sensor with their own returnvalue structures...
    // Currently it is workable for me only (for the SHT31, address 0x44).
    public I2C(Support s)
    {
      Sup = s;

      Sup.LogDebugMessage("I2C Constructor");

      DetectSensors();

      if (SHT31present)
      {
        ThisDriver = new I2cDriver(ProcessorPin.Gpio02, ProcessorPin.Gpio03, false);
        SHT31Connect = ThisDriver.Connect(SHT31); // SHT31
      }
    }

    ~I2C()
    {
      Sup.LogDebugMessage("I2C Destructor");
      //Console.WriteLine("Doing the Destructor...");
    }

    public void StopI2C()
    {
      Sup.LogDebugMessage("I2C Stop");

      if (SHT31present)
      {
        StopSHT31();
        ThisDriver.Dispose();
      }
    }

    // Fix this for general behaviour with more I2C sensors -> create a list of devices with their results?
    public void DoI2C()
    {
      Sup.LogTraceInfoMessage("DoI2C public routine");
      if (SHT31present)
      {
        DoSHT31();
      }

      return;
    }

    #endregion MainI2C

    #region SHT31

    // List of commands for SHT31
    readonly byte[] SHT31SoftResetCommand = { 0x30, 0xA2 }; // Soft Reset.
    readonly byte[] SHT31ReadStatusCommand = { 0xF3, 0x2D }; // Soft Reset.
    // readonly byte[] SHT31BreakCommand = { 0x30, 0x93 }; // Soft Reset.
    readonly byte[] SHT31SingleShotCommand = { 0x2C, 0x0D }; // Single shot fetch; Clock stretching disabled, medium repeatability.

    private void DoSHT31()
    {
      SHT31data thisReading = new SHT31data();

      Sup.LogTraceInfoMessage("DoSHT31 main routine entry");

      try
      {
        lock (Response)
        {
          SHT31Connect.Write(SHT31SingleShotCommand);
          Response = SHT31Connect.Read(6);
        }

        // For debugging, normally not on
        Sup.LogTraceInfoMessage($"SHT31 read data...{Response[0]}; {Response[1]}; {Response[2]}; {Response[3]}; {Response[4]}; {Response[5]}; "); // {Response[6]}; {Response[7]};

        thisReading.Humidity = 100 * (double)(Response[3] * 0x100 + Response[4]) / 65535;             // BitConverter.ToUInt16(Response, 3) / 65535;
        thisReading.TemperatureF = -49 + 315 * (double)(Response[0] * 0x100 + Response[1]) / 65535;   // Must be in Fahrenheit for the Davis simulation
        thisReading.TemperatureC = -45 + 175 * (double)(Response[0] * 0x100 + Response[1]) / 65535;   // This is the same in Celsius

        SHT31current = thisReading;
      }
      catch (InvalidOperationException e)
      {
        Sup.LogTraceWarningMessage($"SHT31 Exception:...{e.Message}");
      }
      catch (Exception e)
      {
        Sup.LogTraceWarningMessage($"SHT31 Exception:...{e.Message}");
      }

      return;
    }

    private void StopSHT31()
    {
      Sup.LogTraceInfoMessage("SHT31 Stop");

      // Get and display the status before exiting
      lock (Response)
      {
        SHT31Connect.Write(SHT31ReadStatusCommand);
        Response = SHT31Connect.Read(3);
        SHT31Connect.Write(SHT31SoftResetCommand);
      }

      //Console.WriteLine("15 14 13 12 11 10 09 08 07 06 05 04 03 02 01 00");
      //Sup.LogDebugMessage("15 14 13 12 11 10 09 08 07 06 05 04 03 02 01 00");

      //Console.WriteLine($"StatusWord : {Convert.ToString((ushort)(Response[0] * 256 + Response[1]), toBase: 2)} ");
      Sup.LogTraceInfoMessage($"StatusWord : {Convert.ToString((ushort)(Response[0] * 256 + Response[1]), toBase: 2)} ");
    }


    #endregion SHT31

    #region DetectSensors
    static readonly int _rows = 8;
    static readonly int _cols = 16;
    static readonly byte[,] _nDevices = new byte[_rows, _cols];
    static I2cDetect _I2CDetect;

    public void DetectSensors()
    {
      Sup.LogDebugMessage("I2C Detect");
      Sup.LogDebugMessage("===============");

      _I2CDetect = new I2cDetect();
      var list = _I2CDetect.Detect();
      //List<byte> list = new List<byte>();
      //list.Add(15);
      //list.Add(33);
      //list.Add(3);
      foreach (var i2c in list)
      {
        int r = i2c / 16;
        int c = i2c % 16;
        _nDevices[r, c] = 1;

        // Check if the devices we want are present
        if (i2c == SHT31)
        {
          Sup.LogDebugMessage($"DetectSensors: found {i2c:x2} (SHT31)");
          SHT31present = true;
        }
      }

      Sup.LogDebugMessage("     0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f");


      for (int i = 0; i < _rows; i++)
      {
        PrintRow(i);
      }
    }

    private void PrintRow(int rowId)
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
}