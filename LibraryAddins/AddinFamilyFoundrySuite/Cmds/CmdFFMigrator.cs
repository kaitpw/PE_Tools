using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;
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
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("FF Migrator");
            var settingsManager = storage.SettingsDir();
            var settings = settingsManager.Json<BaseSettings<ProfileRemap>>().Read();
            var profile = settingsManager.SubDir("profiles").Json<ProfileRemap>($"{settings.CurrentProfile}.json")
                .Read();
            var outputFolderPath = storage.OutputDir().DirectoryPath;

            using var tempFile = new TempSharedParamFile(doc);
            var apsParamData = profile.GetAPSParams(tempFile);

            using var processor = new OperationProcessor(
                doc,
                profile.ExecutionOptions);
            var apsParamNames = apsParamData.Select(p => p.externalDefinition.Name).ToList();
            var mappingDataAllNames = profile.AddAndMapSharedParams.MappingData
                .Select(m => m.CurrName)
                .Concat(apsParamNames);

            var addTimestamp = new AddAndSetParamsSettings {
                Parameters = [
                    new SetParamModel {
                        Name = "_FOUNDRY LAST PROCESSED AT",
                        PropertiesGroup = new ForgeTypeId(""),
                        DataType = SpecTypeId.String.Text,
                        IsInstance = false,
                        ValueOrFormula = DateTime.Now.ToString("yyyy_MM_dd HH:mm:ss")
                    }
                ]
            };

            var addFamParams = new AddAndSetParamsSettings {
                Parameters = [
                    new SetParamModel {
                        Name = "PE_E___NumberOfPoles",
                        ValueOrFormula = "if(PE_E___Voltage = 120, 1, if(PE_E___Voltage = 208, 2, (if(PE_E___Voltage = 240, 2, 1))))"
                    },
                    new SetParamModel {
                        Name = "PE_E___ApparentPower",
                        ValueOrFormula = "PE_E___Voltage * PE_E___MCA * 0.8 * if(PE_E___NumberOfPoles = 3, sqrt(3), 1)"
                    }
                ]
            };


            var queue = new OperationQueue()
                .Add(new DeleteUnusedParams(profile.DeleteUnusedParams, mappingDataAllNames))
                .Add(new DeleteUnusedNestedFamilies(profile.DeleteUnusedNestedFamilies))
                .Add(new MapAndAddSharedParams(profile.AddAndMapSharedParams, apsParamData))
                .Add(new MakeElecConnector(profile.HydrateElectricalConnector))
                .Add(new UnwrapFormulas(apsParamNames))
                .Add(new AddAndSetParams(addFamParams))
                .Add(new DeleteUnusedParams(profile.DeleteUnusedParams, apsParamNames))
                .Add(new AddAndSetParams(addTimestamp));

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
                var logs = processor
                    .SelectFamilies(() => {
                        var picked = Pickers.GetSelectedFamilies(uiDoc);
                        return picked.Any() ? picked : profile.GetFamilies(doc);
                    }
                    )
                    .ProcessQueue(queue, outputFolderPath, settings.OnProcessingFinish);
                var logPath = OperationLogger.OutputProcessingResults(
                    logs.familyResults,
                    logs.totalMs,
                    storage,
                    settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);
                var balloon = new Ballogger();

                foreach (var output in logs.familyResults)
                    _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {output.FamilyName} in {output.TotalMs}ms");
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