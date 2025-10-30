using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundryMigration : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            var processor = new OperationProcessor<ProfileRemap>(new Storage("FamilyFoundry"));
            using var tempFile = new TempSharedParamFile(doc);
            var apsParamData = processor.profile.GetAPSParams(tempFile);
            var apsParamNames = apsParamData.Select(p => p.externalDefinition.Name).ToList();
            var mappingDataAllNames = processor.profile.AddAndMapSharedParams.MappingData
                .Select(m => m.CurrName)
                .Concat(apsParamNames)
                .ToList();

            var addFamilyParams = new AddAndSetFormulaFamilyParamsSettings {
                FamilyParamData = [
                    new FamilyParamModel {
                        Name = "_FOUNDRY LAST PROCESSED AT",
                        PropertiesGroup = new ForgeTypeId(""),
                        DataType = SpecTypeId.String.Text,
                        IsInstance = false,
                        GlobalValue = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                ]
            };

            var queue = processor.CreateQueue()
                .Add(new DeleteUnusedParams(mappingDataAllNames), profile => profile.DeleteUnusedParams)
                .Add(new DeleteUnusedNestedFamilies(), profile => profile.DeleteUnusedNestedFamilies)
                .Add(new MapAndAddSharedParams(apsParamData), profile => profile.AddAndMapSharedParams)
                .Add(new HydrateElectricalConnector(), profile => profile.HydrateElectricalConnector)
                .Add(new DeleteUnusedParams(apsParamNames), profile => profile.DeleteUnusedParams)
                .Add(new DebugLogAnnoInfo(), new DebugLogAnnoInfoSettings())
                .Add(new AddAndSetFormulaFamilyParams(), addFamilyParams);

            var metadata = queue.GetOperationMetadata();
            foreach (var op in metadata)
                Debug.WriteLine($"[Batch {op.BatchGroup}] {op.Type}: {op.Name} - {op.Description}");

            var logs = processor.ProcessQueue(doc, queue);
            var balloon = new Ballogger();

            foreach (var log in logs) {
                var successCount = log.SuccessCount;
                var failedCount = log.FailedCount;
                var summary = $"{log.OperationName}: {successCount} succeeded, {failedCount} failed";

                _ = failedCount > 0
                    ? balloon.Add(Log.WARN, new StackFrame(), summary)
                    : balloon.Add(Log.INFO, new StackFrame(), summary);
            }

            balloon.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

public class DebugLogAnnoInfo : DocOperation<DebugLogAnnoInfoSettings> {
    public override string Description => "Log information about Generic Annotation family parameters";

    public override OperationLog Execute(Document doc) {
        var logs = new List<LogEntry>();

        try {
            var category = doc.OwnerFamily?.FamilyCategory;
            if (category == null) {
                logs.Add(new LogEntry { Item = "Family Category", Error = "Could not retrieve family category" });
                return new OperationLog(this.Name, logs);
            }

            var categoryName = category.Name;
            logs.Add(new LogEntry { Item = $"Category: {categoryName}" });

            if (categoryName != "Generic Annotations") {
                logs.Add(new LogEntry {
                    Item = "Category Check",
                    Error = $"Family is not a Generic Annotation (found: {categoryName})"
                });
                return new OperationLog(this.Name, logs);
            }

            var parameters = doc.FamilyManager.Parameters.OfType<FamilyParameter>().ToList();
            Debug.WriteLine($"Total Parameters: {parameters.Count}");

            foreach (var param in parameters) {
                var paramName = param.Definition.Name;
                var isInstance = param.IsInstance ? "Instance" : "Type";
                var dataType = param.Definition.GetDataType()?.TypeId ?? "Unknown";
                var formula = param.Formula ?? "(no formula)";
                var group = param.Definition.GetGroupTypeId()?.TypeId ?? "Unknown";
                var isBuiltIn = ParameterUtils.IsBuiltInParameter(param.Id) ? "Built-in" : "User";

                var paramInfo = $"{paramName} [{isBuiltIn}, {isInstance}, {dataType}, Group: {group}] = {formula}";
                Debug.WriteLine(paramInfo);
            }
        } catch (Exception ex) {
            logs.Add(new LogEntry { Item = "Operation", Error = ex.Message });
        }

        return new OperationLog(this.Name, logs);
    }
}

public class DebugLogAnnoInfoSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}

public class ProfileRemap : BaseProfileSettings {
    [Description("Settings for deleting unused parameters")]
    [Required]
    public DeleteUnusedParamsSettings DeleteUnusedParams { get; init; } = new();

    [Description("Settings for deleting unused nested families")]
    [Required]
    public DeleteUnusedNestedFamiliesSettings DeleteUnusedNestedFamilies { get; init; } = new();

    [Description("Settings for parameter mapping (add/replace and remap)")]
    [Required]
    public MapParamsSettings AddAndMapSharedParams { get; init; } = new();

    [Description("Settings for hydrating electrical connectors")]
    [Required]
    public HydrateElectricalConnectorSettings HydrateElectricalConnector { get; init; } = new();
}