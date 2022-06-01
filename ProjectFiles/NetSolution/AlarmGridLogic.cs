#region Using directives
<<<<<<< HEAD
using UAManagedCore;
using QPlatform.NetLogic;
using QPlatform.UI;
=======
using FTOptix.NetLogic;
using FTOptix.UI;
using UAManagedCore;
using FTOptix.OPCUAServer;
>>>>>>> 1363862374e586794e48f462c33b07a3d60e64bf
#endregion

public class AlarmGridLogic : BaseNetLogic {
    public override void Start() {
        alarmsDataGridModel = Owner.Get<DataGrid>("AlarmsDataGrid").GetVariable("Model");

        var currentSession = LogicObject.Context.Sessions.CurrentSessionInfo;
        actualLanguagesVariable = currentSession.SessionObject.Get<IUAVariable>("ActualLanguages");
        actualLanguagesVariable.VariableChange += OnSessionActualLanguagesChange;
    }

    public override void Stop() {
        actualLanguagesVariable.VariableChange -= OnSessionActualLanguagesChange;
    }

    public void OnSessionActualLanguagesChange(object sender, VariableChangeEventArgs e) {
        var dynamicLink = alarmsDataGridModel.GetVariable("DynamicLink");
        if (dynamicLink == null)
            return;

        // Restart the data bind on the data grid model variable to refresh data
        string dynamicLinkValue = dynamicLink.Value;
        dynamicLink.Value = string.Empty;
        dynamicLink.Value = dynamicLinkValue;
    }

    private IUAVariable alarmsDataGridModel;
    private IUAVariable actualLanguagesVariable;
}
