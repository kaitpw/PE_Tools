using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeRevit.Ui;
using PeServices.Storage;
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
            var addFamilyParams = new AddAndGlobalSetFamilyParamsSettings {
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

            var processor = new OperationProcessor<ProfileRemap>(new Storage("FamilyFoundry"));
            var excludeFromDeletion = processor.profile.AddAndMapApsParams.MappingData
                .Select(m => m.CurrName)
                .Concat(processor.profile.GetAPSParams().Select(p => p.Name))
                .ToList();

            var queue = processor.CreateQueue()
                .Add(new DeleteUnusedParams(excludeFromDeletion), profile => profile.DeleteUnusedParams)
                .Add(new DeleteUnusedNestedFamilies(), profile => profile.DeleteUnusedNestedFamilies)
                .Add(new MapAndAddApsParams(processor.profile.GetAPSParams()), profile => profile.AddAndMapApsParams)
                .Add(new AddAndGlobalSetFamilyParams(), addFamilyParams)
                .Add(new HydrateElectricalConnector(), profile => profile.HydrateElectricalConnector);

            // Get metadata for debugging/logging
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

public class ProfileRemap : BaseProfileSettings {
    [Description("Settings for deleting unused parameters")]
    [Required]
    public DeleteUnusedParamsSettings DeleteUnusedParams { get; init; } = new();

    [Description("Settings for deleting unused nested families")]
    [Required]
    public DeleteUnusedNestedFamiliesSettings DeleteUnusedNestedFamilies { get; init; } = new();

    [Description("Settings for parameter mapping (add/replace and remap)")]
    [Required]
    public MapParamsSettings AddAndMapApsParams { get; init; } = new();

    [Description("Settings for hydrating electrical connectors")]
    [Required]
    public HydrateElectricalConnectorSettings HydrateElectricalConnector { get; init; } = new();
}