#region Using directives
using System;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.OPCUAServer;
using FTOptix.Recipe;
using FTOptix.UI;
using FTOptix.Store;
using FTOptix.SQLiteStore;
#endregion

public class RecipesEditorComboBoxLogic : BaseNetLogic
{
	public override void Start()
	{
		var comboBox = (ComboBox)Owner;
		comboBox.SelectedValueVariable.VariableChange += SelectedValueVariable_VariableChange;
	}

	private void SelectedValueVariable_VariableChange(object sender, VariableChangeEventArgs e)
	{
		var comboBox = (ComboBox)Owner;
		if (comboBox.SelectedValue == null)
			return;

		var recipeSchemaEditor = Owner.Owner;
		var recipeSchemaVariable = recipeSchemaEditor.GetVariable("RecipeSchema");
		if (recipeSchemaVariable == null)
			return;

		var recipeSchemaNodeId = (NodeId)recipeSchemaVariable.Value.Value;

		var recipeSchemaObject = (RecipeSchema)InformationModel.Get(recipeSchemaNodeId);
		if (recipeSchemaObject == null)
			return;

		var editModelNode = recipeSchemaObject.GetObject("EditModel");
		if (editModelNode == null)
			return;

		var recipeNameLocalizedText = comboBox.SelectedValue as LocalizedText;
		if (recipeNameLocalizedText == null || recipeNameLocalizedText.IsEmpty())
			return;

		var recipeName = recipeNameLocalizedText.Text;

		recipeSchemaObject.CopyFromStoreRecipe(recipeName, editModelNode.NodeId, CopyErrorPolicy.BestEffortCopy);
	}

	public override void Stop()
	{
		var comboBox = (ComboBox)Owner;
		comboBox.SelectedValueVariable.VariableChange -= SelectedValueVariable_VariableChange;
	}
}
