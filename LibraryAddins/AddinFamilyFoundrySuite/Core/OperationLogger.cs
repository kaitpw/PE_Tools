using PeServices.Storage;
using PeUtils.Files;

namespace AddinFamilyFoundrySuite.Core;

public class OperationLogger {
    public static (object summary, object detailed) GenerateDryRunData(
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> apsParams,
        Document doc,
        OperationQueue queue,
        Func<Document, List<Family>> getFamilies,
        string currentProfile
    ) {
        var families = getFamilies(doc);
        var operationMetadata = queue.GetExecutableMetadata();

        var summary = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Profile = currentProfile,
            Operations =
                operationMetadata.Select(op =>
                    new { Operation = $"[Batch {op.IsMerged}] ({op.Type}) {op.Name}", op.Description }).ToList(),
            ApsParameters = apsParams.Select(p => p.externalDefinition.Name).ToList(),
            Families = families.Select(f => f.Name).ToList(),
            Summary = new { TotalApsParameters = apsParams.Count, TotalFamilies = families.Count }
        };

        var detailed = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Profile = currentProfile,
            Operations =
                operationMetadata.Select(op =>
                    new { Operation = $"[Batch {op.IsMerged}] ({op.Type}) {op.Name}", op.Description }).ToList(),
            ApsParameters = apsParams.Select(p => new {
                p.externalDefinition.Name,
                GUID = p.externalDefinition.GUID.ToString(),
                GroupTypeId = p.groupTypeId.TypeId,
                DataType = p.externalDefinition.GetDataType().TypeId,
                IsInstance = p.isInstance,
                p.externalDefinition.Description
            }).ToList(),
            Families = families.Select(f => new {
                f.Name,
                Id = f.Id.ToString(),
                CategoryName = f.FamilyCategory?.Name,
                CategoryId = f.FamilyCategory?.Id.ToString(),
                f.IsEditable,
                f.IsUserCreated
            }).ToList(),
            Summary = new { TotalApsParameters = apsParams.Count, TotalFamilies = families.Count }
        };

        return (summary, detailed);
    }

    public static (object summary, object detailed) GenerateLogData(
        List<FamilyProcessOutput> familyResults,
        double totalMs
    ) {
        // Summary log with grouped errors
        var summary = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalSecondsElapsed = Math.Round(totalMs / 1000.0, 3),
            ProcessedFamilies = familyResults.Select(output => {
                var (logs, err) = output.logs;
                var operationLogs = err != null ? new List<OperationLog>() : logs;
                return new {
                    FamilyName = output.familyName,
                    TotalSecondsElapsed = Math.Round(output.totalMs / 1000.0, 3),
                    Operations = operationLogs.Select(log => {
                        // Group errors by item and error message, collecting contexts
                        var groupedErrors = log.Entries
                            .Where(e => e.Error != null)
                            .GroupBy(e => new { e.Item, e.Error })
                            .Select(g => {
                                var contexts = g.Select(e => e.Context).Where(c => c != null).ToList();
                                var contextsStr = contexts.Any() ? $"[{string.Join(", ", contexts)}] " : "";
                                return $"{contextsStr}{g.Key.Item} : {g.Key.Error}";
                            })
                            .ToList();

                        return new Dictionary<string, object> {
                            ["OperationName"] = log.OperationName,
                            ["SecondsElapsed"] = Math.Round(log.MsElapsed / 1000.0, 3),
                            ["SuccessCount"] = log.SuccessCount,
                            ["FailedCount"] = log.FailedCount,
                            ["Errors"] = groupedErrors
                        };
                    }).ToList()
                };
            }).ToList()
        };

        // Detailed log with all entries
        var detailed = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ProcessedFamilies = familyResults.Select(output => {
                var (logs, err) = output.logs;
                var operationLogs = err != null ? new List<OperationLog>() : logs;
                return new {
                    FamilyName = output.familyName,
                    Operations = operationLogs.Select(log => new {
                        log.OperationName,
                        Successes = log.Entries.Where(e => e.Error == null)
                            .GroupBy(e => new { e.Item, e.Error })
                            .Select(g => {
                                var contexts = g.Select(e => e.Context).Where(c => c != null).ToList();
                                var contextsStr = contexts.Any() ? $"[{string.Join(", ", contexts)}] " : "";
                                return $"{contextsStr}{g.Key.Item}";
                            })
                            .ToList(),
                        Errors = log.Entries
                            .Where(e => e.Error != null)
                            .GroupBy(e => new { e.Item, e.Error })
                            .Select(g => {
                                var contexts = g.Select(e => e.Context).Where(c => c != null).ToList();
                                var contextsStr = contexts.Any() ? $"[{string.Join(", ", contexts)}] " : "";
                                return $"{contextsStr}{g.Key.Item} : {g.Key.Error}";
                            })
                            .ToList()
                    }).ToList()
                };
            }).ToList()
        };

        return (summary, detailed);
    }

    public static void OutputDryRunResults(
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> apsParams,
        Document doc,
        OperationQueue queue,
        Func<Document, List<Family>> getFamilies,
        Storage storage,
        string currentProfile,
        bool openOutputFilesOnCommandFinish
    ) {
        var (summary, detailed) = GenerateDryRunData(apsParams, doc, queue, getFamilies, currentProfile);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"dry-run_{timestamp}.json";
        var detailedFilename = $"dry-run_{timestamp}_detailed.json";

        storage.Output().Json<object>(filename).Write(summary);
        storage.Output().Json<object>(detailedFilename).Write(detailed);

        if (openOutputFilesOnCommandFinish)
            FileUtils.OpenInDefaultApp(Path.Combine(storage.Output().DirectoryPath, filename));
    }

    public static string OutputProcessingResults(
        List<FamilyProcessOutput> familyResults,
        double totalMs,
        Storage storage,
        bool openOutputFilesOnCommandFinish
    ) {
        var (summary, detailed) = GenerateLogData(familyResults, totalMs);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"{timestamp}.json";
        var detailedFilename = $"{timestamp}_detailed.json";

        storage.Output().Json<object>(filename).Write(summary);
        storage.Output().Json<object>(detailedFilename).Write(detailed);

        var logPath = Path.Combine(storage.Output().DirectoryPath, filename);
        if (openOutputFilesOnCommandFinish)
            FileUtils.OpenInDefaultApp(logPath);

        return logPath;
    }
}