#region Using directives
using System;
using System.Collections.Generic;
using System.Linq;
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using OpcUa = UAManagedCore.OpcUa;
using System.Text;
using FTOptix.Core;
using System.IO;
#endregion

#region Internal classes

public interface ProjectAnalyzer
{
    void Verify();
    string GetAnalysisReport();
    bool HasSomethingToOptimize();
    void Optimize();
    string AnalysisName();
}

public class DataItemTemplateAnalyzer : ProjectAnalyzer
{
    public DataItemTemplateAnalyzer(IContext context_)
    {
        context = context_;
    }

    public void Verify()
    {
        dataItemTemplatesToBeOptimized = new List<IUANode>();

        VerifyReportDataItemTemplates();
        VerifyUIDataItemTemplates();
    }

    public bool HasSomethingToOptimize()
    {
        return dataItemTemplatesToBeOptimized.Count != 0;
    }

    public string AnalysisName()
    {
        return "Data item templates";
    }

    private void VerifyReportDataItemTemplates()
    {
        var dataGridColumnType = context.GetObjectType(FTOptix.Report.ObjectTypes.ReportDataGridColumn);
        VerifyDataItemTemplates(dataGridColumnType.GetInstances(), uiNamespaceIndex);
    }

    private void VerifyUIDataItemTemplates()
    {
        var dataGridColumnType = context.GetObjectType(FTOptix.UI.ObjectTypes.DataGridColumn);
        VerifyDataItemTemplates(dataGridColumnType.GetInstances(), uiNamespaceIndex);
    }

    private void VerifyDataItemTemplates(IReadOnlyList<IUANode> dataGridColumns, int namespaceIndexToMatch)
    {
        foreach (var dataGridColumn in dataGridColumns)
        {
            var dataGridItemTemplate = dataGridColumn.Children["DataItemTemplate"];
            if (dataGridItemTemplate == null)
                continue;

            if (dataGridItemTemplate.QualifiedBrowseName.NamespaceIndex != namespaceIndexToMatch)
                dataItemTemplatesToBeOptimized.Add(dataGridItemTemplate);
        }
    }

    public string GetAnalysisReport()
    {
        if (!HasSomethingToOptimize())
            return "No DataItemTemplates to be optimized";

        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"Number of DataItemTemplates to be optimized: {dataItemTemplatesToBeOptimized.Count}");
        stringBuilder.AppendLine("DataItemTemplates to be optimized:");
        foreach (var dataItemTemplate in dataItemTemplatesToBeOptimized)
            stringBuilder.AppendLine($"\t- {Log.Node(dataItemTemplate)}");

        return stringBuilder.ToString();
    }

    public void Optimize()
    {
        foreach (var dataItemTemplate in dataItemTemplatesToBeOptimized)
            dataItemTemplate.QualifiedBrowseName = new QualifiedName(uiNamespaceIndex, "DataItemTemplate");
    }

    private readonly int uiNamespaceIndex = FTOptix.UI.ObjectTypes.DataGridColumn.NamespaceIndex;
    private List<IUANode> dataItemTemplatesToBeOptimized;
    private readonly IContext context;
}

public class PrototypeComputer
{
    public Dictionary<IUANode, IUANode> ComputePrototypes()
    {
        return ComputePrototypes(Project.Current);
    }

    public Dictionary<IUANode, IUANode> ComputePrototypes(IUANode rootNode)
    {
        computedPrototypes = new Dictionary<IUANode, IUANode>();

        ComputePrototypeRecursive(rootNode);

        return computedPrototypes;
    }

    private void ComputePrototypeRecursive(IUANode node)
    {
        if (node.IsObjectOrVariable())
            computedPrototypes.Add(node, ComputePrototype(node));

        foreach (var childNode in node.Children)
            ComputePrototypeRecursive(childNode);
    }

    private IUANode ComputePrototype(IUANode node)
    {
        List<QualifiedName> browsePath = new List<QualifiedName>();
        IUANode matchingNode = null, newMatchingNode = null;

        while (node != null)
        {
            browsePath.Add(node.QualifiedBrowseName);

            node = node.Owner;
            if (node == null)
                return matchingNode;

            if (node.IsObjectTypeOrVariableType())
                newMatchingNode = FindNodeInTypeOrSuperType(node.GetSuperType(), browsePath);
            else if (node.IsObjectOrVariable())
                newMatchingNode = FindNodeInTypeOrSuperType(node.GetTypeNode(), browsePath);
            else
                matchingNode = null;

            if (newMatchingNode != null)
                matchingNode = newMatchingNode;
        }

        return matchingNode;
    }

    private IUANode FindNodeInTypeOrSuperType(IUANode typeNode, List<QualifiedName> browsePath)
    {
        while (typeNode != null)
        {
            var childNode = FindNode(typeNode, browsePath);
            if (childNode != null)
                return childNode;

            typeNode = typeNode.GetSuperType();
        }

        return null;
    }

    private IUANode FindNode(IUANode parentNode, List<QualifiedName> browsePath)
    {
        IUANode currentNode = parentNode;

        for (int i = browsePath.Count - 1; i >= 0; --i)
        {
            var childNode = currentNode.Refs.GetNode(browsePath[i]);
            if (childNode == null)
                return null;

            if (childNode.Owner != currentNode)
                return null;

            currentNode = childNode;
        }

        return currentNode;
    }

    private Dictionary<IUANode, IUANode> computedPrototypes;
}

public class OptionalInstanceDeclarationOptimizer
{
    public void Verify(Dictionary<IUANode, IUANode> prototypesToApply_)
    {
        prototypesToApply = prototypesToApply_;

        variablesToBeMaterializedByNode = new Dictionary<IUANode, List<VariableToBeMaterialized>>();

        foreach (var item in prototypesToApply)
        {
            var node = item.Key;
            var newPrototype = item.Value;
            var previousPrototype = node.GetPrototype();

            if (previousPrototype != null && previousPrototype != newPrototype)
                VerifyOptionalInstanceDeclarations(node, previousPrototype);
        }

        report = IterateVariablesToBeMaterialized(false);
    }

    public string GetAnalysisReport()
    {
        return report;
    }

    public bool SomethingToOptimize()
    {
        return variablesToBeMaterializedByNode.Count != 0;
    }

    public void Optimize()
    {
        report = IterateVariablesToBeMaterialized(true);
    }

