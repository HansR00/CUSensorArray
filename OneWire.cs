/*
 * CUSensorArray / OneWire.cs
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

namespace zeroWsensors
{
  // DHT22 : http://www.uugear.com/portfolio/read-dht1122-temperature-humidity-sensor-from-raspberry-pi/
  // DHT22 : specs / timings
  //

  class OneWire
  {
    readonly Support Sup;

    public OneWire(Support s)
    {
      Sup.LogDebugMessage("OneWire: Constructor...");
      Sup = s;
    }

    ~OneWire()
    {
      Sup.LogDebugMessage("OneWire: Destructor...");
    }
  }
}

