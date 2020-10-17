using System;
using System.Collections.Generic;
using System.Linq;
using zeroWsensors;

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
}
