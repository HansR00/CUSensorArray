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
using System.Text;

namespace zeroWsensors
{
  class Support
  {
    #region Diagnostics

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
    public void LogDebugMessage(string message) => Debug.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);
    public void LogTraceMessage(string message) => Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message);

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

      for(int i=0; i<40; i++)
      {
        if (b[i]) bitBuilder.Append("1");
        else bitBuilder.Append("0");
        if ((i + 1) % 8 == 0) bitBuilder.Append(" ");
      }

      return bitBuilder.ToString();
    }
#endregion

#region VersionCopyright
    public string Version()
    {
      string _ver = "";

      if (string.IsNullOrEmpty(_ver))
      {
        _ver = typeof(Support).Assembly.GetName().Version.Major.ToString(CultureInfo.InvariantCulture) + "." +
                     typeof(Support).Assembly.GetName().Version.Minor.ToString(CultureInfo.InvariantCulture) + "." +
                     typeof(Support).Assembly.GetName().Version.Build.ToString(CultureInfo.InvariantCulture);

        _ver = string.Format(CultureInfo.InvariantCulture, $"CUSensorArray - Version {_ver} " +
                             $"- Started at {DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)})");

        this.LogTraceMessage($"Support: {_ver}");
      }

      return _ver;
    }

    public string Copyright() => "© Hans Rottier";
    //{
    //  string _copyRight = 
    //  return _copyRight;
    //}

#endregion

  }
}
