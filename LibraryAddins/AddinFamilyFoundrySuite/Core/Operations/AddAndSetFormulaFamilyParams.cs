// TODO: Migrate this!!!!!!!!!!

using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndSetFormulaFamilyParams(AddAndSetFormulaFamilyParamsSettings settings)
    : DocOperation<AddAndSetFormulaFamilyParamsSettings>(settings) {
    // change this to type later probably after seeing if looping through the types isa ctually necessary
    public override string Description => "Add Family Parameters to the family";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        if (this.Settings.FamilyParamData is null || this.Settings.FamilyParamData.Any(p => p is null)) {
            logs.Add(new LogEntry { Item = "Parameters", Error = "Invalid parameter data" });
            return new OperationLog(this.Name, logs);
        }

        foreach (var p in this.Settings.FamilyParamData) {
            try {
                var parameter = doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
                // TODO: make this dependent on the p.DataType
                if (p.GlobalValue is not null && this.Settings.OverrideExistingValues)
                    doc.FamilyManager.SetFormula(parameter, $"\"{p.GlobalValue}\"");
                logs.Add(new LogEntry { Item = p.Name });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = p.Name, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}

public class AddAndSetFormulaFamilyParamsSettings : IOperationSettings {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    [Required]
    public bool OverrideExistingValues { get; init; } = true;

    public List<FamilyParamModel> FamilyParamData { get; init; } = [];
    public bool Enabled { get; init; } = true;
}