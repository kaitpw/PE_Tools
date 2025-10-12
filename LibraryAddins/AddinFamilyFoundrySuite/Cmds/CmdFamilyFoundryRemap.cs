using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Ui;
using PeServices.Aps.Models;
using PeServices.Storage;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundryRemap : FamilyFoundryBase<SettingsRemap, ProfileRemap>, IExternalCommand {
    protected ParametersApi.Parameters _apsParams;

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            this.Init(() => this._apsParams = this._settings.GetAPSParams()
            );

            // Prepare settings for operations
            this._profile.AddApsParams.ApsParams = this._apsParams;

            var queue = new OperationEnqueuer(doc, this._profile)
                .Add(new AddApsParamsOperationTyped())
                .Add(new HydrateElectricalConnectorOperationTyped())
                .Add(new RemapParamsOperationTyped());

            // Get metadata for debugging/logging
            var metadata = queue.GetOperationMetadata();
            foreach (var op in metadata)
                Debug.WriteLine($"[Batch {op.BatchGroup}] {op.Type}: {op.Name} - {op.Description}");

            this.ProcessQueue(queue);

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

public class SettingsRemap : BaseSettings<ProfileRemap> {
    public ParametersApi.Parameters GetAPSParams() {
        var apsParams = Storage.GlobalState("parameters-service-cache.json").Json<ParametersApi.Parameters>().Read();
        if (apsParams.Results != null) return apsParams;

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }
}

public class ProfileRemap : BaseProfileSettings {
    // New typed operation settings (automatically discovered)
    [Description("Settings for adding APS parameters")]
    [Required]
    public AddApsParamsSettings AddApsParams { get; init; } = new();

    [Description("Settings for remapping parameters")]
    [Required]
    public RemapParamsSettings RemapParams { get; init; } = new();
}