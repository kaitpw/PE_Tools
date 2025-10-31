using Newtonsoft.Json;
using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class LogFamilyParamsState : DocOperation<DefaultOperationSettings> {
    public LogFamilyParamsState(DefaultOperationSettings settings, string outputDir) : base(settings) => this.OutputPath = outputDir;
    public string OutputPath { get; }
    public override string Description => "Log the state of the family parameters to a JSON file";

    public override OperationLog Execute(Document doc) {
        var familyManager = doc.FamilyManager;
        var familyParamDataList = new List<FamilyParamModel>();

        foreach (FamilyParameter param in familyManager.Parameters) {
            var formula = param.Formula;
            var globalValue = string.IsNullOrEmpty(formula)
                ? doc.GetValue(param)
                : null;

            var familyParamData = new FamilyParamModel {
                Name = param.Definition.Name,
                PropertiesGroup = param.Definition.GetGroupTypeId(),
                DataType = param.Definition.GetDataType(),
                IsInstance = param.IsInstance,
                GlobalValue = globalValue,
                Formula = string.IsNullOrEmpty(formula) ? null : formula
            };

            familyParamDataList.Add(familyParamData);
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"family-params_{timestamp}.json";
        var filePath = Path.Combine(this.OutputPath, filename);

        var serializerSettings = new JsonSerializerSettings {
            Formatting = Formatting.Indented,
            Converters = new List<Newtonsoft.Json.JsonConverter> { new ForgeTypeIdConverter() }
        };

        var json = JsonConvert.SerializeObject(familyParamDataList, serializerSettings);
        File.WriteAllText(filePath, json);

        var log = new LogEntry { Item = $"Wrote {familyParamDataList.Count} parameters to {filename}" };
        return new OperationLog(this.Name, [log]);
    }
}