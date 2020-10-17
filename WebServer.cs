/*
 * CUSensorArray / WebServer.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

// https://gist.github.com/joeandaverde/3994603
// https://weblog.west-wind.com/posts/2005/Dec/04/Add-a-Web-Server-to-your-NET-20-app-with-a-few-lines-of-code  (this one is used)
// https://16bpp.net/tutorials/csharp-networking/02/
// https://stackoverflow.com/questions/9034721/handling-multiple-requests-with-c-sharp-httplistener

namespace zeroWsensors
{

  public delegate void delReceiveWebRequest(HttpListenerContext Context);

  public class WebServer
  {
    readonly Support Sup;
    readonly I2C thisI2C;
    readonly Serial thisSerial;
    readonly EmulateAirLink thisAirLink;

    protected HttpListener Listener;
    protected bool IsStarted = false;

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

      // *** Already running - just leave it in place
      if (this.IsStarted) return;

      if (this.Listener == null) this.Listener = new HttpListener();

      this.Listener.Prefixes.Add(baseURL);

      try
      {
        this.Listener.Start();
        this.IsStarted = true;  

        IAsyncResult result = this.Listener.BeginGetContext(new AsyncCallback(WebRequestCallback), this.Listener);
      }
      catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException )
      {
        Sup.LogTraceWarningMessage($"Webserver: Start Exception - {e.Message}");
        Stop();
      }
    }

    public void Stop()
    {
      Sup.LogDebugMessage("Webserver: Stop");

      if (Listener != null)
      {
        try
        {
          this.Listener.Close();
          this.Listener = null;
          this.IsStarted = false;
        }
        catch(Exception e)
        {
          Sup.LogTraceWarningMessage($"Webserver: Stop Exception - {e.Message}");
        }
      }
    }

    protected void WebRequestCallback(IAsyncResult result)
    {
      Sup.LogTraceInfoMessage("Webserver: WebRequestCallback");

      if (this.Listener == null) return;

      HttpListenerContext context = this.Listener.EndGetContext(result);

      // Immediately set up the next context
      this.Listener.BeginGetContext(new AsyncCallback(WebRequestCallback), this.Listener);
      this.ReceiveWebRequest?.Invoke(context);
      this.ProcessRequest(context);
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

      Sup.LogTraceInfoMessage(Request.HttpMethod + " " + Request.RawUrl + " Http/" + Request.ProtocolVersion.ToString());
      if (Request.UrlReferrer != null) Sup.LogTraceInfoMessage("Referer: " + Request.UrlReferrer);
      if (Request.UserAgent != null) Sup.LogTraceInfoMessage("User-Agent: " + Request.UserAgent);

      for (int x = 0; x < Request.Headers.Count; x++)
        Sup.LogTraceInfoMessage("Request header: " + Request.Headers.Keys[x] + ":" + " " + Request.Headers[x]);

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

      byte[] bOutput = System.Text.Encoding.UTF8.GetBytes(Output);
      Response.ContentType = "application/json";
      Response.ContentLength64 = bOutput.Length;

      Stream OutputStream = Response.OutputStream;
      OutputStream.Write(bOutput, 0, bOutput.Length);
      OutputStream.Close();
      Response.Close();
    }
  }// Class Webserver
}// Namespace
