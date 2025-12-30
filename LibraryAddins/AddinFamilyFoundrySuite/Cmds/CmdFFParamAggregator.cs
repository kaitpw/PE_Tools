using AddinFamilyFoundrySuite.Core.Aggregators;
using AddinFamilyFoundrySuite.Core.Snapshots;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;

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

            // Get families based on profile filter (or selected families)
            var selectedFamilies = Pickers.GetSelectedFamilies(uiDoc);
            var families = selectedFamilies.Any()
                ? selectedFamilies
                : new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .OfType<Family>()
                    .ToList();

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

            FileUtils.OpenInDefaultApp(csvPath);

            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}