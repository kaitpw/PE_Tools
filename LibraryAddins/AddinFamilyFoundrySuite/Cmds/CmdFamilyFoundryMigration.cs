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
            var addFamilyParams = new AddFamilyParamsSettings {
                FamilyParamData = [
                    new FamilyParamDataRecord {
                        Name = "_DATE LAST PROCESSED",
                        PropertiesGroup = GroupTypeId.General,
                        DataType = SpecTypeId.String.Text,
                        IsInstance = false,
                        Value = DateTime.Now.ToString("yyyy-MM-dd")
                    }
                ]
            };

            var processor = new OperationProcessor<ProfileRemap>(new Storage("FamilyFoundry"));

            var queue = processor.CreateQueue()
                .Add(new DeleteUnusedParamsOperation(), profile => profile.DeleteUnusedParams)
                .Add(new AddApsParamsOperationTyped(), profile => profile.AddApsParams)
                .Add(new HydrateElectricalConnectorOperationTyped(), profile => profile.HydrateElectricalConnector)
                .Add(new RemapParamsOperation(), profile => profile.RemapParams)
                .Add(new AddFamilyParamsOperation(), addFamilyParams);

            // Get metadata for debugging/logging
            var metadata = queue.GetOperationMetadata();
            foreach (var op in metadata)
                Debug.WriteLine($"[Batch {op.BatchGroup}] {op.Type}: {op.Name} - {op.Description}");

            var results = processor.ProcessQueue(doc, queue);
            var balloon = new Ballogger();
            foreach (var result in results) {
                _ = result.Error is not null
                    ? balloon.Add(Log.INFO, new StackFrame(), result.Error, true)
                    : balloon.Add(Log.INFO, new StackFrame(), result.Name);
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

    [Description("Settings for adding APS parameters")]
    [Required]
    public AddApsParamsSettings AddApsParams { get; init; } = new();

    [Description("Settings for hydrating electrical connectors")]
    [Required]
    public HydrateElectricalConnectorSettings HydrateElectricalConnector { get; init; } = new();

    [Description("Settings for remapping parameters")]
    [Required]
    public RemapParamsSettings RemapParams { get; init; } = new();
}