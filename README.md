# CUSensorArray
A sensor handling program for CumulusUtils, including an AirLink Simulator

/*
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
 * Files:       Program.cs
 *              i2c.cs
 *              OneWire.cs (nothing there)
 *              Serial.cs
 *              Support.cs
 *              WebServer.cs
 */

This is a program for the Raspberry Pi to readout serveral sensors through the GPIO pins. 
Currently two sensors (PMS1003 and SHT31) on the serial and the i2c interface are supported. 
It is meant to run under Linux Buster as a .NET Core 3.1 dll on a Raspberry Pi and it emulates a Davis AirLink device.
It is meant as a hobby project, not as a direct replacement of the Davis AirLink.

Data is written out to CUSensorArray.txt.

A small webserver is present to mimic the Davis interface if its AirLink Air quality sensor. This is especially designed for CumulusMX, but any (and I mean any) 
request to this server will get as a reply the JSON structure with the current data. All averages are being served, the nowcast exponential interpolation still 
has to b e done and gets a -1 as value.

I bought the sensors and the breakout circuit for roughly 35 euro, some wires and breadboard you're supposed to have.
Add a Raspberry Pi (from 3B+) for euro 35. Look carfully to the wiring / pins.

When compiled and dotnet is installed, run as follows:

    nohup dotnet sudo dotnet CUSensorArray.dll
    
NOTE: it must be run as root to prevent problems with GPIO accessibility. The RPi can be used for other purposes but don't overload it, GPIO is time critical. 
      Close the commandline window when it's running, you can check its operation through tail -f sensors.log which shows whether it is still running or not.
      If CMX is running and properly configured it will poll for the AirLink and the CUSensorArray will respond as an AirLink and you will find you data
      in the CMX AirLink data file.
      
Other sensors can (and will)e added.

[Wiring] :

Wiring is your own of course but make sure the datalines are as below.

[PMS1003]
Enable: +3v
Reset:  +3v
Rx:     GPIO15
Tx:     GPIO14
Gnd:    Ground
Power:  +5v

[SHT31]
Vin:    +3v
Gnd:    Gnd
Scl:    SCL i2c (pin 5)
Sda:    SDA i2c (pin 3)