    private void VerifyOptionalInstanceDeclarations(IUANode node, IUANode previousPrototype)
    {
        var variablesToBeMaterialized = new List<VariableToBeMaterialized>();
        var instanceDeclarationBrowseNames = GetOptionalInstanceDeclarationBrowseNames(node);
        foreach (var browseName in instanceDeclarationBrowseNames)
        {
            if (node.Refs.GetVariable(browseName) != null)
                continue;

            var variableOnPreviousPrototype = GetVariableOnPreviousPrototype(previousPrototype, browseName);
            if (variableOnPreviousPrototype == null)
            {
                Log.Error($"Unable to retrieve variable {browseName} on node {Log.Node(previousPrototype)}");
                continue;
            }

            var converter = variableOnPreviousPrototype.Refs.GetNode(FTOptix.CoreBase.ReferenceTypes.HasConverter);
            if (converter != null)
            {
                Log.Error($"Unexpected converter {converter.BrowseName} on {Log.Node(variableOnPreviousPrototype)}, " +
                    $"previous prototype for variable '{browseName}' on node {Log.Node(node)}. New prototype for node will be " +
                    $"{(prototypesToApply[node] != null ? Log.Node(prototypesToApply[node]) : "null")}.");
                continue;
            }

            var dynamicLink = variableOnPreviousPrototype.Refs.GetNode(FTOptix.CoreBase.ReferenceTypes.HasDataBind) as DataBind;
            if (dynamicLink != null)
            {
                string dynamicLinkPath = dynamicLink.Value;
                if (!dynamicLink.Children.Any() && dynamicLinkPath.StartsWith("{NodeId:"))
                {
                    var pointedVariable = node.Context.ResolvePath(dynamicLinkPath).ResolvedNode as IUAVariable;
                    if (pointedVariable != null)
                    {
                        var dynamicLinkToMaterialize = new VariableToBeMaterialized();
                        dynamicLinkToMaterialize.isDynamicLink = true;
                        dynamicLinkToMaterialize.BrowseName = browseName;
                        dynamicLinkToMaterialize.PointedVariable = pointedVariable;
                        dynamicLinkToMaterialize.DynamicLinkMode = dynamicLink.Mode;
                        variablesToBeMaterialized.Add(dynamicLinkToMaterialize);

                        continue;
                    }
                }

                Log.Error($"Unexpected dynamic link {dynamicLink.BrowseName} on {Log.Node(variableOnPreviousPrototype)}, " +
                    $"previous prototype for variable '{browseName}' on node {Log.Node(node)}. New prototype for node will be " +
                    $"{(prototypesToApply[node] != null ? Log.Node(prototypesToApply[node]) : "null")}.");
                continue;
            }

            var objectValue = GetNewOptionalVariableValue(node, browseName);
            var previousPrototypeValue = variableOnPreviousPrototype.Value;
            if ((objectValue.Value == null && previousPrototypeValue.Value == null) || (objectValue.Value != null && objectValue == previousPrototypeValue))
                continue;

            var variableToMaterialize = new VariableToBeMaterialized();
            variableToMaterialize.BrowseName = browseName;
            variableToMaterialize.ObjectValue = objectValue;
            variableToMaterialize.PreviousPrototypeValue = previousPrototypeValue;
            variablesToBeMaterialized.Add(variableToMaterialize);
        }

        if (variablesToBeMaterialized.Count > 0)
            variablesToBeMaterializedByNode.Add(node, variablesToBeMaterialized);
    }

    private string IterateVariablesToBeMaterialized(bool fix)
    {
        if (variablesToBeMaterializedByNode.Count == 0)
            return "No optional variables to be materialized";

        StringBuilder stringBuilder = new StringBuilder();

        foreach (var item in variablesToBeMaterializedByNode)
        {
            var node = item.Key;
            var variablesToBeMaterialized = item.Value;
            stringBuilder.AppendLine($"Changes after prototype fix for {Log.Node(node)}:");
            foreach (var variableToBeMaterialized in variablesToBeMaterialized)
            {
                if (variableToBeMaterialized.isDynamicLink)
                {
                    stringBuilder.AppendLine($"\t- {variableToBeMaterialized.BrowseName} dynamic link to {Log.Node(variableToBeMaterialized.PointedVariable)}");

                    if (fix)
                    {
                        var logAfterFix = FixDynamicLink(node, variableToBeMaterialized);
                        stringBuilder.Append($"\t\t {logAfterFix}");
                    }
                }
                else
                {
                    stringBuilder.AppendLine($"\t- {variableToBeMaterialized.BrowseName} value: {variableToBeMaterialized.ObjectValue} -> {variableToBeMaterialized.PreviousPrototypeValue}");

                    if (fix)
                    {
                        var logAfterFix = FixOptionalVariable(node, variableToBeMaterialized);
                        stringBuilder.Append($"\t\t {logAfterFix}");
                    }
                }
            }
        }

        return stringBuilder.ToString();
    }

    private string FixDynamicLink(IUANode node, VariableToBeMaterialized dynamicLinkToBeMaterialized)
    {
        var newVariable = GetOrCreateVariable(node, dynamicLinkToBeMaterialized.BrowseName);
        newVariable.SetDynamicLink(dynamicLinkToBeMaterialized.PointedVariable, dynamicLinkToBeMaterialized.DynamicLinkMode);
        var newDynamicLink = newVariable.Refs.GetVariable(FTOptix.CoreBase.ReferenceTypes.HasDataBind);

        ComputePrototypeForNewlyMaterializedVariable(newVariable);
        ComputePrototypeForNewlyMaterializedVariable(newDynamicLink);

        return $"new prototype: {Log.Node(newVariable.Prototype)}";
    }

    private string FixOptionalVariable(IUANode node, VariableToBeMaterialized variableToBeMaterialized)
    {
        var materialized = GetOrCreateVariable(node, variableToBeMaterialized.BrowseName);
        materialized.Value = variableToBeMaterialized.PreviousPrototypeValue;

        ComputePrototypeForNewlyMaterializedVariable(materialized);

        return $"new prototype {Log.Node(materialized.Prototype)}";
    }

    private List<string> GetOptionalInstanceDeclarationBrowseNames(IUANode node)
    {
        IUANode typeNode = null;
        if (node.IsObjectOrVariable())
            typeNode = node.GetTypeNode();
        else if (node.IsObjectTypeOrVariableType())
            typeNode = node;

        if (typeNode == null)
            return new List<string>();

        List<string> result = new List<string>();
        while (typeNode != null)
        {
            var variableInstanceDeclarations = typeNode.Children.OfType<IUAVariable>();
            var optionalInstanceDeclarations = variableInstanceDeclarations.Where(v => v.ModellingRule == NamingRuleType.Optional);
            foreach (var instanceDeclaration in optionalInstanceDeclarations)
            {
                if (!result.Contains(instanceDeclaration.BrowseName))
                    result.Add(instanceDeclaration.BrowseName);
            }

            typeNode = typeNode.GetSuperType();
        }

        return result;
    }

