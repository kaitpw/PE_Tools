using AddinFamilyFoundrySuite.Core.Settings;
using AddinFamilyFoundrySuite.Core;
using PeRevit.Families;
using PeRevit.Ui;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundryAdd : FamilyFoundryBase<SettingsAdd, ProfileAdd>, IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {

            this.Process(doc,
                famDoc => {
                    var familyName = famDoc.OwnerFamily?.Name ?? "Unknown";
                    Debug.WriteLine($"\nProcessing family: {familyName}");
                    Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                    Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                },
                famDoc => AddParams.ParamService(famDoc, this._apsParams, this._profile.ParamsAddPS.Filter));

            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), Log.ERR,
                    $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}")
                .Show();
            return Result.Cancelled;
        }
    }
}

public class SettingsAdd : BaseSettings<ProfileAdd> {
    // No additional settings needed for Add variant
}

public class ProfileAdd : BaseProfileSettings {
    [Description("Parameters adding settings")]
    [Required] public ParamsAddPS ParamsAddPS { get; init; } = new();
}
