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
            var options = new LoadAndSaveOptionsClass();

            this.Init(
                options,
                () => {
                    var tmpParams = this._settings.GetAPSParams();
                    if (tmpParams.Results == null) {
                        throw new InvalidOperationException(
                            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
                            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
                    }

                    this._apsParams = tmpParams;
                });

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

public class LoadAndSaveOptionsClass : ILoadAndSaveOptions {
    /// <summary>
    ///     Load the family into the main model document
    /// </summary>
    public bool LoadFamily { get; set; } = true;

    /// <summary>
    ///     Save the family to the internal path of the family document
    /// </summary>
    public bool SaveFamilyToInternalPath { get; set; } = false;

    /// <summary>
    ///     Save the family to the output directory of the command
    /// </summary>
    public bool SaveFamilyToOutputDir { get; set; } = false;
}

public class SettingsRemap : BaseSettings<ProfileRemap> {
    public ParametersApi.Parameters GetAPSParams() =>
        Storage.GlobalState("parameters-service-cache.json").Json<ParametersApi.Parameters>().Read();
}

public class ProfileRemap : BaseProfileSettings {
    [Description("Parameters adding settings")]
    [Required]
    public ParamsAddPS ParamsAddPS { get; init; } = new();

    [Description("Parameters remap settings")]
    [Required]
    public ParamsRemap ParamsRemap { get; init; } = new();
}