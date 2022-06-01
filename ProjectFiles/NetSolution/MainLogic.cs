#region Using directives
<<<<<<< HEAD
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.HMIProject;
using QPlatform.WebUI;
using QPlatform.UI;
using QPlatform.NativeUI;
using QPlatform.Recipe;
using QPlatform.Datalogger;
using QPlatform.EventLogger;
using QPlatform.SQLiteStore;
using QPlatform.Store;
using QPlatform.OPCUAClient;
using QPlatform.CoreBase;
using QPlatform.CommunicationDriver;
using QPlatform.Alarm;
using QPlatform.Core;
using QPlatform.Logix;
using QPlatform.EthernetIP;
=======
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using UAManagedCore;
using FTOptix.OPCUAServer;
>>>>>>> 1363862374e586794e48f462c33b07a3d60e64bf
#endregion

public class MainLogic : BaseNetLogic {
    public override void Start() {
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

    public override void Stop() {
        // Insert code to be executed when the user-defined logic is stopped
    }
}
