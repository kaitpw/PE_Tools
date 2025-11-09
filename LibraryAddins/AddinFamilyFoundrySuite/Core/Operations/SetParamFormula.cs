using PeExtensions.FamDocument;
using PeExtensions.FamManager;
using AddinFamilyFoundrySuite.Core.OperationSettings;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class SetParamFormula(AddFamilyParamsSettings settings, bool setOnly = true)
    : DocOperation<AddFamilyParamsSettings>(settings) {
    public readonly bool SetOnly = setOnly;

    public override string Description => "Add Family Parameters and set their formula.";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new Dictionary<string, LogEntry>();

        var parametersToRetry = new List<(FamilyParameter, FamilyParamModel)>();
        var sortedParameters = this.Settings.FamilyParamData.OrderBy(p => p.Formula?.Length ?? 0);
        foreach (var p in sortedParameters) {
            FamilyParameter parameter = null;
            try {
                parameter = this.SetOnly
                    ? doc.FamilyManager.FindParameter(p.Name)
                    : doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
                if (parameter is null) {
                    logs[p.Name] = new LogEntry { Item = p.Name, Error = $"Parameter '{p.Name}' not found" };
                    continue;
                }
                if (p.Formula is not null && parameter.Formula != p.Formula && this.Settings.OverrideExistingValues)
                    doc.FamilyManager.SetFormula(parameter, p.Formula);
                logs[p.Name] = new LogEntry { Item = p.Name };
            } catch (Exception ex) {
                parametersToRetry.Add((parameter, p));
                logs[p.Name] = new LogEntry { Item = p.Name, Error = ex.Message };
            }
        }

        RetryFailedFormulas(doc, parametersToRetry, logs);

        return new OperationLog(this.Name, logs.Values.ToList());
    }

    private static void RetryFailedFormulas(FamilyDocument doc,
        List<(FamilyParameter, FamilyParamModel)> parametersToRetry,
        Dictionary<string, LogEntry> logs) {
        foreach (var (parameter, paramModel) in parametersToRetry) {
            try {
                if (parameter is not null
                    && paramModel.Formula is not null
                    && parameter.Formula != paramModel.Formula
                   ) doc.FamilyManager.SetFormula(parameter, paramModel.Formula);

                logs[paramModel.Name] = new LogEntry { Item = paramModel.Name };
            } catch (Exception ex) {
                logs[paramModel.Name] = new LogEntry { Item = paramModel.Name, Error = ex.Message };
            }
        }
    }
}