    private IUAVariable GetVariableOnPreviousPrototype(IUANode node, string name)
    {
        var instanceVariable = node.Refs.GetVariable(name);
        if (instanceVariable != null)
            return instanceVariable;

        var prototype = node;
        while (prototype != null)
        {
            instanceVariable = prototype.Refs.GetVariable(name);
            if (instanceVariable != null && instanceVariable.ModellingRule != NamingRuleType.None)
                return instanceVariable;

            prototype = prototype.GetPrototype();
        }

        var superType = node.GetTypeNode();
        while (superType != null)
        {
            instanceVariable = superType.Refs.GetVariable(name);
            if (instanceVariable != null && instanceVariable.ModellingRule != NamingRuleType.None)
                return instanceVariable;

            superType = superType.GetSuperType();
        }

        return null;
    }

    private UAValue GetNewOptionalVariableValue(IUANode node, string name)
    {
        IUAVariable instanceVariable;
        var prototype = prototypesToApply[node];
        while (prototype != null)
        {
            instanceVariable = prototype.Refs.GetVariable(name);
            if (instanceVariable != null && instanceVariable.ModellingRule != NamingRuleType.None)
                return instanceVariable.Value;

            var oldPrototype = prototype;
            if (!prototypesToApply.TryGetValue(oldPrototype, out prototype))
                break;
        }

        var superType = node.GetTypeNode();
        while (superType != null)
        {
            instanceVariable = superType.Refs.GetVariable(name);
            if (instanceVariable != null && instanceVariable.ModellingRule != NamingRuleType.None)
                return instanceVariable.Value;

            superType = superType.GetSuperType();
        }

        return new UAValue();
    }

    private IUAVariable GetOrCreateVariable(IUANode node, string name)
    {
        if (node is IUAObject obj)
            return obj.GetOrCreateVariable(name);

        if (node is IUAVariable variable)
            return variable.GetOrCreateVariable(name);

        if (node is IUAObjectType objectType)
            return objectType.GetOrCreateVariable(name);

        if (node is IUAVariableType variableType)
            return variableType.GetOrCreateVariable(name);

        return null;
    }

    private void ComputePrototypeForNewlyMaterializedVariable(IUAVariable variable)
    {
        var prototypeComputer = new PrototypeComputer();
        var computedPrototypes = prototypeComputer.ComputePrototypes(variable);
        var correctPrototype = computedPrototypes.FirstOrDefault(k => k.Key == variable).Value;
        variable.Prototype = correctPrototype as IUAVariable;
    }

    class VariableToBeMaterialized
    {
        public string BrowseName;
        public UAValue ObjectValue;
        public UAValue PreviousPrototypeValue;
        public bool isDynamicLink = false;
        public IUAVariable PointedVariable;
        public DynamicLinkMode DynamicLinkMode;
    }

    private Dictionary<IUANode, List<VariableToBeMaterialized>> variablesToBeMaterializedByNode;
    private Dictionary<IUANode, IUANode> prototypesToApply;
    private string report;
}

public class OptionalInstanceDeclarationAnalyzer : ProjectAnalyzer
{
    public OptionalInstanceDeclarationAnalyzer()
    {
        optionalInstanceDeclarationOptimizer = new OptionalInstanceDeclarationOptimizer();
    }

    public string AnalysisName()
    {
        return "Optional instance declarations";
    }

    public string GetAnalysisReport()
    {
        return optionalInstanceDeclarationOptimizer.GetAnalysisReport();
    }

    public void Optimize()
    {
        optionalInstanceDeclarationOptimizer.Optimize();
    }

    public bool HasSomethingToOptimize()
    {
        return optionalInstanceDeclarationOptimizer.SomethingToOptimize();
    }

    public void Verify()
    {
        var computer = new PrototypeComputer();
        var computedPrototypes = computer.ComputePrototypes();
        optionalInstanceDeclarationOptimizer.Verify(computedPrototypes);
    }

    private readonly OptionalInstanceDeclarationOptimizer optionalInstanceDeclarationOptimizer;
}

public class InstanceDeclarationAnalyzer : ProjectAnalyzer
{
    public InstanceDeclarationAnalyzer(IContext context_)
    {
        context = context_;
    }

    public void Verify()
    {
        duplicatedNodes = new List<Tuple<string, List<IUANode>>>();

        var baseObjectType = context.GetObjectType(OpcUa.ObjectTypes.BaseObjectType);
        VisitType(baseObjectType);

        var baseVariableType = context.GetVariableType(OpcUa.VariableTypes.BaseVariableType);
        VisitType(baseVariableType);

        report = SerializeVerificationResult();
    }

    public bool HasSomethingToOptimize()
    {
        return duplicatedNodes.Count != 0;
    }

    public string GetAnalysisReport()
    {
        return report;
    }

    public string AnalysisName()
    {
        return "Duplicated properties";
    }

    public void Optimize()
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var duplicatedNode in duplicatedNodes)
        {
            foreach (var node in duplicatedNode.Item2.Skip(1))
            {
                var owner = node.Owner;
                stringBuilder.AppendLine($"Removed duplicated node {Log.Node(node)} - NodeId: {node.NodeId}");
                owner.Remove(node);
            }
        }

        report = stringBuilder.ToString();
    }

    private string SerializeVerificationResult()
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"{Environment.NewLine}Number of duplicated instance declarations {duplicatedNodes.Count}");

        foreach (var duplicatedNode in duplicatedNodes)
        {
            stringBuilder.AppendLine($"Duplicated node {duplicatedNode.Item1}");
            foreach (var node in duplicatedNode.Item2)
                stringBuilder.AppendLine($"\t{node.NodeId}");
        }

        return stringBuilder.ToString();
    }

    private void VisitType(IUANode type)
    {
        var subtypes = type.Refs.GetNodes(OpcUa.ReferenceTypes.HasSubtype, false);
        var instanceDeclarations = type.Children.Except(subtypes);

        VerifyInstanceDeclarations(instanceDeclarations);

        foreach (var subtype in subtypes)
            VisitType(subtype);
    }

    private void VerifyInstanceDeclarations(IEnumerable<IUANode> instanceDeclarations)
    {
        FindDuplicatedNodes(instanceDeclarations);

        foreach (var instanceDeclaration in instanceDeclarations)
            VerifyInstanceDeclarations(instanceDeclaration.Children);
    }

    private void FindDuplicatedNodes(IEnumerable<IUANode> childNodes)
    {
        var nodeGroups = GroupNodesByQualifiedBrowseName(childNodes);
        foreach (var nodeGroup in nodeGroups)
        {
            if (nodeGroup.Count > 1)
                duplicatedNodes.Add(Tuple.Create(Log.Node(nodeGroup.First()), nodeGroup));
        }
    }

    private IEnumerable<List<IUANode>> GroupNodesByQualifiedBrowseName(IEnumerable<IUANode> childNodes)
    {
        return childNodes.GroupBy(x => x.QualifiedBrowseName).Select(g => g.ToList());
    }

    private List<Tuple<string, List<IUANode>>> duplicatedNodes;
    private readonly IContext context;
    private string report;
}

