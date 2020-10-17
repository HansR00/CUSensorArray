/*
 * CUSensorArray / Support.cs
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace zeroWsensors
{
  public class Support
  {
    private readonly IniFile CUSensorIni;

    #region Init
    public Support()
    {
      // Do the logging setup
      if (!Directory.Exists("log")) Directory.CreateDirectory("log");

      string[] files = Directory.GetFiles("log");
      
      if (files.Length >= 10)
      {
        foreach (string file in files)
        {
          FileInfo fi = new FileInfo(file);
          if (DateTime.Now.Subtract(fi.LastWriteTime).TotalDays > 30 ) fi.Delete();
        }
      }

      // So the ini start
      CUSensorIni = new IniFile(this, "CUSensorArray.ini");
    }

    #endregion

    #region Diagnostics

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
    public void LogDebugMessage(string message) => Debug.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
    public void LogTraceErrorMessage(string message) => Trace.WriteLineIf(Program.CUSensorsSwitch.TraceError, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Error " + message);
    public void LogTraceWarningMessage(string message) => Trace.WriteLineIf(Program.CUSensorsSwitch.TraceWarning, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Warning " + message);
    public void LogTraceInfoMessage(string message) => Trace.WriteLineIf(Program.CUSensorsSwitch.TraceInfo, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Information " + message);
    public void LogTraceVerboseMessage(string message) => Trace.WriteLineIf(Program.CUSensorsSwitch.TraceVerbose, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Verbose " + message);

    #endregion

    #region Ini

    public string GetSensorsIniValue(string section, string key, string def) => CUSensorIni.GetValue(section, key, def);
    public void SetSensorsIniValue(string section, string key, string def) => CUSensorIni.SetValue(section, key, def);

    #endregion

    #region VersionCopyright
    public string Version()
    {
      string tmp;

      tmp = typeof(Support).Assembly.GetName().Version.Major + "." + typeof(Support).Assembly.GetName().Version.Minor + "." + typeof(Support).Assembly.GetName().Version.Build;

      return string.Format(CultureInfo.InvariantCulture, $"CUSensorArray - Version {tmp} - Started at {DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");
    }

    public string Copyright => "© Hans Rottier";

    #endregion

    #region SpecificConversionByteHex
    public string ByteArrayToHexString(byte[] ba)
    {
      // https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa

      StringBuilder hex = new StringBuilder(ba.Length * 2);
      foreach (byte b in ba)
        hex.AppendFormat("{0:x2}", b);
      return hex.ToString();
    }

    public string BoolArrayToBitString(bool[] b)
    {
      // https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa

      StringBuilder bitBuilder = new StringBuilder(b.Length * 2);
      bitBuilder.Append("Bitstring DHT22 : ");

      for (int i = 0; i < 40; i++)
      {
        if (b[i]) bitBuilder.Append("1");
        else bitBuilder.Append("0");
        if ((i + 1) % 8 == 0) bitBuilder.Append(" ");
      }

      return bitBuilder.ToString();
    }
    #endregion

  } // Class
} // Namespace
