using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeRevit.Ui;
using PeServices.Aps.Models;
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
                FamilyParamData = [new FamilyParamDataRecord {
                    Name = "Test",
                    PropertiesGroup = new ForgeTypeId("General"),
                    DataType = SpecTypeId.,
                    IsInstance = true,
                    Value = "Test"
                }]
            };
            var processor = new OperationProcessor<ProfileRemap>(new Storage("FamilyFoundry"));

            var queue = processor.CreateQueue()
                .Add(new AddApsParamsOperationTyped(), profile => profile.AddApsParams)
                .Add(new HydrateElectricalConnectorOperationTyped(), profile => profile.HydrateElectricalConnector)
                .Add(new RemapParamsOperation(), profile => profile.RemapParams)
                .Add(new AddFamilyParamsOperation(), profile => profile.AddFamilyParams);

            // Get metadata for debugging/logging
            var metadata = queue.GetOperationMetadata();
            foreach (var op in metadata)
                Debug.WriteLine($"[Batch {op.BatchGroup}] {op.Type}: {op.Name} - {op.Description}");

            processor.ProcessQueue(doc, queue);

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

public class ProfileRemap : BaseProfileSettings {
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