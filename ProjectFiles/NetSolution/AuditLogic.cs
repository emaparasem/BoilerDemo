#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.Alarm;
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
using FTOptix.EthernetIP;
using FTOptix.CoreBase;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
#endregion

public class AuditLogic : BaseNetLogic
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
    public void InsertRow(String recipeName, String recipeAction) {
        try {
            var myStore = Project.Current.Get<Store>("DataStores/EmbeddedDatabase");
            var myTable = myStore.Tables.Get<Table>("RecipesAudit");
            string[] columns = { "Timestamp", "RecipeName", "Action" };
            if (recipeName == "") {
                recipeName = "Current Recipe";
            }
            var values = new object[1, 3];
            values[0, 0] = DateTime.Now;
            values[0, 1] = recipeName;
            values[0, 2] = recipeAction;
            myTable.Insert(columns, values);
            Log.Info("Audit." + recipeAction, "Inserting " + recipeName + " Action: " + recipeAction);
        } catch (Exception ex) {
            Log.Error("Audit", ex.Message.ToString());
        }
    }
}
