using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeRevit.Ui;
using PeServices.Storage;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFFMigrator : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            var storage = new Storage("FF Migrator");
            var settingsManager = storage.Settings();
            var settings = settingsManager.Json<BaseSettings<ProfileRemap>>().Read();
            var profile = settings.GetProfile(settingsManager);
            var outputFolderPath = storage.Output().GetFolderPath();

            using var processor = new OperationProcessor<ProfileRemap>(
                doc,
                profile.GetFamilies,
                profile.GetAPSParams,
                profile.ExecutionOptions);
            var apsParamData = processor.GetApsParams();
            var apsParamNames = apsParamData.Select(p => p.externalDefinition.Name).ToList();
            var mappingDataAllNames = profile.AddAndMapSharedParams.MappingData
                .Select(m => m.CurrName)
                .Concat(apsParamNames)
                .ToList();

            var addFamilyParamsSettings = new AddAndSetFormulaFamilyParamsSettings {
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
                .Add(new MakeElecConnector(profile.HydrateElectricalConnector))
                .Add(new DeleteUnusedParams(profile.DeleteUnusedParams, apsParamNames))
                .Add(new DebugLogAnnoInfo(new()))
                .Add(new AddAndSetFormulaFamilyParams(addFamilyParamsSettings));

            var metadata = queue.GetExecutableMetadata();
            foreach (var op in metadata)
                Debug.WriteLine($"[Batch {op.IsMerged}] {op.Type}: {op.Name} - {op.Description}");


            if (profile.ExecutionOptions.PreviewRun)
                OperationLogger.OutputDryRunResults(
                    apsParamData,
                    doc,
                    queue,
                    profile.GetFamilies,
                    storage,
                    settings.CurrentProfile,
                    settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);
            else {
                var logs = processor.ProcessQueue(queue, outputFolderPath, settings.OnProcessingFinish);
                var logPath = OperationLogger.OutputProcessingResults(
                    logs.familyResults,
                    logs.totalMs,
                    storage,
                    settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);
                var balloon = new Ballogger();

                foreach (var (famName, (_, ms)) in logs.familyResults)
                    _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {famName} in {ms}ms");
                balloon.Show();
            }

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}
public class ProfileRemap : BaseProfileSettings {
    [Description("Settings for deleting unused parameters")]
    [Required]
    public DeleteUnusedParamsSettings DeleteUnusedParams { get; init; } = new();

    [Description("Settings for deleting unused nested families")]
    [Required]
    public DefaultOperationSettings DeleteUnusedNestedFamilies { get; init; } = new();

    [Description("Settings for parameter mapping (add/replace and remap)")]
    [Required]
    public MapParamsSettings AddAndMapSharedParams { get; init; } = new();

    [Description("Settings for hydrating electrical connectors")]
    [Required]
    public MakeElecConnectorSettings HydrateElectricalConnector { get; init; } = new();
}