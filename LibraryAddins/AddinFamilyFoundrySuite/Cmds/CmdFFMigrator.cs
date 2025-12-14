using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Aggregators;
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

            var apsParamNames = apsParamData.Select(p => p.externalDefinition.Name).ToList();
            var mappingDataAllNames = profile.AddAndMapSharedParams.MappingData
                .SelectMany(m => m.CurrNames)
                .Concat(apsParamNames);

            var internalParams = new List<SetParamModel> {
                    new SetParamModel {
                        Name = "PE_E___NumberOfPoles",
                        ValueOrFormula =
                            "if(PE_E___Voltage = 120, 1, if(PE_E___Voltage = 208, 2, (if(PE_E___Voltage = 240, 2, 1))))"
                    },
                    new SetParamModel {
                        Name = "PE_E___ApparentPower",
                        ValueOrFormula = "PE_E___Voltage * PE_E___MCA * 0.8 * if(PE_E___NumberOfPoles = 3, sqrt(3), 1)"
                    },
                    new SetParamModel {
                        Name = "_FOUNDRY LAST PROCESSED AT",
                        PropertiesGroup = new ForgeTypeId(""),
                        DataType = SpecTypeId.String.Text,
                        IsInstance = false,
                        ValueOrFormula = $"\"{DateTime.Now:yyyy_MM_dd HH:mm:ss}\""
                    }
            };

            var addAndSet = new AddAndSetParamsSettings {
                OverrideExistingValues = profile.AddAndSetParams.OverrideExistingValues,
                CreateFamParamIfMissing = profile.AddAndSetParams.CreateFamParamIfMissing,
                Parameters = profile.AddAndSetParams.Parameters.Concat(internalParams).ToList(),
                ParametersPerType = profile.AddAndSetParams.ParametersPerType,
            };

            var queue = new OperationQueue()
                .Add(new PurgeParams(profile.DeleteUnusedParams, mappingDataAllNames))
                .Add(new PurgeNestedFamilies(profile.DeleteUnusedNestedFamilies))
                .Add(new AddAndMapSharedParams(profile.AddAndMapSharedParams, apsParamData))
                .Add(new AddAndSetParams(addAndSet))
                .Add(new MakeElecConnector(profile.MakeElectricalConnector))
                .Add(new PurgeParams(profile.DeleteUnusedParams, apsParamNames))
                .Add(new SortParams(profile.SortParams));

            var metadataString = queue.GetExecutableMetadataString();
            Debug.WriteLine(metadataString);


            if (profile.ExecutionOptions.PreviewRun) {
                _ = new DryRunResultBuilder(storage)
                    .WithProfile(profile, settings.CurrentProfile)
                    .WithApsParams(apsParamData)
                    .WithFamilies(profile.GetFamilies(doc))
                    .WithOperationMetadata(queue)
                    .WriteOutput(settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);
            } else {
                // Create collectors for pre/post snapshots (project doc vs family doc)
                var projectCollector = new ProjectParamCollector();
                var familyDocCollector = new FamilyDocParamCollector();

                using var processor =
                    new OperationProcessor(doc, profile.ExecutionOptions, projectCollector, familyDocCollector);
                var logs = processor
                    .SelectFamilies(() => {
                        var picked = Pickers.GetSelectedFamilies(uiDoc);
                        return picked.Any() ? picked : profile.GetFamilies(doc);
                    })
                    .ProcessQueue(queue, outputFolderPath, settings.OnProcessingFinish);

                _ = new ProcessingResultBuilder(storage)
                    .WithProfile(profile, settings.CurrentProfile)
                    .WithOperationMetadata(queue)
                    .WithFamilyResults(logs.familyContexts)
                    .WithTotalTime(logs.totalMs)
                    .WriteOutput(settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish);

                var balloon = new Ballogger();
                foreach (var ctx in logs.familyContexts)
                    _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {ctx.FamilyName} in {ctx.TotalMs}ms");
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
    public PurgeParamsSettings DeleteUnusedParams { get; init; } = new();

    [Description("Settings for deleting unused nested families")]
    [Required]
    public DefaultOperationSettings DeleteUnusedNestedFamilies { get; init; } = new();

    [Description("Settings for parameter mapping (add/replace and remap)")]
    [Required]
    public MapParamsSettings AddAndMapSharedParams { get; init; } = new();

    [Description("Settings for setting parameter values and adding family parameters.")]
    [Required]
    public AddAndSetParamsSettings AddAndSetParams { get; init; } = new();

    [Description("Settings for hydrating electrical connectors")]
    [Required]
    public MakeElecConnectorSettings MakeElectricalConnector { get; init; } = new();

    [Description("Settings for sorting parameters within each property group.")]
    [Required]
    public SortParamsSettings SortParams { get; init; } = new();
}