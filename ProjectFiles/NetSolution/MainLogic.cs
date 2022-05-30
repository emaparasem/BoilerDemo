#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.HMIProject;
using FTOptix.WebUI;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.Recipe;
using FTOptix.Datalogger;
using FTOptix.EventLogger;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.OPCUAClient;
using FTOptix.CoreBase;
using FTOptix.CommunicationDriver;
using FTOptix.Alarm;
using FTOptix.Core;
using FTOptix.Logix;
using FTOptix.EthernetIP;
#endregion

public class MainLogic : BaseNetLogic
{
    public override void Start()
    {
        var highLevel = Project.Current.GetVariable("OPC-UA/OPCUAClient/Objects/Demo/BoilerDemo/Boiler1/FillLevelSensor/FillLevel/EURange/High");
        if (highLevel != null)
            highLevel.Value = 100;
        var minLevel = Project.Current.GetVariable("OPC-UA/OPCUAClient/Objects/Demo/BoilerDemo/Boiler1/FillLevelSensor/FillLevel/EURange/Low");
        if (minLevel != null)
            minLevel.Value = 0;
        var highTemperature = Project.Current.GetVariable("OPC-UA/OPCUAClient/Objects/Demo/BoilerDemo/Boiler1/TemperatureSensor/Temperature/EURange/High");
        if (highTemperature != null)
            highTemperature.Value = 90;
        var lowTemperature = Project.Current.GetVariable("OPC-UA/OPCUAClient/Objects/Demo/BoilerDemo/Boiler1/TemperatureSensor/Temperature/EURange/Low");
        if (lowTemperature != null)
            lowTemperature.Value = -10;
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }
}
