using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using Newtonsoft.Json;
using PeExtensions.FamDocument;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeServices.Storage.Core;
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
            var settingsManager = storage.Settings();
            var settings = settingsManager.Json<BaseSettings<ProfileFamilyManager>>().Read();
            var profile = settingsManager.Subdirectory("profiles").Json<ProfileFamilyManager>($"{settings.CurrentProfile}.json").Read();
            var outputFolderPath = storage.Output().DirectoryPath;

            // force this to never be single transaction
            var executionOptions = new ExecutionOptions {
                SingleTransaction = false,
                PreviewRun = profile.ExecutionOptions.PreviewRun,
                OptimizeTypeOperations = profile.ExecutionOptions.OptimizeTypeOperations
            };

            using var tempFile = new TempSharedParamFile(doc);
            var apsParamData = profile.GetAPSParams(tempFile);

            using var processor = new OperationProcessor(
                doc,
                executionOptions);

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
            var queue = new OperationQueue()
                    .Add(new AddSharedParams(apsParamData))
                    .Add(new MakeRefPlaneAndDims(profile.MakeRefPlaneAndDims))
                    .Add(new AddAndGlobalSetFamilyParams(profile.AddAndGlobalSetFamilyParams))
                    .Add(new AddAndSetFormulaFamilyParams(addFamilyParams));

            var metadataString = queue.GetExecutableMetadataString();
            Debug.WriteLine(metadataString);

            if (executionOptions.PreviewRun) {
                OperationLogger.OutputDryRunResults(
                    apsParamData,
                    doc,
                    queue,
                    profile.GetFamilies,
                    storage,
                    settings.CurrentProfile,
                    settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);
                return Result.Succeeded;
            }

            var logs = processor
                .SelectFamilies(
                    () => doc.IsFamilyDocument ? null : Pickers.GetSelectedFamilies(uiDoc)
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
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}


public class ProfileFamilyManager : BaseProfileSettings {

    [Description("Settings for making reference planes and dimensions")]
    [Required]
    public MakeRefPlaneAndDimsSettings MakeRefPlaneAndDims { get; init; } = new();

    [Description("Settings for adding family parameters")]
    [Required]
    public AddAndGlobalSetFamilyParamsSettings AddAndGlobalSetFamilyParams { get; init; } = new();
}