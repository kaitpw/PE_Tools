// TODO: Migrate this!!!!!!!!!!

using Newtonsoft.Json;
using PeExtensions.FamDocument;
using PeServices.Storage.Core;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndGlobalSetFamilyParams(AddAndGlobalSetFamilyParamsSettings settings)
    : TypeOperation<AddAndGlobalSetFamilyParamsSettings>(settings) {
    public override string Description => "Add Family Parameters to the family";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new Dictionary<string, LogEntry>();

        var parametersToRetry = new List<(FamilyParameter, FamilyParamModel)>();
        var sortedParameters = this.Settings.FamilyParamData.OrderBy(p => p.Formula?.Length ?? 0);
        foreach (var p in sortedParameters) {
            FamilyParameter parameter = null;
            try {
                parameter = doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
                if (p.Formula is not null && parameter.Formula != p.Formula)
                    doc.FamilyManager.SetFormula(parameter, p.Formula);
                else if (p.GlobalValue is not null && this.Settings.OverrideExistingValues)
                    _ = doc.SetValue(parameter, p.GlobalValue);
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

public class AddAndGlobalSetFamilyParamsSettings : IOperationSettings {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    [Required]
    public bool OverrideExistingValues { get; init; } = true;

    public List<FamilyParamModel> FamilyParamData { get; init; } = [];
    public bool Enabled { get; init; } = true;
}

public record FamilyParamModel {
    [Required] public required string Name { get; init; }

    // make a JsonConverter for GroupTypeId later
    public ForgeTypeId PropertiesGroup { get; init; } = new("");

    [JsonConverter(typeof(ForgeTypeIdConverter))]
    [Required]
    public required ForgeTypeId DataType { get; init; }

    public bool IsInstance { get; init; } = true;
    public object GlobalValue { get; init; } = null;
    public string Formula { get; init; } = null;
}