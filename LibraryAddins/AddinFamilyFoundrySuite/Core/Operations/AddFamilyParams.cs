using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddFamilyParams(AddFamilyParamsSettings settings)
    : DocOperation<AddFamilyParamsSettings>(settings) {
    public override string Description =>
        "Add Family Parameters and set the value for each family type to the same value.";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        foreach (var p in this.Settings.FamilyParamData) {
            try {
                _ = doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
                logs.Add(new LogEntry { Item = $"{p.Name}" });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = $"{p.Name}", Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}