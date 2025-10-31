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
            using var processor = new OperationProcessor<ProfileFamilyManager>(doc, storage);
            // force this to never be singl transaction
            processor.Profile.ExecutionOptions.SingleTransaction = false;
            var apsParamData = processor.GetApsParams();

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
            var mode = processor.Profile.ExecutionOptions.Mode;
            var queue = mode switch {
                "snapshot" => processor.CreateQueue()
                    .Add(new LogFamilyParamsState(new(), processor.storage.Output().GetFolderPath()))
                    .Add(new LogRefPlaneAndDims(new(), processor.storage.Output().GetFolderPath())),
                _ => processor.CreateQueue()
                    .Add(new AddSharedParams(processor.Profile.AddSharedParams, apsParamData))
                    .Add(new MakeRefPlaneAndDims(processor.Profile.MakeRefPlaneAndDims))
                    .Add(new AddAndGlobalSetFamilyParams(processor.Profile.AddAndGlobalSetFamilyParams))
                    .Add(new AddAndSetFormulaFamilyParams(addFamilyParams))
            };
            var metadata = queue.GetOperationMetadata();
            foreach (var op in metadata)
                Debug.WriteLine($"[Batch {op.IsMerged}] {op.Type}: {op.Name} - {op.Description}");


            if (processor.Profile.ExecutionOptions.PreviewRun) {
                OperationLogger.OutputDryRunResults(processor, queue);
                return Result.Succeeded;
            }

            var logs = processor.ProcessQueue(queue);
            var logPath = OperationLogger.OutputProcessingResults(processor, logs.familyResults, logs.totalMs);
            var balloon = new Ballogger();

            foreach (var (famName, (_, ms)) in logs.familyResults) {
                _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {famName} in {ms}ms");
            }
            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

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
            Converters = new List<JsonConverter> { new ForgeTypeIdConverter() }
        };

        var json = JsonConvert.SerializeObject(familyParamDataList, serializerSettings);
        File.WriteAllText(filePath, json);

        var log = new LogEntry { Item = $"Wrote {familyParamDataList.Count} parameters to {filename}" };
        return new OperationLog(this.Name, [log]);
    }
}

public class ProfileFamilyManager : BaseProfileSettings {

    [Description("Settings for adding shared parameters")]
    [Required]
    public DefaultOperationSettings AddSharedParams { get; init; } = new();

    [Description("Settings for making reference planes and dimensions")]
    [Required]
    public MakeRefPlaneAndDimsSettings MakeRefPlaneAndDims { get; init; } = new();

    [Description("Settings for adding family parameters")]
    [Required]
    public AddAndGlobalSetFamilyParamsSettings AddAndGlobalSetFamilyParams { get; init; } = new();
}