using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Aggregators;
using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using AddinFamilyFoundrySuite.Core.Snapshots;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFFManager : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSetf
    ) {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("FF Manager");
            var settingsManager = storage.SettingsDir();
            var settings = settingsManager.Json<BaseSettings<ProfileFamilyManager>>().Read();
            // TODO: Add palette UI for profile selection like CmdFFMigrator
            var profile = settingsManager.SubDir("profiles")
                .Json<ProfileFamilyManager>("Default.json").Read();
            var outputFolderPath = storage.OutputDir().DirectoryPath;

            using var tempFile = new TempSharedParamFile(doc);
            var apsParamModels = profile.GetFilteredApsParamModels();

            // Build queue structure for preview (using temp file just for structure, not storing definitions)
            using var previewTempFile = new TempSharedParamFile(doc);
            var apsParamData = BaseProfileSettings.ConvertToSharedParameterDefinitions(
                apsParamModels, previewTempFile);


            var specs = new List<RefPlaneSubcategorySpec> {
                new() { Strength = RpStrength.NotARef, Name = "NotARef", Color = new Color(211, 211, 211) },
                new() { Strength = RpStrength.WeakRef, Name = "WeakRef", Color = new Color(217, 124, 0) },
                new() { Strength = RpStrength.StrongRef, Name = "StrongRef", Color = new Color(255, 0, 0) },
                new() { Strength = RpStrength.CenterLR, Name = "Center", Color = new Color(115, 0, 253) },
                new() { Strength = RpStrength.CenterFB, Name = "Center", Color = new Color(115, 0, 253) }
            };

            // Convert legacy AddFamilyParamsSettings to AddAndSetParamsSettings
            var addAndSetParamsSettings = new AddAndSetParamsSettings {
                OverrideExistingValues = profile.AddFamilyParams.OverrideExistingValues,
                CreateFamParamIfMissing = true,
                Parameters = profile.AddFamilyParams.FamilyParamData
                    .Select(p => new SetParamModel {
                        Name = p.Name,
                        ValueOrFormula = p.GlobalValue?.ToString() ?? p.Formula,
                        PropertiesGroup = p.PropertiesGroup,
                        DataType = p.DataType,
                        IsInstance = p.IsInstance,
                        SetAsFormula = p.GlobalValue == null && !string.IsNullOrWhiteSpace(p.Formula)
                    })
                    .ToList()
            };

            var timestampSettings = new AddAndSetParamsSettings {
                CreateFamParamIfMissing = true,
                Parameters = [
                    new SetParamModel {
                        Name = "_FOUNDRY LAST PROCESSED AT",
                        DataType = SpecTypeId.String.Text,
                        ValueOrFormula = $"\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\""
                    }
                ]
            };
            var queue = new OperationQueue()
                .Add(new AddSharedParams(apsParamData))
                .Add(new MakeRefPlaneAndDims(profile.MakeRefPlaneAndDims))
                .Add(new AddAndSetParams(addAndSetParamsSettings)) // must come after AddAllFamilyParams and RP/dims
                .Add(new MakeRefPlaneSubcategories(specs))
                .Add(new AddAndSetParams(timestampSettings))
                .Add(new SortParams(new SortParamsSettings()));
            var metadataString = queue.GetExecutableMetadataString();
            Debug.WriteLine(metadataString);

            // force this to never be single transaction
            var executionOptions = new ExecutionOptions {
                SingleTransaction = false,
                OptimizeTypeOperations = profile.ExecutionOptions.OptimizeTypeOperations
            };

            // Request both parameter and refplane snapshots
            var collectorQueue = new CollectorQueue()
                .Add(new ParamSectionCollector())
                .Add(new RefPlaneSectionCollector());

            using var processor = new OperationProcessor(doc, executionOptions);
            var logs = processor
                .SelectFamilies(() => doc.IsFamilyDocument ? null : Pickers.GetSelectedFamilies(uiDoc))
                .ProcessQueue(queue, collectorQueue, outputFolderPath, settings.OnProcessingFinish);

            _ = new ProcessingResultBuilder(storage)
                .WithProfile(profile, "Default")
                .WithOperationMetadata(queue)
                .WithFamilyResults(logs.familyContexts)
                .WithTotalTime(logs.totalMs)
                .WriteOutput(settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);

            var balloon = new Ballogger();
            foreach (var ctx in logs.familyContexts)
                _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {ctx.FamilyName} in {ctx.TotalMs}ms");
            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

public class ProfileFamilyManager : BaseProfileSettings {
    [Description("Settings for adding family parameters")]
    [Required]
    public AddFamilyParamsSettings AddFamilyParams { get; init; } = new();

    [Description("Settings for making reference planes and dimensions")]
    [Required]
    public MakeRefPlaneAndDimsSettings MakeRefPlaneAndDims { get; init; } = new();
}