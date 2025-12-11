using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class SetParamValueAsValue(AddFamilyParamsSettings settings, bool setOnly = true)
    : DocOperation<AddFamilyParamsSettings>(settings) {
    public readonly bool SetOnly = setOnly;

    public override string Description =>
        "Add Family Parameters and set the value for each family type to the same value.";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new Dictionary<string, LogEntry>();

        var sortedParameters = this.Settings.FamilyParamData.Where(p => p.GlobalValue is not null);
        foreach (var p in sortedParameters) {
            var parameter = this.SetOnly
                ? doc.FamilyManager.FindParameter(p.Name)
                : doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
            if (parameter is null) {
                logs[p.Name] = new LogEntry { Item = p.Name, Error = $"Parameter '{p.Name}' not found" };
                continue;
            }

            if (!this.Settings.OverrideExistingValues) {
                logs[p.Name] = new LogEntry { Item = p.Name };
                continue;
            }

            try {
                var param = doc.SetGlobalValue(parameter, p.GlobalValue);
                logs[p.Name] = param is not null
                    ? new LogEntry { Item = p.Name }
                    : new LogEntry { Item = p.Name, Error = $"Failed to set value '{p.Name}' to '{p.GlobalValue}'" };
            } catch (Exception ex) {
                logs[p.Name] = new LogEntry { Item = p.Name, Error = $"Failed to set value: {ex.Message}" };
            }
        }

        return new OperationLog(this.Name, logs.Values.ToList());
    }
}