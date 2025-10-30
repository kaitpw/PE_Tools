using PeExtensions.FamDocument;
using PeServices.Storage;

namespace AddinFamilyFoundrySuite.Core;

public class OperationProcessor<TProfile>
    where TProfile : BaseProfileSettings, new() {
    public OperationProcessor(Storage storage) {
        this.storage = storage;
        var settingsManager = this.storage.Settings();
        this.settings = settingsManager.Json<BaseSettings<TProfile>>().Read();
        this.profile = this.settings.GetProfile(settingsManager);
    }

    public Storage storage { get; }
    public BaseSettings<TProfile> settings { get; }
    public TProfile profile { get; }

    public OperationQueue<TProfile> CreateQueue() => new(this.profile);

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling
    /// </summary>
    public List<OperationLog> ProcessQueue(Document doc, OperationQueue<TProfile> queue, bool singleTransaction = true) {
        if (this.settings.DryRun) {
            this.OutputDryRunResults(doc, queue);
            return new List<OperationLog>();
        }

        var familyResults = new Dictionary<string, (List<OperationLog> logs, double totalMs)>(); 

        var totalSw = Stopwatch.StartNew();
        (Action<Document>[] familyActions, Func<List<OperationLog>> getLogs) = (null, null);
        if (singleTransaction) {
            (familyActions, getLogs) = this.ToBatchedFamilyActions(queue.GetOperationBatches());
        } else {
            (familyActions, getLogs) = this.ToFamilyActions(queue.GetOperations());
        }
        if (doc.IsFamilyDocument) {
            try {
                var familySw = Stopwatch.StartNew();
                var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                _ = doc
                    .ProcessFamily(singleTransaction, familyActions)
                    .SaveFamily(saveLocation);
                familySw.Stop();
                familyResults.Add(doc.Title, (getLogs(), familySw.Elapsed.TotalMilliseconds));
            } catch (Exception ex) {
                Debug.WriteLine($"Failed to process family {doc.Title}: {ex.Message}");
            }
        } else {
            var families = this.profile.GetFamilies(doc);
            foreach (var family in families) {
                var familyName = family.Name; // Capture name 
                try {
                    var familySw = Stopwatch.StartNew();
                    var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                    _ = doc
                        .EditFamily(family)
                        .ProcessFamily(singleTransaction, familyActions)
                        .SaveFamily(saveLocation)
                        .LoadAndCloseFamily(doc, new EditAndLoadFamilyOptions());
                    familySw.Stop();
                    familyResults.Add(familyName, (getLogs(), familySw.Elapsed.TotalMilliseconds));
                } catch (Exception ex) {
                    Debug.WriteLine($"Failed to process family {familyName}: {ex.Message}");
                }
            }
        }

        totalSw.Stop();

        _ = this.OutputProcessingResults(familyResults, totalSw.Elapsed.TotalMilliseconds);
        return familyResults.SelectMany(kvp => kvp.Value.logs).ToList();
    }


    public (Action<Document>[] actions, Func<List<OperationLog>> getLogs) ToFamilyActions(List<IOperation> operations) {
        var familyActions = new List<Action<Document>>();
        var allLogs = new List<OperationLog>();

        foreach (var op in operations) {
            familyActions.Add(famDoc => {
                string currOpName = null;
                try {
                    currOpName = op.Name;
                    var sw = Stopwatch.StartNew();
                    var log = op.Execute(famDoc);
                    sw.Stop();
                    log.MsElapsed = sw.Elapsed.TotalMilliseconds;
                    allLogs.Add(log);

                } catch (Exception ex) {
                    allLogs.Add(
                        new OperationLog(
                            $"{currOpName ?? "Unknown Operation"} (FATAL ERROR)",
                            [new LogEntry { Item = ex.GetType().Name, Error = ex.Message }])
                    );
                }
            });
        }
        return (familyActions.ToArray(), () => {
            var logsCopy = allLogs.ToList();
            allLogs.Clear();
            return logsCopy;
        }
        );
    }

    /// <summary>
    ///     Converts the queued operations into optimized family document callbacks
    /// </summary>
    public (Action<Document>[] actions, Func<List<OperationLog>> getLogs) ToBatchedFamilyActions(List<OperationBatch> batches) {
        var familyActions = new List<Action<Document>>();
        var allLogs = new List<OperationLog>();

        foreach (var batch in batches) {
            switch (batch.Type) {
            case OperationType.Doc:
                familyActions.Add(famDoc => {
                    string currOpName = null;
                    try {
                        foreach (var op in batch.Operations) {
                            currOpName = op.Name;
                            var sw = Stopwatch.StartNew();
                            var log = op.Execute(famDoc);
                            sw.Stop();
                            log.MsElapsed = sw.Elapsed.TotalMilliseconds;
                            allLogs.Add(log);
                        }
                    } catch (Exception ex) {
                        allLogs.Add(
                            new OperationLog(
                                $"{currOpName ?? "Unknown Operation"} (FATAL ERROR)",
                                [new LogEntry { Item = ex.GetType().Name, Error = ex.Message }])
                        );
                    }
                });
                break;

            case OperationType.Type:
                familyActions.Add(famDoc => {
                    string currOpName = null;
                    try {
                        var fm = famDoc.FamilyManager;
                        var operationLogs = new List<OperationLog>();

                        // Switch types once, executing all operations per type
                        foreach (FamilyType famType in fm.Types) {
                            var typeSwitchSw = Stopwatch.StartNew();
                            fm.CurrentType = famType;
                            typeSwitchSw.Stop();
                            var amortizedSwitchMs = typeSwitchSw.Elapsed.TotalMilliseconds / batch.Operations.Count;

                            // Execute all operations for this type
                            foreach (var op in batch.Operations) {
                                currOpName = op.Name;
                                var opSw = Stopwatch.StartNew();
                                var log = op.Execute(famDoc);
                                opSw.Stop();

                                log.MsElapsed = opSw.Elapsed.TotalMilliseconds + amortizedSwitchMs;
                                foreach (var entry in log.Entries) entry.Context = famType.Name;
                                operationLogs.Add(log);
                            }
                        }

                        // Combine logs by operation name
                        var combinedLogs = operationLogs
                            .GroupBy(log => log.OperationName)
                            .Select(group => new OperationLog(group.Key, group.SelectMany(log => log.Entries).ToList()) {
                                MsElapsed = group.Sum(log => log.MsElapsed)
                            });

                        allLogs.AddRange(combinedLogs);
                    } catch (Exception ex) {
                        allLogs.Add(
                            new OperationLog(
                                $"{currOpName ?? "Unknown Operation"}: Error",
                                [new LogEntry { Item = ex.Message }])
                        );
                    }
                });
                break;
            }
        }

        return (familyActions.ToArray(), () => {
            var logsCopy = allLogs.ToList();
            allLogs.Clear();
            return logsCopy;
        }
        );
    }

    private void OutputDryRunResults(Document doc, OperationQueue<TProfile> queue) =>
        OperationLogger.OutputDryRunResults(
            this.storage, this.profile, this.settings.CurrentProfile, doc, queue,
            this.settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish
        );

    private string OutputProcessingResults(Dictionary<string, (List<OperationLog> logs, double totalMs)> familyResults,
        double totalMs) =>
        OperationLogger.OutputProcessingResults(
            this.storage, familyResults, totalMs,
            this.settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish
        );

    private List<string> GetSaveLocations(Document famDoc, ILoadAndSaveOptions options) {
        var saveLocations = new List<string>();
        if (options.SaveFamilyToInternalPath) {
            var saveLocation = this.storage.Output().GetFolderPath();
            saveLocations.Add(saveLocation);
        }

        if (options.SaveFamilyToOutputDir) {
            var saveLocation = famDoc.PathName;
            saveLocations.Add(saveLocation);
        }

        return saveLocations;
    }
}

public interface ILoadAndSaveOptions {
    /// <summary>
    ///     Load the family into the main model document
    /// </summary>
    bool LoadFamily { get; set; }

    /// <summary>
    ///     Save the family to the internal path of the family document
    /// </summary>
    bool SaveFamilyToInternalPath { get; set; }

    /// <summary>
    ///     Save the family to the output directory of the command
    /// </summary>
    bool SaveFamilyToOutputDir { get; set; }
}

internal class EditAndLoadFamilyOptions : IFamilyLoadOptions {
    public bool OnFamilyFound(
        bool familyInUse,
        out bool overwriteParameterValues) {
        overwriteParameterValues = true;
        return true;
    }

    public bool OnSharedFamilyFound(
        Family sharedFamily,
        bool familyInUse,
        out FamilySource source,
        out bool overwriteParameterValues) {
        source = FamilySource.Project;
        overwriteParameterValues = true;
        return true;
    }
}