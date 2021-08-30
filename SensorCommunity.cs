using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CuSensorArray
{
    internal class SensorCommunity
    {
        const string URL = "https://api.sensor.community/v1/push-sensor-data/";

        readonly string SensorID;
        readonly Support Sup;
        readonly HttpClient SensorCommunityHttpClient;

        internal SensorCommunity( Support s )
        {
            string line;

            Sup = s;
            Sup.LogDebugMessage( $"SensorCommunity ctor: Start" );

            SensorCommunityHttpClient = new HttpClient();
            SensorCommunityHttpClient.Timeout = TimeSpan.FromSeconds(30);
            SensorCommunityHttpClient.BaseAddress = new Uri(URL);

            using ( StreamReader cpuFile = new StreamReader( "/proc/cpuinfo" ) )
            {
                line = cpuFile.ReadLine();
                Sup.LogDebugMessage( $"SensorCommunity ctor reading cpuinfo: {line}" );

                do
                {
                    if ( line.Contains( "Serial", StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        Sup.LogDebugMessage( $"SensorCommunity ctor: Serial line found => {line}" );
                        string[] splitstring = line.Split( ':' );
                        SensorID = "raspi-" + splitstring[ 1 ].Trim();

                        Sup.LogDebugMessage( $"SensorCommunity ctor: SensorID = {SensorID}" );
                        break;
                    }

                    line = cpuFile.ReadLine();
                } while ( true ); // end while
            } // end using => disposes the cpuFile

            Sup.LogDebugMessage( $"SensorCommunity ctor end" );
        } // end constructor

        ~SensorCommunity()
        {
            // LuftdatenHttpClient.Dispose();
        }

        internal async Task Send(EmulateAirLink thisEmulator)
        {
            string HTDataToSend;
            string PMDataToSend;

            HTDataToSend = "{ " +
                $"\"timestamp:\" : \"{DateTime.Now}\", " +
                $"\"sensordatavalues\" : [" +
                $"{{ \"value_type\":\"temperature\",\"value\":\"{thisEmulator.TemperatureC:F1}\" }}," +
                $"{{ \"value_type\":\"humidity\",\"value\":\"{thisEmulator.Humidity:F1}\"}}" +
                $"]" +
            "}";

            PMDataToSend = "{ " +
                $"\"timestamp:\" : \"{DateTime.Now}\", " +
                $"\"sensordatavalues\" : [" +
                $"{{ \"value_type\":\"P1\",\"value\":\"{thisEmulator.PM10_last:F2}\"}}," +
                $"{{ \"value_type\":\"P2\",\"value\":\"{thisEmulator.PM25_last:F2}\"}}" +
                $"]" +
            "}";

            Sup.LogTraceInfoMessage( $" PMDataToSend {PMDataToSend} " );
            Sup.LogTraceInfoMessage( $" HTDateToSend {HTDataToSend} " );

            await PostToSensorCommunity( HTDataToSend, PMDataToSend );
        }

        private async Task PostToSensorCommunity( string HTdata, string PMdata)
        {
            try
            {
                SensorCommunityHttpClient.CancelPendingRequests();

                using ( StringContent content = new StringContent( HTdata, Encoding.UTF8, "application/json" ) )
                {
                    // BME280 use 11   for SHT31 use 7
                    content.Headers.Add("X-pin", "7"); 
                    content.Headers.Add( "X-Sensor", $"{SensorID}" );

                    using ( HttpResponseMessage response = await SensorCommunityHttpClient.PostAsync( "", content ) )
                    {
                        if ( response.IsSuccessStatusCode )
                            Sup.LogTraceInfoMessage( $"PostToSensorCommunity : OK - {response.StatusCode} - {await response.Content.ReadAsStringAsync()}" );
                        else
                            Sup.LogTraceErrorMessage( $"PostToSensorCommunity : Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}" );
                    }
                }

                using ( StringContent content = new StringContent( PMdata, Encoding.UTF8, "application/json" ) )
                {
                    content.Headers.Add( "X-pin", "1" ); // BME280 use 11   for SHT31 use 7
                    content.Headers.Add( "X-Sensor", $"{SensorID}" );

                    using ( HttpResponseMessage response = await SensorCommunityHttpClient.PostAsync( "", content ) )
                    {
                        if ( response.IsSuccessStatusCode )
                            Sup.LogTraceInfoMessage( $"PostToSensorCommunity : OK - {response.StatusCode} - {await response.Content.ReadAsStringAsync()}" );
                        else
                            Sup.LogTraceErrorMessage( $"PostToSensorCommunity : Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}" );
                    }
                }
            }
            catch ( Exception e )
            {
                Sup.LogTraceErrorMessage( $"PostToSensorCommunity : Exception - {e.Message}" );
            }

            return;
        }


    }
}
