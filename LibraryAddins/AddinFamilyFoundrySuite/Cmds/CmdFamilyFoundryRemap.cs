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
            var filter = this._profile.ParamsAddPS.Filter;

            // var hydratedRemapData = new List<(FamilyParameter oldParam, FamilyParameter newParam, string policy)>();

            this.Process(doc,
                famDoc => {
                    var familyName = famDoc.OwnerFamily?.Name ?? "Unknown";
                    Debug.WriteLine($"\nProcessing family: {familyName}");
                    Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                    Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                },
                famDoc => AddParams.ParamService(famDoc, this._apsParams, filter),
                famDoc => this.RemapParameters(famDoc, this._profile.ParamsRemap.RemapData));

            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), Log.ERR,
                    $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}")
                .Show();
            return Result.Cancelled;
        }
    }

    public List<Result<FamilyParameter>> RemapParameters(Document famDoc, List<ParamsRemap.RemapDataRecord> paramRemaps) {
        List<Result<FamilyParameter>> results = new();

        if (!famDoc.IsFamilyDocument)
            throw new Exception("Family document is null or not a family document");

        var fm = famDoc.FamilyManager;
        var familyTypes = fm.Types.Cast<FamilyType>().ToList(); // Evaluate once

        foreach (var famType in familyTypes) {
            fm.CurrentType = famType;
            foreach (var p in paramRemaps) {
                try {
                    results.Add(famDoc.MapValue(p.CurrNameOrId, p.NewNameOrId, p.MappingPolicy));

                } catch (Exception ex) {
                    results.Add(ex);
                }
            }
        }

        return results;
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