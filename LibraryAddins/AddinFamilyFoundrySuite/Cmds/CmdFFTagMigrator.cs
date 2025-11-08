using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeExtensions.FamDocument;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFFTagMigrator : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            var storage = new Storage("FF Migrator");
            var settingsManager = storage.SettingsDir();
            var settings = settingsManager.Json<BaseSettings<TagMigratorProfile>>().Read();
            var profile = settingsManager.SubDir("profiles").Json<TagMigratorProfile>($"{settings.CurrentProfile}.json")
                .Read();
            var outputFolderPath = storage.OutputDir().DirectoryPath;

            using var tempFile = new TempSharedParamFile(doc);
            var apsParamData = profile.GetAPSParams(tempFile);
            var families = profile.GetFamilies(doc);

            using var processor = new OperationProcessor(
                doc,
                profile.ExecutionOptions);
            var apsParamNames = apsParamData.Select(p => p.externalDefinition.Name).ToList();
            var mappingDataAllNames = profile.AddAndMapSharedParams.MappingData
                .Select(m => m.CurrName)
                .Concat(apsParamNames)
                .ToList();

            var addFamilyParamsSettings = new AddFamilyParamsSettings {
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

            var queue = new OperationQueue()
                .Add(new DeleteUnusedParams(profile.DeleteUnusedParams, mappingDataAllNames))
                .Add(new DeleteUnusedNestedFamilies(profile.DeleteUnusedNestedFamilies))
                .Add(new MapAndAddSharedParams(profile.AddAndMapSharedParams, apsParamData))
                .Add(new DebugLogAnnoInfo())
                .Add(new AddAndSetValueAsFormula(addFamilyParamsSettings));

            var metadataString = queue.GetExecutableMetadataString();
            Debug.WriteLine(metadataString);


            if (profile.ExecutionOptions.PreviewRun) {
                OperationLogger.OutputDryRunResults(
                    apsParamData,
                    doc,
                    queue,
                    profile.GetFamilies,
                    storage,
                    settings.CurrentProfile,
                    settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);
            } else {
                var uiDoc = commandData.Application.ActiveUIDocument;
                var logs = processor
                    .SelectFamilies(() => !doc.IsFamilyDocument
                        ? Pickers.GetSelectedFamilies(uiDoc) ?? profile.GetFamilies(doc)
                        : null
                    )
                    .ProcessQueue(queue, outputFolderPath, settings.OnProcessingFinish);
                var logPath = OperationLogger.OutputProcessingResults(
                    logs.familyResults,
                    logs.totalMs,
                    storage,
                    settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);
                var balloon = new Ballogger();

                foreach (var output in logs.familyResults)
                    _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {output.familyName} in {output.totalMs}ms");
                balloon.Show();
            }

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

public class DebugLogAnnoInfo : DocOperation {
    public override string Description => "Log information about Generic Annotation family parameters";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        try {
            var category = doc.OwnerFamily?.FamilyCategory;
            if (category == null) {
                logs.Add(new LogEntry { Item = "Family Category", Error = "Could not retrieve family category" });
                return new OperationLog(this.Name, logs);
            }

            var categoryName = category.Name;
            logs.Add(new LogEntry { Item = $"Category: {categoryName}" });

            if (categoryName != "Generic Annotations") {
                logs.Add(new LogEntry {
                    Item = "Category Check", Error = $"Family is not a Generic Annotation (found: {categoryName})"
                });
                return new OperationLog(this.Name, logs);
            }

            var parameters = doc.FamilyManager.Parameters.OfType<FamilyParameter>().ToList();
            Debug.WriteLine($"Total Parameters: {parameters.Count}");

            foreach (var param in parameters) {
                var paramName = param.Definition.Name;
                var isInstance = param.IsInstance ? "Instance" : "Type";
                var dataType = param.Definition.GetDataType()?.TypeId ?? "Unknown";
                var formula = param.Formula ?? "(no formula)";
                var group = param.Definition.GetGroupTypeId()?.TypeId ?? "Unknown";
                var isBuiltIn = ParameterUtils.IsBuiltInParameter(param.Id) ? "Built-in" : "User";

                var paramInfo = $"{paramName} [{isBuiltIn}, {isInstance}, {dataType}, Group: {group}] = {formula}";
                Debug.WriteLine(paramInfo);
            }
        } catch (Exception ex) {
            logs.Add(new LogEntry { Item = "Operation", Error = ex.Message });
        }

        return new OperationLog(this.Name, logs);
    }
}

public class TagMigratorProfile : BaseProfileSettings {
    [Description("Settings for deleting unused parameters")]
    [Required]
    public DeleteUnusedParamsSettings DeleteUnusedParams { get; init; } = new();

    [Description("Settings for deleting unused nested families")]
    [Required]
    public DefaultOperationSettings DeleteUnusedNestedFamilies { get; init; } = new();

    [Description("Settings for parameter mapping (add/replace and remap)")]
    [Required]
    public MapParamsSettings AddAndMapSharedParams { get; init; } = new();
}