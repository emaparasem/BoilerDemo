#region Using directives
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
#endregion

public class RuntimeNetLogic1 : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void Method1()
    {
        // Insert code to be executed by the method
    }
}
