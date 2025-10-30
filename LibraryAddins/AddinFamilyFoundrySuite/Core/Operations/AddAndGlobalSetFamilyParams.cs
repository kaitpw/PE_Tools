// TODO: Migrate this!!!!!!!!!!

using Newtonsoft.Json;
using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndGlobalSetFamilyParams : TypeOperation<AddAndGlobalSetFamilyParamsSettings> {

    // change this to type later probably after seeing if looping through the types isa ctually necessary
    public override string Description => "Add Family Parameters to the family";

    public override OperationLog Execute(Document doc) {
        var logs = new List<LogEntry>();

        if (this.Settings.FamilyParamData is null || this.Settings.FamilyParamData.Any(p => p is null)) {
            logs.Add(new LogEntry { Item = "Parameters", Error = "Invalid parameter data" });
            return new OperationLog(this.Name, logs);
        }

        foreach (var p in this.Settings.FamilyParamData) {
            try {
                var parameter = doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
                if (p.Formula is not null)
                    doc.FamilyManager.SetFormula(parameter, p.Formula);
                else if (p.GlobalValue is not null && this.Settings.OverrideExistingValues)
                    _ = doc.SetValue(parameter, p.GlobalValue);
                logs.Add(new LogEntry { Item = p.Name });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = p.Name, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
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
    [Required] public string Name { get; init; }
    // make a JsonConverter for GroupTypeId later
    public ForgeTypeId PropertiesGroup { get; init; } = new("");
    [JsonConverter(typeof(ForgeTypeIdConverter))]
    [Required] public ForgeTypeId DataType { get; init; }
    public bool IsInstance { get; init; } = true;
    public object GlobalValue { get; init; } = null;
    public string Formula { get; init; } = null;
}