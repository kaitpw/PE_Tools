// TODO: Migrate this!!!!!!!!!!

using PeExtensions.FamDocument;
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamManager;
using AddinFamilyFoundrySuite.Core.OperationSettings;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class SetParamValueAsValue(AddFamilyParamsSettings settings)
    : TypeOperation<AddFamilyParamsSettings>(settings) {
    public override string Description =>
        "Add Family Parameters and set the value for each family type to the same value.";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new Dictionary<string, LogEntry>();

        var sortedParameters = this.Settings.FamilyParamData.Where(p => p.GlobalValue is not null);
        foreach (var p in sortedParameters) {
            var parameter = doc.FamilyManager.FindParameter(p.Name);
            if (parameter is null) {
                logs[p.Name] = new LogEntry { Item = p.Name, Error = $"Parameter '{p.Name}' not found" };
                continue;
            }

            if (this.Settings.OverrideExistingValues)
                _ = doc.SetValue(parameter, p.GlobalValue, ValueCoercionStrategy.CoerceSimple);
            logs[p.Name] = new LogEntry { Item = p.Name };
        }

        return new OperationLog(this.Name, logs.Values.ToList());
    }
}