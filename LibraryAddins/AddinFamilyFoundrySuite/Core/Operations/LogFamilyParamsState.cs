using Newtonsoft.Json;
using PeExtensions.FamDocument;
using PeServices.Storage.Core.Json.ContractResolvers;
using PeServices.Storage.Core.Json.Converters;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class LogFamilyParamsState(string outputDir) : DocOperation {
    public string OutputPath { get; } = outputDir;
    public override string Description => "Log the state of the family parameters to a JSON file";

    public override OperationLog Execute(FamilyDocument doc) {
        var familyManager = doc.FamilyManager;
        var familyParamDataList = new List<FamilyParamModel>();
        var famParams = familyManager.GetParameters().Where(p => !ParameterUtils.IsBuiltInParameter(p.Id)).ToList();

        foreach (var param in famParams) {
            var formula = param.Formula;


            var familyParamData = new FamilyParamModel {
                Name = param.Definition.Name,
                PropertiesGroup = param.Definition.GetGroupTypeId(),
                DataType = param.Definition.GetDataType(),
                IsInstance = param.IsInstance,
                Formula = string.IsNullOrEmpty(formula) ? null : formula
            };

            familyParamDataList.Add(familyParamData);
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"family-params_{timestamp}.json";
        var filePath = Path.Combine(this.OutputPath, filename);

        var defaultInstance = new FamilyParamModel { Name = "", DataType = new ForgeTypeId("") };

        var serializerSettings = new JsonSerializerSettings {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> { new ForgeTypeIdConverter() },
            ContractResolver = new DefaultValueSkippingContractResolver(defaultInstance)
        };

        var json = JsonConvert.SerializeObject(familyParamDataList, serializerSettings);
        File.WriteAllText(filePath, json);

        var log = new LogEntry { Item = $"Wrote {familyParamDataList.Count} parameters to {filename}" };
        return new OperationLog(this.Name, [log]);
    }
}