public static class PrototypeAnalyzerExtensions
{
    public static IUANode GetTypeNode(this IUANode node)
    {
        if (node is IUAObject obj)
            return obj.ObjectType;

        if (node is IUAVariable var)
            return var.VariableType;

        return null;
    }

    public static IUANode GetSuperType(this IUANode node)
    {
        if (node is IUAObjectType objType)
            return objType.SuperType;

        if (node is IUAVariableType varType)
            return varType.SuperType;

        return null;
    }

    public static bool IsObjectTypeOrVariableType(this IUANode node)
    {
        return node.NodeClass == NodeClass.ObjectType || node.NodeClass == NodeClass.VariableType;
    }

    public static bool IsObjectOrVariable(this IUANode node)
    {
        return node.NodeClass == NodeClass.Object || node.NodeClass == NodeClass.Variable;
    }

    public static IUANode GetPrototype(this IUANode node)
    {
        if (node is IUAObject obj)
            return obj.Prototype;

        if (node is IUAVariable var)
            return var.Prototype;

        return null;
    }

    public static void SetPrototype(this IUANode node, IUANode prototype)
    {
        if (node is IUAObject obj)
            obj.Prototype = (IUAObject)prototype;
        else if (node is IUAVariable var)
            var.Prototype = (IUAVariable)prototype;
    }

    public static IReadOnlyList<IUANode> GetInstances(this IUANode node)
    {
        return node.InverseRefs.GetNodes(UAManagedCore.OpcUa.ReferenceTypes.HasTypeDefinition, false);
    }
}

public class ModellingRuleAnalyzer : ProjectAnalyzer
{
    public ModellingRuleAnalyzer(IContext context_)
    {
        context = context_;

        int tagImporterNamespaceIndex = context.GetNamespaceIndex("urn:FTOptix:TagImporter");
        if (tagImporterNamespaceIndex != NodeId.InvalidNamespaceIndex)
            tagImporterObjectType = context.GetObjectType(new NodeId(tagImporterNamespaceIndex, 1));
    }

    public void Verify()
    {
        nodesWithoutModellingRule = new List<IUANode>();

        var baseObjectType = context.GetObjectType(OpcUa.ObjectTypes.BaseObjectType);
        VisitType(baseObjectType);

        var baseVariableType = context.GetVariableType(OpcUa.VariableTypes.BaseVariableType);
        VisitType(baseVariableType);

        SerializeVerificationResult();
    }

    public bool HasSomethingToOptimize()
    {
        return nodesWithoutModellingRule.Count != 0;
    }

    public string GetAnalysisReport()
    {
        return stringBuilder.ToString();
    }

    public string AnalysisName()
    {
        return "Modelling Rules";
    }

    public void Optimize()
    {
        stringBuilder.Clear();
        foreach (var node in nodesWithoutModellingRule)
        {
            var modellingRule = FindModellingRule(node);
            node.ModellingRule = modellingRule;
            stringBuilder.AppendLine($"Set modelling rule {modellingRule} to node {Log.Node(node)}");
        }
    }

    public NamingRuleType FindModellingRule(IUANode node)
    {
        if (IsInputOrOutputArgument(node))
        {
            if (node.Owner.ModellingRule == NamingRuleType.Mandatory)
                return NamingRuleType.Mandatory;
        }

        var prototype = node.GetPrototype();
        if (prototype == null)
            return NamingRuleType.Mandatory;

        if (prototype.ModellingRule != NamingRuleType.None)
            return prototype.ModellingRule;

        var modellingRule = FindModellingRule(prototype);
        prototype.ModellingRule = modellingRule;
        stringBuilder.AppendLine($"Set modelling rule {modellingRule} to prototype {Log.Node(prototype)}");
        return modellingRule;
    }

    private void SerializeVerificationResult()
    {
        stringBuilder.Clear();

        stringBuilder.AppendLine($"Number of instance declarations without modelling rules {nodesWithoutModellingRule.Count}");

        foreach (var node in nodesWithoutModellingRule)
            stringBuilder.AppendLine($"Missing modelling rule on node {Log.Node(node)}");
    }

    private void VisitType(IUANode type)
    {
        var subtypes = type.Refs.GetNodes(OpcUa.ReferenceTypes.HasSubtype, false);
        var instanceDeclarations = type.Children.Except(subtypes);

        VerifyInstanceDeclarations(instanceDeclarations);

        foreach (var subtype in subtypes)
            VisitType(subtype);
    }

    private void VerifyInstanceDeclarations(IEnumerable<IUANode> instanceDeclarations)
    {
        foreach (var instanceDeclaration in instanceDeclarations)
            VerifyInstanceDeclaration(instanceDeclaration);
    }

    private void VerifyInstanceDeclaration(IUANode instanceDeclaration)
    {
        if (IsPairsOfConverter(instanceDeclaration))
            return;

        if (IsNamingRule(instanceDeclaration))
            return;

        if (IsTagImporter(instanceDeclaration))
            return;

        if (ShouldBeFixed(instanceDeclaration))
            nodesWithoutModellingRule.Add(instanceDeclaration);

        VerifyInstanceDeclarations(instanceDeclaration.Children);
    }

    private bool IsPairsOfConverter(IUANode instanceDeclaration)
    {
        if (instanceDeclaration.BrowseName != "Pairs")
            return false;

        var ownerNode = instanceDeclaration.Owner;

        if (ownerNode.NodeClass != NodeClass.Object &&
            ownerNode.NodeClass != NodeClass.ObjectType)
            return false;

        if (ownerNode.NodeClass == NodeClass.Object)
        {
            var owner = (IUAObject)ownerNode;
            if (!owner.IsInstanceOf(FTOptix.CoreBase.ObjectTypes.Converter))
                return false;
        }

        if (ownerNode.NodeClass == NodeClass.ObjectType)
        {
            var owner = (IUAObjectType)ownerNode;
            if (!owner.IsSubTypeOf(FTOptix.CoreBase.ObjectTypes.Converter))
                return false;
        }

        return true;
    }

    private bool IsInputOrOutputArgument(IUANode node)
    {
        if (node.QualifiedBrowseName.NamespaceIndex != 0)
            return false;

        return node.QualifiedBrowseName.Name == "OutputArguments" || node.QualifiedBrowseName.Name == "InputArguments";
    }

    private bool IsNamingRule(IUANode node)
    {
        return node.QualifiedBrowseName.NamespaceIndex == 2 && node.QualifiedBrowseName.Name == "NamingRule";
    }

    private bool IsTagImporter(IUANode node)
    {
        if (tagImporterObjectType == null)
            return false;

        if (node.NodeClass != NodeClass.Object)
            return false;

        var obj = (IUAObject)node;
        return obj.IsInstanceOf(tagImporterObjectType.NodeId);
    }

