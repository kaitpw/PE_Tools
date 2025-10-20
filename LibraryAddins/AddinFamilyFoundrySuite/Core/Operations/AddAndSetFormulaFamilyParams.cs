// TODO: Migrate this!!!!!!!!!!

using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndSetFormulaFamilyParams : IOperation<AddAndSetFormulaFamilyParamsSettings> {
    public AddAndSetFormulaFamilyParamsSettings Settings { get; set; }

    // change this to type later probably after seeing if looping through the types isa ctually necessary
    public OperationType Type => OperationType.Doc;


    public string Description => "Add Family Parameters to the family";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog(this.GetType().Name);

        if (this.Settings.FamilyParamData is null || this.Settings.FamilyParamData.Any(p => p is null)) {
            log.Entries.Add(new LogEntry { Item = "Parameters", Error = "Invalid parameter data" });
            return log;
        }

        foreach (var p in this.Settings.FamilyParamData) {
            try {
                var parameter = doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
                // TODO: make this dependent on the p.DataType
                if (p.GlobalValue is not null && this.Settings.OverrideExistingValues)
                    doc.FamilyManager.SetFormula(parameter, $"\"{p.GlobalValue}\"");
                log.Entries.Add(new LogEntry { Item = p.Name });
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = p.Name, Error = ex.Message });
            }
        }

        return log;
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