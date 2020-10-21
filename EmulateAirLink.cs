﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace zeroWsensors
{
  public class EmulateAirLink
  {
    private readonly Support Sup;
    private readonly Serial Ser;

    public List<double> PM25_last_1_hourList = new List<double>();  // The list to create the minutevalues
    public List<double> PM25_last_3_hourList = new List<double>();  // The list to create the minutevalues
    public List<double> PM25_last_24_hourList = new List<double>(); // The list to create the minutevalues
    //public List<double> PM25_nowcast = new List<double>();          // The list to create the minutevalues
    public List<double> PM10_last_1_hourList = new List<double>();  // The list to create the minutevalues
    public List<double> PM10_last_3_hourList = new List<double>();  // The list to create the minutevalues
    public List<double> PM10_last_24_hourList = new List<double>(); // The list to create the minutevalues
    //public List<double> PM10_nowcast = new List<double>();          // The list to create the minutevalues
    public double NowCast25, NowCast10;                             // Just need one parameter, after calculation 
                                                                    // it is send by the webserver and can be forgotten.

    public EmulateAirLink(Support s, Serial se)
    {
      Sup = s;
      Ser = se;
    }
    
    public void DoAirLink()
    {
      // For Airquality (AirNow) and nowscast documentation: see Program.cs
      // We get here from the main loop once per minute

      Sup.LogTraceInfoMessage($"DoAirLink: Adding minutevalues to the averageslists...");

      if (PM25_last_1_hourList.Count == 60) PM25_last_1_hourList.RemoveAt(0);
      PM25_last_1_hourList.Add(Ser.Sensor.MinuteValues.Pm25_atm);
      Sup.LogTraceInfoMessage($"DoAirLink: PM25_last_1_hourList - count: {PM25_last_1_hourList.Count} / Average: {PM25_last_1_hourList.Average():F1}");

      if (PM25_last_3_hourList.Count == 3 * 60) PM25_last_3_hourList.RemoveAt(0);
      PM25_last_3_hourList.Add(Ser.Sensor.MinuteValues.Pm25_atm);
      Sup.LogTraceInfoMessage($"DoAirLink: PM25_last_3_hourList - count: {PM25_last_3_hourList.Count} / Average {PM25_last_3_hourList.Average():F1}");

      if (PM25_last_24_hourList.Count == 24 * 60) PM25_last_24_hourList.RemoveAt(0);
      PM25_last_24_hourList.Add(Ser.Sensor.MinuteValues.Pm25_atm);
      Sup.LogTraceInfoMessage($"DoAirLink: PM25_last_24_hourList - count: {PM25_last_24_hourList.Count} / Average {PM25_last_24_hourList.Average():F1}");

      if (PM10_last_1_hourList.Count == 60) PM10_last_1_hourList.RemoveAt(0);
      PM10_last_1_hourList.Add(Ser.Sensor.MinuteValues.Pm10_atm);
      Sup.LogTraceInfoMessage($"DoAirLink: PM10_last_1_hourList - count: {PM10_last_1_hourList.Count} / Average {PM10_last_1_hourList.Average():F1}");

      if (PM10_last_3_hourList.Count == 3 * 60) PM10_last_3_hourList.RemoveAt(0);
      PM10_last_3_hourList.Add(Ser.Sensor.MinuteValues.Pm10_atm);
      Sup.LogTraceInfoMessage($"DoAirLink: PM10_last_3_hourList - count: {PM10_last_3_hourList.Count} / Average {PM10_last_3_hourList.Average():F1}");

      if (PM10_last_24_hourList.Count == 24 * 60) PM10_last_24_hourList.RemoveAt(0);
      PM10_last_24_hourList.Add(Ser.Sensor.MinuteValues.Pm10_atm);
      Sup.LogTraceInfoMessage($"DoAirLink: PM10_last_24_hourList - count: {PM10_last_24_hourList.Count} / Average {PM10_last_24_hourList.Average():F1}");

      NowCast25 = CalculateNowCast(PM25_last_24_hourList);
      NowCast10 = CalculateNowCast(PM10_last_24_hourList);

      return;
    }

    private double CalculateNowCast(List<double> thisList)
    {
      const int NrOfHoursForNowCast = 12;         // Standard 12
      const int NrOfMinutesInHour = 60;           // 

      bool PartialHourValid = false;

      int arraySize;

      double Cmin, Cmax;                          // the min and  max concentrations in μg/m3 for this calculation
      double Omega;                               // The ratio Cmin/Cmax for this calculation
      double[] HourlyAverages = new double[NrOfHoursForNowCast];   // To hold the hourly averages of PM values of the last 12 hrs
      double[] thisArray;                         // To hold thisList as an array

      if (thisList.Count < 2 * NrOfMinutesInHour ) return 0.0;         // Less than 2 hrs in the list, then skip this and return no value

      Sup.LogTraceVerboseMessage($"DoAirLink/CalcNowCast: List contains {thisList.Count} elements present.");

      thisList.Reverse();   // Make the last hour at the start of the array so i=0 is really the most recent observation
      if (thisList.Count < NrOfHoursForNowCast * 60) thisArray = thisList.ToArray();
      else thisArray = thisList.Take(NrOfHoursForNowCast * 60).ToArray();
      thisList.Reverse();   // reverse the list to the original situation so that it functions correctly when adding new observations

      arraySize = thisArray.Length;

      int NrOfFullHours = arraySize / NrOfMinutesInHour;
      int NrOfMinutesLeft = arraySize % NrOfMinutesInHour;    // IF NrOfFullHours == 12 this must always be 0. That should follow from the algorithm
                                                              // so I don't check (if fail it is a bug, no paranoia mode)

      Sup.LogTraceVerboseMessage($"DoAirLink/CalcNowCast: NrOfFullHours {NrOfFullHours} / NrOfMinutesLeft = {NrOfMinutesLeft}");

      for (int i = 0; i < NrOfFullHours; i++) HourlyAverages[i] = thisArray[(i * 60)..(i * 60 + 59)].Average();

      if (NrOfMinutesLeft > 0)
      {
        HourlyAverages[NrOfFullHours] = NrOfMinutesLeft == 1 ? thisArray[(NrOfFullHours * 60)] : thisArray[(NrOfFullHours * 60)..(NrOfFullHours * 60 + NrOfMinutesLeft - 1)].Average();
        PartialHourValid = true;
      }

      Cmax = thisArray[0..(NrOfFullHours * 60 + NrOfMinutesLeft - 1)].Max();
      Cmin = thisArray[0..(NrOfFullHours * 60 + NrOfMinutesLeft - 1)].Min();
      Omega = Cmin / Cmax; Omega = Omega > 0.5 ? Omega : 0.5;

      double a = 0, b = 0;
      int v = PartialHourValid ? NrOfFullHours + 1 : NrOfFullHours;
      double[] mpoi = new double[v];

      // For performance reasons everything which can be done outside the loop must be done outside the loop
      for (int i = 0; i < v; i++) mpoi[i] = Math.Pow(Omega, i);
      for (int i = 0; i < v; i++) { a += mpoi[i] * HourlyAverages[i]; b += mpoi[i]; Sup.LogTraceVerboseMessage($"DoAirLink/CalcNowCast: a = {a:F2} / b = {b:F2} - for v = {v} and i = {i}"); }

      Sup.LogTraceVerboseMessage($"DoAirLink/CalcNowCast: returning a/b = {a / b:F2}");

      return a / b;
    }
  }

  public delegate void delReceiveWebRequest(HttpListenerContext Context);

  public class WebServer
  {
    readonly Support Sup;
    readonly I2C thisI2C;
    readonly Serial thisSerial;
    readonly EmulateAirLink thisAirLink;

    protected HttpListener Listener;
    // protected bool IsStarted = false;

    public event delReceiveWebRequest ReceiveWebRequest;
    public const string baseURL = "http://+/";

    public WebServer(Support s, I2C i, Serial se, EmulateAirLink emu)
    {
      Sup = s;
      thisI2C = i;
      thisSerial = se;
      thisAirLink = emu;
    }

    public void Start() // old  parameter: string UrlBase
    {
      Sup.LogDebugMessage($"Webserver: Start - connecting to {baseURL}");

      if (Listener == null)
      {
        Listener = new HttpListener();
        Listener.Prefixes.Add(baseURL);

        try
        {
          Listener.Start();
          IAsyncResult result = Listener.BeginGetContext(new AsyncCallback(WebRequestCallback), Listener);
        }
        catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException)
        {
          Sup.LogTraceWarningMessage($"Webserver: Start Exception - {e.Message}");
          Stop();
        }
      }

      return;
    }

    public void Stop()
    {
      Sup.LogDebugMessage("Webserver: Stop");

      if (Listener != null)
      {
        try
        {
          Listener.Close();   // Might consider using Stop() to keep the object. Not now anyway
          Listener = null;
        }
        catch (Exception e)
        {
          Sup.LogTraceWarningMessage($"Webserver: Stop Exception - {e.Message}");
          Listener = null;    // Do this anyway.
        }
      }

      return;
    }

    protected void WebRequestCallback(IAsyncResult result)
    {
      Sup.LogTraceInfoMessage("Webserver: WebRequestCallback");

      if (Listener != null)
      {
        HttpListenerContext context = Listener.EndGetContext(result);

        // Immediately set up the next context
        Listener.BeginGetContext(new AsyncCallback(WebRequestCallback), Listener);
        ReceiveWebRequest?.Invoke(context);
        ProcessRequest(context);
      }

      return;
    }

    protected virtual void ProcessRequest(HttpListenerContext Context)
    {
      /*
       * So what needs to be returnd is the JSON structure as defined by Davis: https://weatherlink.github.io/airlink-local-api/
       * 
       * {
       * "data":{
       *   "did":"001D0A100021",
       *   "name":"My AirLink",
       *   "ts":1599150192,
       *   "conditions":[
       *     {
       *       "lsid":123456,
       *       "data_structure_type":6,
       *       "temp":75.8,
       *       "hum":54.3,
       *       "dew_point":58.2,
       *       "wet_bulb":62.7,
       *       "heat_index":76.0,
       *       "pm_1_last":1,
       *       "pm_2p5_last":1,
       *       "pm_10_last":1,
       *       "pm_1":0.96,
       *       "pm_2p5":1.21,
       *       "pm_2p5_last_1_hour":2.30,
       *       "pm_2p5_last_3_hours":2.29,
       *       "pm_2p5_last_24_hours":4.81,
       *       "pm_2p5_nowcast":2.30,
       *       "pm_10":1.21,
       *       "pm_10_last_1_hour":2.84,
       *       "pm_10_last_3_hours":2.80,
       *       "pm_10_last_24_hours":6.03,
       *       "pm_10_nowcast":2.84,
       *       "last_report_time":1599150192,
       *       "pct_pm_data_last_1_hour":100,
       *       "pct_pm_data_last_3_hours":100,
       *       "pct_pm_data_nowcast":100,
       *       "pct_pm_data_last_24_hours":80
       *     }
       *   ]
       *  },
       *  "error":null
       * }
       * 
       */

      Sup.LogTraceInfoMessage("Webserver: ProcessRequest");

      HttpListenerRequest Request = Context.Request;
      HttpListenerResponse Response = Context.Response;
      StringBuilder sb = new StringBuilder();

      if (Program.CUSensorsSwitch.TraceInfo)
      {
        Sup.LogTraceInfoMessage(Request.HttpMethod + " " + Request.RawUrl + " Http/" + Request.ProtocolVersion.ToString());
        if (Request.UrlReferrer != null) Sup.LogTraceInfoMessage("Referer: " + Request.UrlReferrer);
        if (Request.UserAgent != null) Sup.LogTraceInfoMessage("User-Agent: " + Request.UserAgent);

        for (int x = 0; x < Request.Headers.Count; x++)
          Sup.LogTraceInfoMessage("Request header: " + Request.Headers.Keys[x] + ":" + " " + Request.Headers[x]);
      }

      sb.AppendLine("{");
      sb.AppendLine("  \"data\":{");
      sb.AppendLine("    \"did\":\"000000000000\",");
      sb.AppendLine("    \"name\":\"FakeAirLink\",");
      sb.AppendLine($"    \"ts\":{DateTimeOffset.Now.ToUnixTimeSeconds()},");
      sb.AppendLine("    \"conditions\":[ {");
      sb.AppendLine("        \"lsid\":000000,");
      sb.AppendLine("        \"data_structure_type\":6,");
      sb.AppendLine($"        \"temp\":{thisI2C.SHT31current.TemperatureF:F1},");   // Send the temp in the required Fahrenheit
      sb.AppendLine($"        \"hum\":{thisI2C.SHT31current.Humidity:F1},");
      sb.AppendLine("        \"dew_point\":-1,");
      sb.AppendLine("        \"wet_bulb\":-1,");
      sb.AppendLine("        \"heat_index\":-1,");
      sb.AppendLine("        \"pm_1_last\":-1,");
      sb.AppendLine("        \"pm_2p5_last\":-1,");
      sb.AppendLine("        \"pm_10_last\":-1,");
      sb.AppendLine($"        \"pm_1\":{thisSerial.Sensor.MinuteValues.Pm1_atm:F2},");
      sb.AppendLine($"        \"pm_2p5\":{thisSerial.Sensor.MinuteValues.Pm25_atm:F2},");
      sb.AppendLine($"        \"pm_2p5_last_1_hour\":{thisAirLink.PM25_last_1_hourList.Average():F2},");
      sb.AppendLine($"        \"pm_2p5_last_3_hours\":{thisAirLink.PM25_last_3_hourList.Average():F2},");
      sb.AppendLine($"        \"pm_2p5_last_24_hours\":{thisAirLink.PM25_last_24_hourList.Average():F2},");
      sb.AppendLine($"        \"pm_2p5_nowcast\":{thisAirLink.NowCast25:F2},");
      sb.AppendLine($"        \"pm_10\":{thisSerial.Sensor.MinuteValues.Pm10_atm:F2},");
      sb.AppendLine($"        \"pm_10_last_1_hour\":{thisAirLink.PM10_last_1_hourList.Average():F2},");
      sb.AppendLine($"        \"pm_10_last_3_hours\":{thisAirLink.PM10_last_3_hourList.Average():F2},");
      sb.AppendLine($"        \"pm_10_last_24_hours\":{thisAirLink.PM10_last_24_hourList.Average():F2},");
      sb.AppendLine($"        \"pm_10_nowcast\":{thisAirLink.NowCast10:F1},");
      sb.AppendLine($"        \"last_report_time\":{DateTimeOffset.Now.ToUnixTimeSeconds()},");
      sb.AppendLine($"        \"pct_pm_data_last_1_hour\":{thisAirLink.PM25_last_1_hourList.Count / 60.0 * 100:F0},");
      sb.AppendLine($"        \"pct_pm_data_last_3_hours\":{thisAirLink.PM25_last_3_hourList.Count / (3 * 60.0) * 100:F0},");
      sb.AppendLine($"        \"pct_pm_data_last_24_hours\":{thisAirLink.PM25_last_24_hourList.Count / (24 * 60.0) * 100:F0},");
      sb.AppendLine($"        \"pct_pm_data_nowcast\":{(thisAirLink.PM25_last_24_hourList.Count > 12 * 60 ? 12 * 60 : thisAirLink.PM25_last_24_hourList.Count) / (12 * 60.0) * 100:F0},");
      sb.AppendLine("      } ]");  // End Of Conditions
      sb.AppendLine("    },"); // End of Data
      sb.AppendLine("    \"error\":null");
      sb.AppendLine("  }");

      string Output = sb.ToString();

      Sup.LogTraceInfoMessage($"Webserver: Response: {Output}");

      byte[] bOutput = Encoding.UTF8.GetBytes(Output);
      Response.ContentType = "application/json";
      Response.ContentLength64 = bOutput.Length;

      Response.OutputStream.Write(bOutput, 0, bOutput.Length);
      Response.Close();

      return;
    }
  }// Class Webserver
} // Namespace