    private bool ShouldBeFixed(IUANode instanceDeclaration)
    {
        if (IsInputOrOutputArgument(instanceDeclaration) &&
            instanceDeclaration.ModellingRule == NamingRuleType.None &&
            instanceDeclaration.Owner.ModellingRule == NamingRuleType.Mandatory)
        {
            return true;
        }

        return instanceDeclaration.ModellingRule == NamingRuleType.None;
    }

    private List<IUANode> nodesWithoutModellingRule;
    private readonly IContext context;
    private readonly StringBuilder stringBuilder = new StringBuilder();
    private readonly IUAObjectType tagImporterObjectType;
}

public class PrototypeVerifier
{
    public string VerifyPrototypes(Dictionary<IUANode, IUANode> prototypesToMatch_)
    {
        stringBuilder.Clear();
        prototypesToMatch = prototypesToMatch_;
        numberOfPrototypeMismatch = 0;

        VerifyPrototypeRecursive(Project.Current);

        if (numberOfPrototypeMismatch > 0)
            stringBuilder.AppendLine($"Number of prototype mismatch: {numberOfPrototypeMismatch}");
        else
            stringBuilder.AppendLine("No prototype mismatch found");

        return stringBuilder.ToString();
    }

    public bool SomethingToOptimize()
    {
        return numberOfPrototypeMismatch != 0;
    }

    private void VerifyPrototypeRecursive(IUANode node)
    {
        if (node.IsObjectOrVariable())
            VerifyPrototype(node);

        foreach (var childNode in node.Children)
            VerifyPrototypeRecursive(childNode);
    }

    private void VerifyPrototype(IUANode node)
    {
        if (!prototypesToMatch.ContainsKey(node))
        {
            Log.Error($"Node not found in the computedPrototypes map ({Log.Node(node)})");
            return;
        }

        var computedPrototype = prototypesToMatch[node];
        var currentPrototype = node.GetPrototype();

        if (computedPrototype != currentPrototype)
        {
            ++numberOfPrototypeMismatch;

            stringBuilder.AppendLine($"Prototype mismatch {numberOfPrototypeMismatch}" +
                        $"\r\nnode                 {Log.Node(node)}" +
                        $"\r\ncomputed prototype   {(computedPrototype != null ? Log.Node(computedPrototype) : "null")}" +
                        $"\r\ncurrent prototype    {(currentPrototype != null ? Log.Node(currentPrototype) : "null")}");
        }
    }

    private readonly StringBuilder stringBuilder = new StringBuilder();
    private Dictionary<IUANode, IUANode> prototypesToMatch;
    private int numberOfPrototypeMismatch;
}

public class PrototypeOptimizer
{
    public void FixPrototypes(Dictionary<IUANode, IUANode> prototypesToApply)
    {
        foreach (var item in prototypesToApply)
            SetPrototype(item.Key, item.Value);
    }

    private void SetPrototype(IUANode node, IUANode prototype)
    {
        if (node is IUAObject obj)
            SetObjectPrototype(obj, prototype);
        else if (node is IUAVariable var)
            SetVariablePrototype(var, prototype);
    }

    private void SetObjectPrototype(IUAObject obj, IUANode prototype)
    {
        if (prototype == null)
        {
            obj.Prototype = null;
            return;
        }

        if (prototype is IUAObject prototypeObj)
            obj.Prototype = prototypeObj;
        else
            Log.Error($"Prototype node class mismatch" +
                      $"\r\nnode      {obj.NodeClass}\t{Log.Node(obj)}" +
                      $"\r\nprototype {prototype.NodeClass}\t{Log.Node(prototype)}");
    }

    private void SetVariablePrototype(IUAVariable var, IUANode prototype)
    {
        if (prototype == null)
        {
            var.Prototype = null;
            return;
        }

        if (prototype is IUAVariable prototypeVar)
            var.Prototype = prototypeVar;
        else
            Log.Error($"Prototype node class mismatch" +
                      $"\r\nnode      {var.NodeClass}\t{Log.Node(var)}" +
                      $"\r\nprototype {prototype.NodeClass}\t{Log.Node(prototype)}");
    }
}

public class PrototypeAnalyzer : ProjectAnalyzer
{
    public PrototypeAnalyzer()
    {
        var computer = new PrototypeComputer();
        computedPrototypes = computer.ComputePrototypes();

        verifier = new PrototypeVerifier();
        optimizer = new PrototypeOptimizer();
    }

    public string AnalysisName()
    {
        return "Inheritance graph";
    }

    public string GetAnalysisReport()
    {
        return report;
    }

    public void Optimize()
    {

        optimizer.FixPrototypes(computedPrototypes);
    }

    public bool HasSomethingToOptimize()
    {
        return verifier.SomethingToOptimize();
    }

    public void Verify()
    {
        report = verifier.VerifyPrototypes(computedPrototypes);
    }

    private string report;
    private readonly PrototypeVerifier verifier;
    private readonly PrototypeOptimizer optimizer;
    private readonly Dictionary<IUANode, IUANode> computedPrototypes;
}

public class DynamicLinkModeAnalyzer : ProjectAnalyzer
{
    public DynamicLinkModeAnalyzer(IContext context_)
    {
        context = context_;
    }

    public string AnalysisName()
    {
        return "Dynamic link modes";
    }

    public string GetAnalysisReport()
    {
        return report;
    }

    public void Optimize()
    {
        StringBuilder stringBuilder = new StringBuilder();
        Dictionary<IUAVariable, IUAVariable> modeVariablePrototypes = new Dictionary<IUAVariable, IUAVariable>();
        List<IUAVariable> unnecessaryModeVariables = new List<IUAVariable>();

        var dataBindType = context.GetVariableType(FTOptix.CoreBase.VariableTypes.DataBind);
        var dataBindInstances = dataBindType.InverseRefs.GetNodes(OpcUa.ReferenceTypes.HasTypeDefinition, false);

        foreach (var dataBindInstance in dataBindInstances)
        {
            var modeVariable = dataBindInstance.Get<IUAVariable>("Mode");
            if (modeVariable == null)
                continue;

            if (IsUnnecessaryModeVariable(modeVariable))
                unnecessaryModeVariables.Add(modeVariable);

            modeVariablePrototypes.Add(modeVariable, modeVariable.Prototype);
        }

        stringBuilder.AppendLine($"Number of unncessary Mode variables: {unnecessaryModeVariables.Count}");

        int prototypeChangeCount = 0;

        foreach (var dataBindInstance in dataBindInstances)
        {
            var modeVariable = dataBindInstance.Get<IUAVariable>("Mode");
            if (modeVariable == null)
                continue;

            if (unnecessaryModeVariables.Contains(modeVariable))
                continue;

            var currentPrototype = modeVariable.Prototype;

            while (true)
            {
                if (!unnecessaryModeVariables.Contains(currentPrototype))
                    break;

                currentPrototype = modeVariablePrototypes[currentPrototype];
            }

            if (modeVariable.Prototype != currentPrototype)
            {
                stringBuilder.AppendLine($"Change prototype {prototypeChangeCount}" +
                         $"\r\nmode variable   {Log.Node(modeVariable)}" +
                         $"\r\nold prototype   {(modeVariable.Prototype != null ? Log.Node(modeVariable.Prototype) : "null")}" +
                         $"\r\nnew prototype   {Log.Node(currentPrototype)}");

                modeVariable.Prototype = currentPrototype;
                ++prototypeChangeCount;
            }
        }

        stringBuilder.AppendLine($"Number of prototype changes: {prototypeChangeCount}");

        foreach (var modeVariable in unnecessaryModeVariables)
        {
            stringBuilder.AppendLine($"Mode variable removed: {Log.Node(modeVariable)}");
            modeVariable.Delete();
        }

        report = stringBuilder.ToString();
    }

