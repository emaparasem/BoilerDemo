#region Using directives
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.Store;
using System;
using UAManagedCore;
<<<<<<< HEAD
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.Alarm;
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
using QPlatform.EthernetIP;
using QPlatform.CoreBase;
using QPlatform.CommunicationDriver;
using QPlatform.Core;
=======
using FTOptix.OPCUAServer;
>>>>>>> 1363862374e586794e48f462c33b07a3d60e64bf
#endregion

public class AuditLogic : BaseNetLogic {
    public override void Start() {
        // Insert code to be executed when the user-defined logic is started
    }

    public override void Stop() {
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
