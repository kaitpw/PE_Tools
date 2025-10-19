// TODO: Migrate this!!!!!!!!!!

using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndGlobalSetFamilyParams : IOperation<AddAndGlobalSetFamilyParamsSettings> {
    public AddAndGlobalSetFamilyParamsSettings Settings { get; set; }

    // change this to type later probably after seeing if looping through the types isa ctually necessary
    public OperationType Type => OperationType.Type;


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
                if (p.GlobalValue is not null && this.Settings.OverrideExistingValues)
                    _ = doc.SetValue(parameter, p.GlobalValue);
                log.Entries.Add(new LogEntry { Item = p.Name });
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = p.Name, Error = ex.Message });
            }
        }

        return log;
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
    public string Name { get; init; }
    public ForgeTypeId PropertiesGroup { get; init; } = new("");
    public ForgeTypeId DataType { get; init; }
    public bool IsInstance { get; init; } = true;
    public object GlobalValue { get; init; }
}