    public void Verify()
    {
        somethingToOptimize = false;
        StringBuilder stringBuilder = new StringBuilder();
        var dataBindType = context.GetVariableType(FTOptix.CoreBase.VariableTypes.DataBind);
        var dataBindInstances = dataBindType.InverseRefs.GetNodes(OpcUa.ReferenceTypes.HasTypeDefinition, false);
        List<IUAVariable> unnecessaryModeVariables = new List<IUAVariable>();

        foreach (var dataBindInstance in dataBindInstances)
        {
            var modeVariable = dataBindInstance.Get<IUAVariable>("Mode");
            if (modeVariable == null)
                continue;

            if (IsUnnecessaryModeVariable(modeVariable))
            {
                if (!somethingToOptimize)
                    somethingToOptimize = true;

                unnecessaryModeVariables.Add(modeVariable);
            }
        }

        stringBuilder.AppendLine($"Number of unnecessary Mode variables: {unnecessaryModeVariables.Count}");
        stringBuilder.AppendLine("Unnecessary Mode variables:");
        foreach (var modeVariable in unnecessaryModeVariables)
            stringBuilder.AppendLine($"{Log.Node(modeVariable)}");

        report = stringBuilder.ToString();
    }

    public bool HasSomethingToOptimize()
    {
        return somethingToOptimize;
    }

    private bool IsUnnecessaryModeVariable(IUAVariable modeVariable)
    {
        if (modeVariable.Prototype == null)
            return false;

        var mode = (int)modeVariable.Value.Value;
        var prototypeMode = (int)modeVariable.Prototype.Value.Value;
        return mode == prototypeMode;
    }

    private bool somethingToOptimize = false;
    private readonly IContext context;
    private string report;
}

public class ReadWriteModeAnalyzer : ProjectAnalyzer
{
    public ReadWriteModeAnalyzer(IContext context_)
    {
        context = context_;
    }

    public string AnalysisName()
    {
        return "Read/write dynamic link and converter modes";
    }

    public string GetAnalysisReport()
    {
        return report;
    }

    public bool HasSomethingToOptimize()
    {
        return somethingToOptimize;
    }

    public void Optimize()
    {
        StringBuilder stringBuilder = new StringBuilder();
        List<IUAVariable> inconsistentModeVariables = new List<IUAVariable>();

        var converterType = context.GetObjectType(FTOptix.CoreBase.ObjectTypes.Converter);
        var converterInstances = GetInstancesOfTypeRecursive(converterType).OfType<IUAObject>();

        foreach (var converter in converterInstances)
        {
            var modeVariable = converter.Get<IUAVariable>("Mode");
            if (modeVariable == null)
                continue;

            if (IsInconsistentConverterReadWriteModeVariable(modeVariable, converter))
                inconsistentModeVariables.Add(modeVariable);
        }

        var dataBindType = context.GetVariableType(FTOptix.CoreBase.VariableTypes.DataBind);
        var dataBindInstances = dataBindType.InverseRefs.GetNodes(OpcUa.ReferenceTypes.HasTypeDefinition, false).OfType<IUAVariable>();

        foreach (var dataBind in dataBindInstances)
        {
            var modeVariable = dataBind.Get<IUAVariable>("Mode");
            if (modeVariable == null)
                continue;

            if (IsInconsistentDynamicLinkReadWriteModeVariable(modeVariable, dataBind))
                inconsistentModeVariables.Add(modeVariable);
        }

        stringBuilder.AppendLine($"Number of inconsistent read/write Mode variables: {inconsistentModeVariables.Count}");

        foreach (var modeVariable in inconsistentModeVariables)
        {
            stringBuilder.AppendLine($"Inconsistent Mode variable reset to read: {Log.Node(modeVariable)}");
            modeVariable.Value = (int)DynamicLinkMode.Read;
        }

        report = stringBuilder.ToString();
    }

    public void Verify()
    {
        somethingToOptimize = false;
        StringBuilder stringBuilder = new StringBuilder();
        List<IUAVariable> inconsistentModeVariables = new List<IUAVariable>();
        convertersWithModeChangedToRead = new HashSet<IUAObject>();

        var converterType = context.GetObjectType(FTOptix.CoreBase.ObjectTypes.Converter);
        var converterInstances = GetInstancesOfTypeRecursive(converterType).OfType<IUAObject>();

        foreach (var converter in converterInstances)
        {
            var modeVariable = converter.Get<IUAVariable>("Mode");
            if (modeVariable == null)
                continue;

            if (IsInconsistentConverterReadWriteModeVariable(modeVariable, converter))
            {
                if (!somethingToOptimize)
                    somethingToOptimize = true;

                inconsistentModeVariables.Add(modeVariable);
                convertersWithModeChangedToRead.Add(converter);
            }
        }

        var dataBindType = context.GetVariableType(FTOptix.CoreBase.VariableTypes.DataBind);
        var dataBindInstances = dataBindType.InverseRefs.GetNodes(OpcUa.ReferenceTypes.HasTypeDefinition, false).OfType<IUAVariable>();

        foreach (var dataBind in dataBindInstances)
        {
            var modeVariable = dataBind.Get<IUAVariable>("Mode");
            if (modeVariable == null)
                continue;

            if (IsInconsistentDynamicLinkReadWriteModeVariable(modeVariable, dataBind))
            {
                if (!somethingToOptimize)
                    somethingToOptimize = true;

                inconsistentModeVariables.Add(modeVariable);
            }
        }

        stringBuilder.AppendLine($"Number of inconsistent read/write Mode variables: {inconsistentModeVariables.Count}");
        stringBuilder.AppendLine("Inconsistent read/write Mode variables:");
        foreach (var modeVariable in inconsistentModeVariables)
            stringBuilder.AppendLine($"{Log.Node(modeVariable)}");

        report = stringBuilder.ToString();
    }

