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
using QPlatform.Logix;
using QPlatform.CommunicationDriver;
using QPlatform.CoreBase;
using QPlatform.Alarm;
using QPlatform.Core;
#endregion

public class Datalogger : BaseNetLogic
{
    QPlatform.Store.Store myStore;
    QPlatform.Store.StoreColumn timeCol;

    // 

    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }
}
