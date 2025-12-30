using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Aggregators;
using AddinFamilyFoundrySuite.Core.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdFFParamAggregator : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("FF Param Aggregator");
            var settingsManager = storage.SettingsDir();
            var settings = settingsManager.Json<BaseSettings<ProfileParamAggregator>>().Read();
            // TODO: Add palette UI for profile selection like CmdFFMigrator
            var profile = settingsManager
                .SubDir("profiles")
                .Json<ProfileParamAggregator>("Default.json")
                .Read();

            // Get families based on profile filter (or selected families)
            var selectedFamilies = Pickers.GetSelectedFamilies(uiDoc);
            var families = selectedFamilies.Any()
                ? selectedFamilies
                : profile.GetFamilies(doc);

            if (!families.Any()) {
                new Ballogger()
                    .Add(Log.WARN, new StackFrame(), "No families found matching the filter criteria.")
                    .Show();
                return Result.Cancelled;
            }

            // Create collectors - uses separated IProjectCollector/IFamilyDocCollector system
            var collectorQueue = new CollectorQueue()
                .Add(new ParamSectionCollector());

            var aggregator = new FamilyParamAggregator(collectorQueue);

            // Aggregate parameters
            var balloon = new Ballogger();
            _ = balloon.Add(Log.INFO, new StackFrame(), $"Analyzing {families.Count} families...");

            var aggregatedData = aggregator.Aggregate(doc, families);

            // Write to CSV
            var csvPath = aggregator.WriteToCsv(aggregatedData, storage);

            _ = balloon.Add(Log.INFO, new StackFrame(),
                $"Aggregated {aggregatedData.Count} unique parameters from {families.Count} families.");

            if (settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish) FileUtils.OpenInDefaultApp(csvPath);

            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}

/// <summary>
///     Profile settings for the Param Aggregator command.
/// </summary>
public class ProfileParamAggregator : BaseProfileSettings {
    [Description("The type of collector to use for gathering parameter data.")]
    [Required]
    public ParamCollectorType CollectorType { get; init; } = ParamCollectorType.TempInstance;
}

/// <summary>
///     Available collector types for parameter aggregation.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ParamCollectorType {
    [Description("Uses temporary instance placement with transaction rollback. Fast and non-destructive.")]
    TempInstance,

    [Description("Opens family document for editing. Required for advanced data like connectors. (Future)")]
    EditFamily
}