    private List<IUANode> GetInstancesOfTypeRecursive(IUANode typeNode)
    {
        var result = typeNode.InverseRefs.GetNodes(OpcUa.ReferenceTypes.HasTypeDefinition, false).ToList();

        var subtypes = typeNode.Refs.GetNodes(OpcUa.ReferenceTypes.HasSubtype, false);
        foreach (var subtype in subtypes)
            result.AddRange(GetInstancesOfTypeRecursive(subtype));

        return result;
    }

    private bool IsInconsistentDynamicLinkReadWriteModeVariable(IUAVariable modeVariable, IUAVariable dataBind)
    {
        var mode = (DynamicLinkMode)(int)modeVariable.Value.Value;
        if (mode != DynamicLinkMode.ReadWrite)
            return false;

        var parentVariable = dataBind.Owner as IUAVariable;
        if (parentVariable == null)
            return false;

        if (!IsConverterSource(parentVariable) && !IsConverterParameter(parentVariable))
            return false;

        var converter = (IUAObject)parentVariable.Owner;
        if (convertersWithModeChangedToRead.Contains(converter))
            return true;

        var converterMode = (DynamicLinkMode)(int)converter.GetOptionalVariableValue("Mode").Value;
        return converterMode == DynamicLinkMode.Read;
    }

    private bool IsConverterSource(IUAVariable variable)
    {
        return variable.InverseRefs.GetNode(FTOptix.CoreBase.ReferenceTypes.HasSource, false) != null;
    }

    private bool IsConverterParameter(IUAVariable variable)
    {
        return variable.InverseRefs.GetNode(FTOptix.CoreBase.ReferenceTypes.HasParameter, false) != null;
    }

    private bool IsInconsistentConverterReadWriteModeVariable(IUAVariable modeVariable, IUAObject converter)
    {
        var mode = (DynamicLinkMode)(int)modeVariable.Value.Value;
        if (mode != DynamicLinkMode.ReadWrite)
            return false;

        return !IsInvertibleConverter(converter);
    }

    private bool IsInvertibleConverter(IUAObject converter)
    {
        var sources = converter.Refs.GetNodes(FTOptix.CoreBase.ReferenceTypes.HasSource, false);
        if (sources.Count == 0 || sources.Count > 1)
            return false;

        if (converter.IsInstanceOf(FTOptix.CoreBase.ObjectTypes.EUConverter) ||
            converter.IsInstanceOf(FTOptix.CoreBase.ObjectTypes.LinearConverter))
        {
            return true;
        }

        if (converter.IsInstanceOf(FTOptix.CoreBase.ObjectTypes.ValueMapConverter))
            return false;

        if (converter.IsInstanceOf(FTOptix.CoreBase.ObjectTypes.StringFormatter))
            return true;

        if (converter.IsInstanceOf(FTOptix.CoreBase.ObjectTypes.ExpressionEvaluator))
            return true;

        return false;
    }

    private bool somethingToOptimize = false;
    private readonly IContext context;
    private string report;
    private HashSet<IUAObject> convertersWithModeChangedToRead;
}

#endregion
public class ProjectOptimizer : BaseNetLogic
{
    /*
     * Verifies whether all DataItemTemplates of DataGrids have the correct qualified name
     * The output is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Verify_1_DataItemTemplates()
    {
        try
        {
            var analyzer = new DataItemTemplateAnalyzer(LogicObject.Context);
            AnalyzeProject(analyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning(ex.Message);
        }
    }

    /*
     * Verifies whether all DataItemTemplates of DataGrids have the correct qualified name
     * The changelog is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Optimize_1_DataItemTemplates()
    {
        var analyzer = new DataItemTemplateAnalyzer(LogicObject.Context);
        AnalyzeProject(analyzer, AnalysisType.Optimize);
    }

    /*
     * Verifies if there are any duplicated properties in the project (same path and same BrowseName)
     * The output is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Verify_2_DuplicatedProperties()
    {
        try
        {
            var analyzer = new InstanceDeclarationAnalyzer(LogicObject.Context);
            AnalyzeProject(analyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning(ex.Message);
        }
    }

    /*
     * Optimizes the project by removing any duplicated property (same path and same BrowseName)
     * For each group of duplicated properties, only the first one is kept
     * The changelog is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Optimize_2_DuplicatedProperties()
    {
        var analyzer = new InstanceDeclarationAnalyzer(LogicObject.Context);
        AnalyzeProject(analyzer, AnalysisType.Optimize);
    }

    /*
     * Verifies whether there is any optional instance declaration inheriting a wrong value from its prototypes' chain
     * The output is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Verify_3_OptionalInstanceDeclarations()
    {
        try
        {
            var analyzer = new OptionalInstanceDeclarationAnalyzer();
            AnalyzeProject(analyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning(ex.Message);
        }
    }

    /*
     * Optimizes the project by materializing optional instance declarations inheriting a wrong value from their prototypes' chain
     * and by restoring their correct value
     * The changelog is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Optimize_3_OptionalInstanceDeclarations()
    {
        var analyzer = new OptionalInstanceDeclarationAnalyzer();
        AnalyzeProject(analyzer, AnalysisType.Optimize);
    }

    /*
     * Verifies whether there is any error in the inheritance graph (prototypes' chain)
     * The output is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Verify_4_InheritanceGraph()
    {
        try
        {
            var analyzer = new PrototypeAnalyzer();
            AnalyzeProject(analyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning(ex.Message);
        }
    }

    /*
     * Optimizes the project by restoring the correct inheritance hierarchy (prototypes' chain)
     * The changelog is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Optimize_4_InheritanceGraph()
    {
        var analyzer = new PrototypeAnalyzer();
        AnalyzeProject(analyzer, AnalysisType.Optimize);
    }

    /*
     * Verifies whether there is any instance declaration without modelling rule
     * The output is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Verify_5_ModellingRules()
    {
        try
        {
            var analyzer = new ModellingRuleAnalyzer(LogicObject.Context);
            AnalyzeProject(analyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning(ex.Message);
        }
    }

    /*
     * Optimizes the project by setting the modelling rule to instance declarations without it
     * To chose the right modelling rule the prototypes' chain is used
     * The changelog is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Optimize_5_ModellingRules()
    {
        var analyzer = new ModellingRuleAnalyzer(LogicObject.Context);
        AnalyzeProject(analyzer, AnalysisType.Optimize);
    }

    /*
     * Verifies whether there is any inconsistent read/write Mode property of a dynamic link or a converter
     * The output is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Verify_6_ReadWriteModes()
    {
        try
        {
            var analyzer = new ReadWriteModeAnalyzer(LogicObject.Context);
            AnalyzeProject(analyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning(ex.Message);
        }
    }

    /*
     * Optimizes the project by removing inconsistent read/write Mode properties of dynamic links and converters
     * The changelog is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Optimize_6_ReadWriteModes()
    {
        var analyzer = new ReadWriteModeAnalyzer(LogicObject.Context);
        AnalyzeProject(analyzer, AnalysisType.Optimize);
    }

    /*
     * Verifies whether there is any Mode property of a dynamic link that is unnecessary (i.e., could be safely removed)
     * The output is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Verify_7_DynamicLinkModes()
    {
        try
        {
            var analyzer = new DynamicLinkModeAnalyzer(LogicObject.Context);
            AnalyzeProject(analyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning(ex.Message);
        }
    }

    /*
     * Optimizes the project by removing unnecessary Mode properties of dynamic links
     * The changelog is written on OptimizationLogFilePath
     */
    [ExportMethod]
    public void Optimize_7_DynamicLinkModes()
    {
        var analyzer = new DynamicLinkModeAnalyzer(LogicObject.Context);
        AnalyzeProject(analyzer, AnalysisType.Optimize);
    }

