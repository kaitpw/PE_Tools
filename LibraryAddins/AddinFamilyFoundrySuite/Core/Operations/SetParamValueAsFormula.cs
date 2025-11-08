// TODO: Migrate this!!!!!!!!!!

using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class SetParamValueAsFormula(AddFamilyParamsSettings settings)
    : DocOperation<AddFamilyParamsSettings>(settings) {
    // change this to type later probably after seeing if looping through the types is actually necessary
    public override string Description => "Add Family Parameters and set the value by a formula." +
                                          $"\nPro: Faster than {nameof(SetParamValueAsValue)} which sets a param's value per family type" +
                                          "\nCon: Formulas can only be changed by Editing a family";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        if (this.Settings.FamilyParamData is null || this.Settings.FamilyParamData.Any(p => p is null)) {
            logs.Add(new LogEntry { Item = "Parameters", Error = "Invalid parameter data" });
            return new OperationLog(this.Name, logs);
        }

        foreach (var p in this.Settings.FamilyParamData) {
            try {
                var parameter = doc.FamilyManager.FindParameter(p.Name);
                if (parameter is null) {
                    logs.Add(new LogEntry { Item = p.Name, Error = $"Parameter '{p.Name}' not found" });
                    continue;
                }                // TODO: make this dependent on the p.DataType
                if (this.Settings.OverrideExistingValues)
                    doc.FamilyManager.SetFormula(parameter, $"\"{p.GlobalValue}\"");
                logs.Add(new LogEntry { Item = p.Name });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = p.Name, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}