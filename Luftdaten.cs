using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

namespace CuSensorArray
{
  internal class Luftdaten
  {
    public string SensorID;
    readonly Support Sup;
    readonly HttpClient LuftdatenHttpClient;

    internal Luftdaten(Support s)
    {
      string line;

      Sup = s;
      Sup.LogDebugMessage($"Luftdaten ctor: Start");

      // LuftdatenHttpClient = new HttpClient();

      using (StreamReader cpuFile = new StreamReader("/proc/cpuinfo"))
      {
        line = cpuFile.ReadLine();
        Sup.LogDebugMessage($"Luftdaten ctor reading cpuinfo: {line}");

        do
        {
          if (line.Substring(0, 6) == "Serial")
          {
            Sup.LogDebugMessage($"Luftdaten ctor: Serial line found");
            string[] splitstring;
            splitstring = line.Split(':');
            SensorID = splitstring[1];

            Sup.LogDebugMessage($"Luftdaten ctor: SensorID = {SensorID}");
            break;
          }

          line = cpuFile.ReadLine();
          Sup.LogDebugMessage($"Luftdaten ctor reading cpuinfo: {line}");

        } while (true); // end while
      } // end using => disposes the cpuFile
    } // end constructor

    ~Luftdaten()
    {
      // LuftdatenHttpClient.Dispose();
    }

    internal void Send()
    {
      Sup.LogDebugMessage($"Luftdaten Send: Start");
      // Setup the data 

      // And post to Luftdaten
      //LuftdatenHttpClient.
    }
  }
}
