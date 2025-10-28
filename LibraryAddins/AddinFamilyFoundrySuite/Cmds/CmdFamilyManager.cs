using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using Newtonsoft.Json;
using PeExtensions.FamDocument;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFamilyManager : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            var storage = new Storage("FF Manager");
            var processor = new OperationProcessor<ProfileFamilyManager>(storage);
            using var tempFile = new TempSharedParamFile(doc);
            var apsParamData = processor.profile.GetAPSParams(tempFile);
            var apsParamNames = apsParamData.Select(p => p.externalDefinition.Name).ToList();

            var addFamilyParams = new AddAndSetFormulaFamilyParamsSettings {
                FamilyParamData = [
                    new FamilyParamModel {
                        Name = "_FOUNDRY LAST PROCESSED AT",
                        PropertiesGroup = new ForgeTypeId(""),
                        DataType = SpecTypeId.String.Text,
                        IsInstance = false,
                        GlobalValue = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                ]
            };

            var printLog = storage.Settings().Json<BaseSettings<ProfileFamilyManager>>().Read().DryRun;

            var queue = processor.CreateQueue()
                .Add(new LogFamilyParamsState(storage.Output().GetFolderPath(), printLog),
                    new LogFamilyParamsStateSettings())
                .Add(new AddSharedParams(apsParamData), profile => profile.AddSharedParams)
                .Add(new AddAndGlobalSetFamilyParams(), profile => profile.AddAndGlobalSetFamilyParams)
                .Add(new AddAndSetFormulaFamilyParams(), addFamilyParams);

            var metadata = queue.GetOperationMetadata();
            foreach (var op in metadata)
                Debug.WriteLine($"[Batch {op.BatchGroup}] {op.Type}: {op.Name} - {op.Description}");

            var logs = processor.ProcessQueue(doc, queue);
            var balloon = new Ballogger();

            foreach (var log in logs) {
                var successCount = log.SuccessCount;
                var failedCount = log.FailedCount;
                var summary = $"{log.OperationName}: {successCount} succeeded, {failedCount} failed";

                _ = failedCount > 0
                    ? balloon.Add(Log.WARN, new StackFrame(), summary)
                    : balloon.Add(Log.INFO, new StackFrame(), summary);
            }

            balloon.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

public class LogFamilyParamsState : IOperation<LogFamilyParamsStateSettings> {
    public LogFamilyParamsState(string outputDir, bool enable = false) {
        this.OutputPath = outputDir;
        this._enable = enable;
    }

    private bool _enable { get; }

    public string OutputPath { get; }

    public LogFamilyParamsStateSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name { get; set; }
    public string Description => "Log the state of the family parameters to a JSON file";

    public OperationLog Execute(Document doc) {
        if (!this._enable) return new OperationLog(this.Name, []);
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
            Formatting = Formatting.Indented, Converters = new List<JsonConverter> { new ForgeTypeIdConverter() }
        };

        var json = JsonConvert.SerializeObject(familyParamDataList, serializerSettings);
        File.WriteAllText(filePath, json);

        var log = new LogEntry { Item = $"Wrote {familyParamDataList.Count} parameters to {filename}" };
        return new OperationLog(this.Name, [log]);
    }
}

public class LogFamilyParamsStateSettings : IOperationSettings {
    public bool Enabled { get; init; }
}

public class ProfileFamilyManager : BaseProfileSettings {
    [Description("Settings for adding shared parameters")]
    [Required]
    public AddSharedParamsSettings AddSharedParams { get; init; } = new();

    [Description("Settings for adding family parameters")]
    [Required]
    public AddAndGlobalSetFamilyParamsSettings AddAndGlobalSetFamilyParams { get; init; } = new();
}