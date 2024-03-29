A sensor handling program for CumulusUtils, including an AirLink Simulator

© Copyright 2020 Hans Rottier hans.rottier@gmail.com

Author: 	Hans Rottier hans.rottier@gmail.com
Project: 	CUSensorArray, part of CumulusUtils project meteo-wagenborgen.nl 
Dates: 		Startdate project : August 2020 
		Initial release: 11 October 2020 
Environment: 	Raspberry 3B+ 
		Raspbian / Linux C# / Visual Studio NET Core 3.1 
		(https://docs.microsoft.com/en-gb/dotnet/core/install/linux-debian) 
		(https://docs.microsoft.com/en-gb/dotnet/core/install/how-to-detect-installed-versions?pivots=os-linux) 
License: 	GNU GENERAL PUBLIC LICENSE, Version 3, 29 June 2007
Files: 		Program.cs 
		EmulateAirLink.cs
		i2c.cs 
                Inifile.cs
		OneWire.cs (nothing there) 
		Serial.cs 
		Support.cs
                LICENSE
                ReadMe.txt 

This is a program for the Raspberry Pi to readout serveral sensors through the GPIO pins. Currently two sensors (PMS1003 and SHT31) on the serial and the i2c interface are supported. It is meant to run under Linux Buster as a .NET Core 3.1 dll on a Raspberry Pi and it emulates a Davis AirLink device. It is meant as a hobby project, not as a direct replacement of the Davis AirLink although the sensors use are of similar quality.

Data is written out to CUSensorArray.txt for local use.

A small webserver is present to mimic the Davis interface if its AirLink Air quality sensor. This is especially designed for CumulusMX, but any (and I mean any) request to this server will get as a reply the JSON structure with the current data. All averages are being served including the nowcast exponential interpolation.

If you wish to develop before having a sensor you can use the FakeAirLink with Dummy or Simulator devices (specify in the ini-file

I bought the sensors and the breakout circuit for roughly 35 euro, some wires and breadboard you're supposed to have. Add a Raspberry Pi (from 3B+) for euro 35 and you'll have a cheap solution with high learning capability. Look carefully to the wiring / pins.

When compiled and dotnet is installed, run as follows:

	nohup sudo dotnet CuSensorArray.dll&

In version 0.4.0 configurability is added through an inifile CUSensorArray.ini. Run once to generate a file with empty fields, the program exits. 
In version 1.0.0 Everything is finished and ready to go with the PMS1003 and the SHT31. Use Sensor.Community by creating a sensor on their site. The sensorID you will find in the  logfile after running once with SensorCommunity=true. Only use the numerical part in the sensor.community sensor creation form.

Any questions? Let me know.

The inifile looks as follows:

--------------------------------
[General]
TraceInfo=Warning		Possible Error, Warning, Info, Verbose, None
AirLinkEmulation=true		true or false
UseCalibration=false		true or false
SensorCommunity=true		true or false

[AirLinkDevices]		Defines which sensors constitute the AirLink
PMdevice=Serial0		
THdevice=I2C0

[SerialDevices]			Defines the serial [PM]sensors
Serial0=PMS1003			Empty devices spec will be auto filled with Dummy
;Serial0=Simulator		Simulator generates numbers from midnight to its maximum in a straight line (max: 50, 100, 200)
Serial1=

[I2CDevices]			Defines the I2C sensors
I2C0=SHT31			Empty devices spec will be auto filled with Dummy
;I2C0=Simulator			Simulator generates numbers from midnight to its maximum in a straight line (max: 50, 100, 200)
I2C1=
I2C2=
I2C3=
I2C4=
I2C5=
I2C6=
I2C7=

[PortDefinitions]
PMS1003_SerialPort=/dev/ttyS0
PMS1003_SerialBaudrate=9600
PMS1003_SerialParity=None
PMS1003_SerialDataBits=8
PMS1003_SerialStopBits=One
--------------------------------

NOTE: it must be run as root to prevent problems with GPIO accessibility. The RPi can be used for other purposes but don't overload it, GPIO is time critical. Close the commandline window when it's running, you can check its operation through tail -f CUSensorArray.txt which shows whether it is still running or not. If CMX is running and properly configured it will poll for the AirLink and the CUSensorArray will respond as an AirLink and you will find your data in the CMX AirLink data file.

NOTE: the program  is stopped by sending it the signal SIGINT.
Other sensors can (and will)e added.

[Wiring] :
Wiring is your own of course but make sure the datalines are as below.
[PMS1003]
    Enable: +3v;
    Reset: +3v;
    Rx: GPIO15;
    Tx: GPIO14;
    Gnd: Ground;
    Power: +5v;

[SHT31]
    Vin: +3v
    Gnd: Gnd
    SCL: SCL i2c (pin 5)
    SDA: SDA i2c (pin 3)