    [ExportMethod]
    public void VerifyProject()
    {
        try
        {
            var dataItemTemplateAnalyzer = new DataItemTemplateAnalyzer(LogicObject.Context);
            AnalyzeProject(dataItemTemplateAnalyzer, AnalysisType.Verify);
            var instanceDeclarationAnalyzer = new InstanceDeclarationAnalyzer(LogicObject.Context);
            AnalyzeProject(instanceDeclarationAnalyzer, AnalysisType.Verify);
            var optionalInstanceDeclarationAnalyzer = new OptionalInstanceDeclarationAnalyzer();
            AnalyzeProject(optionalInstanceDeclarationAnalyzer, AnalysisType.Verify);
            var prototypeAnalyzer = new PrototypeAnalyzer();
            AnalyzeProject(prototypeAnalyzer, AnalysisType.Verify);
            var analyzer = new ModellingRuleAnalyzer(LogicObject.Context);
            AnalyzeProject(analyzer, AnalysisType.Verify);
            var readWriteModeAnalyzer = new ReadWriteModeAnalyzer(LogicObject.Context);
            AnalyzeProject(readWriteModeAnalyzer, AnalysisType.Verify);
            var dynamicLinkModeAnalyzer = new DynamicLinkModeAnalyzer(LogicObject.Context);
            AnalyzeProject(dynamicLinkModeAnalyzer, AnalysisType.Verify);
        }
        catch (VerificationException ex)
        {
            Log.Warning($"Project verification stopped: {ex.Message}");
        }
    }

    [ExportMethod]
    public void OptimizeProject()
    {
        var dataItemTemplateAnalyzer = new DataItemTemplateAnalyzer(LogicObject.Context);
        AnalyzeProject(dataItemTemplateAnalyzer, AnalysisType.Optimize);
        var instanceDeclarationAnalyzer = new InstanceDeclarationAnalyzer(LogicObject.Context);
        AnalyzeProject(instanceDeclarationAnalyzer, AnalysisType.Optimize);
        var optionalInstanceDeclarationAnalyzer = new OptionalInstanceDeclarationAnalyzer();
        AnalyzeProject(optionalInstanceDeclarationAnalyzer, AnalysisType.Optimize);
        var prototypeAnalyzer = new PrototypeAnalyzer();
        AnalyzeProject(prototypeAnalyzer, AnalysisType.Optimize);
        var modellingRuleAnalyzer = new ModellingRuleAnalyzer(LogicObject.Context);
        AnalyzeProject(modellingRuleAnalyzer, AnalysisType.Optimize);
        var readWriteModeAnalyzer = new ReadWriteModeAnalyzer(LogicObject.Context);
        AnalyzeProject(readWriteModeAnalyzer, AnalysisType.Optimize);
        var dynamicLinkModeAnalyzer = new DynamicLinkModeAnalyzer(LogicObject.Context);
        AnalyzeProject(dynamicLinkModeAnalyzer, AnalysisType.Optimize);
    }

    #region Internal functions

    private enum AnalysisType
    {
        Verify,
        Optimize
    }

    private string AnalysisTypeToString(AnalysisType analysisType)
    {
        switch (analysisType)
        {
            case AnalysisType.Verify:
                return "verification";
            case AnalysisType.Optimize:
                return "optimization";
            default:
                return "";
        }
    }

    [Serializable]
    private class VerificationException : Exception
    {
        public VerificationException(string message)
            : base(message)
        { }
    };

    private void AnalyzeProject(ProjectAnalyzer analyzer, AnalysisType analysisType)
    {
        if (!CheckOptimizationLogFile())
            return;

        var analysisName = analyzer.AnalysisName();
        Log.Info($"Starting {analysisName} {AnalysisTypeToString(analysisType)}...");

        analyzer.Verify();

        if (!analyzer.HasSomethingToOptimize())
        {
            Log.Info($"No optimizations needed for {analysisName}");
            return;
        }

        if (analysisType == AnalysisType.Optimize)
            analyzer.Optimize();

        var report = analyzer.GetAnalysisReport();
        AppendToReportFile(analysisName, analysisType, report);

        if (analysisType == AnalysisType.Verify)
            throw new VerificationException($"{analysisName} need optimizations");

        Log.Info($"{analysisName} {AnalysisTypeToString(analysisType)} ended.");
    }

    private string GetOptimizationLogFilePath()
    {
        var reportFilePathVariable = LogicObject.GetVariable("OptimizationLogFilePath");
        if (reportFilePathVariable == null)
            throw new Exception("OptimizationLogFilePath variable not found");

        return new ResourceUri(reportFilePathVariable.Value).Uri;
    }

    private bool CheckOptimizationLogFile()
    {
        var uri = GetOptimizationLogFilePath();
        if (string.IsNullOrEmpty(uri))
        {
            Log.Warning("OptimizationLogFilePath not specified");
            return false;
        }

        return true;
    }

    private void AppendToReportFile(string analysisName, AnalysisType analysisType, string report)
    {
        var uri = GetOptimizationLogFilePath();
        if (string.IsNullOrEmpty(uri))
        {
            Log.Warning("OptimizationLogFilePath not specified");
            return;
        }

        var streamWriter = new StreamWriter(uri, true, Encoding.UTF8);

        var analysisTypeName = AnalysisTypeToString(analysisType);

        streamWriter.WriteLine("--------------------------------------------------------------------------------");
        streamWriter.WriteLine($"{DateTime.Now} - Project: {Project.Current.BrowseName} - {analysisTypeName}: {analysisName}");
        streamWriter.WriteLine("--------------------------------------------------------------------------------");
        streamWriter.WriteLine(report);
        streamWriter.WriteLine("--------------------------------------------------------------------------------\r\n");
        streamWriter.Flush();
        streamWriter.Dispose();
        Log.Info($"Analysis log written to {uri}");
    }

    #endregion
}
