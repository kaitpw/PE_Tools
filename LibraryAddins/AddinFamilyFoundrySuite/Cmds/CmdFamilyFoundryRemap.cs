using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations.Doc;
using AddinFamilyFoundrySuite.Core.Operations.Type;
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
            this.Init(
                () => this._apsParams = this._settings.GetAPSParams()
            );

            var queue = new OperationEnqueuer(doc)
                .DocOperation(famDoc => {
                    var familyName = famDoc.OwnerFamily?.Name ?? "Unknown";
                    Debug.WriteLine($"\nProcessing family: {familyName}");
                    Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                    Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                })
                .DocOperation(famDoc => famDoc.AddApsParams(this._apsParams, this._profile.ParamsAddPS.Filter))
                .DocOperation(famDoc => famDoc.HydrateElectricalConnector())
                .TypeOperation(famDoc => famDoc.RemapParameters(this._profile.ParamsRemap.RemapData));

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
    [Description("Parameters adding settings")]
    [Required]
    public ParamsAddPS ParamsAddPS { get; init; } = new();

    [Description("Parameters remap settings")]
    [Required]
    public ParamsRemap ParamsRemap { get; init; } = new();
}