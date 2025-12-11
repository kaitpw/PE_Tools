using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Creates missing family parameters from AddAndSetParamsSettings.
///     Uses the optional PropertiesGroup/DataType/IsInstance from SetParamModel and SetParamPerTypeModel.
///     Only used when CreateIfMissing=true in the settings.
/// </summary>
public class AddParamsFromSettings(AddAndSetParamsSettings settings)
    : DocOperation<AddAndSetParamsSettings>(settings) {
    public override string Description =>
        "Create missing family parameters from AddAndSetParams settings.";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        var fm = doc.FamilyManager;

        // Get all unique parameter names that need to be created
        var paramsToCreate = new Dictionary<string, (ForgeTypeId group, ForgeTypeId dataType, bool isInstance)>();

        // From Parameters list
        foreach (var p in this.Settings.Parameters) {
            if (fm.FindParameter(p.Name) is not null) continue; // Already exists
            paramsToCreate[p.Name] = (p.PropertiesGroup, p.DataType, p.IsInstance);
        }

        // From PerTypeParameters list
        foreach (var p in this.Settings.ParametersPerType) {
            if (fm.FindParameter(p.Name) is not null) continue; // Already exists
            if (paramsToCreate.ContainsKey(p.Name)) continue; // Already in list

            paramsToCreate[p.Name] = (p.PropertiesGroup, p.DataType, p.IsInstance);
        }

        // Create the parameters
        foreach (var (name, (group, dataType, isInstance)) in paramsToCreate) {
            try {
                _ = doc.AddFamilyParameter(name, group, dataType, isInstance);
                logs.Add(new LogEntry { Item = name });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = name, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}

