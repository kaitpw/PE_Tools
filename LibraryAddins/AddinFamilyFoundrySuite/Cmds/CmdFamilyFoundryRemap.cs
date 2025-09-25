using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Families;
using PeRevit.Ui;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundryRemap : FamilyFoundryBase<SettingsRemap, ProfileRemap>, IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            // New fluent API - batches type operations into a single optimized loop
            var queue = this.EnqueueOperations(doc)
                .DocOperation(famDoc => {
                    var familyName = famDoc.OwnerFamily?.Name ?? "Unknown";
                    Debug.WriteLine($"\nProcessing family: {familyName}");
                    Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                    Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                })
                .DocOperation(famDoc => AddParams.ParamService(famDoc, this._apsParams, this._profile.ParamsAddPS.Filter))
                .TypeOperation((famDoc) => this.RemapParameters(famDoc, this._profile.ParamsRemap.RemapData));

            this.ProcessQueue(queue);

            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), Log.ERR,
                    $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}")
                .Show();
            return Result.Cancelled;
        }
    }


    /// <summary>
    /// Per-type remap method for use with the new fluent API
    /// </summary>
    private void RemapParameters(Document famDoc, List<ParamsRemap.RemapDataRecord> paramRemaps) {
        foreach (var p in paramRemaps) {
            try {
                _ = famDoc.MapValue(p.CurrNameOrId, p.NewNameOrId, p.MappingPolicy);
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }
    }

    // get all family parameters
    public List<FamilyParameter> GetFamilyParameters(Document famDoc) {
        var fm = famDoc.FamilyManager;
        var parameterList = new List<FamilyParameter>();
        foreach (FamilyParameter param in fm.Parameters) parameterList.Add(param);
        return parameterList;
    }
}

public class SettingsRemap : BaseSettings<ProfileRemap> {
}

public class ProfileRemap : BaseProfileSettings {
    [Description("Parameters adding settings")]
    [Required]
    public ParamsAddPS ParamsAddPS { get; init; } = new();

    [Description("Parameters remap settings")]
    [Required]
    public ParamsRemap ParamsRemap { get; init; } = new();
}