using PeServices.Storage;
using PeUtils.Files;

namespace AddinFamilyFoundrySuite.Core;

public class OperationLogger {
    public static (object summary, object detailed) GenerateDryRunData<TProfile>(
        TProfile profile,
        string currentProfile,
        Document doc,
        OperationQueue<TProfile> queue
    ) where TProfile : BaseProfileSettings, new() {
        using var tempFile = new TempSharedParamFile(doc);
        var apsParams = profile.GetAPSParams(tempFile);
        var families = profile.GetFamilies(doc);
        var operationMetadata = queue.GetOperationMetadata();

        var summary = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Profile = currentProfile,
            Operations = operationMetadata.Select(op => new {
                Operation = $"[Batch {op.BatchGroup}] {op.Type}: {op.Name}",
                op.Description
            }).ToList(),
            ApsParameters = apsParams.Select(p => p.externalDefinition.Name).ToList(),
            Families = families.Select(f => f.Name).ToList(),
            Summary = new { TotalApsParameters = apsParams.Count, TotalFamilies = families.Count }
        };

        var detailed = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Profile = currentProfile,
            Operations = operationMetadata.Select(op => new {
                Operation = $"[Batch {op.BatchGroup}] {op.Type}: {op.Name}",
                op.Description
            }).ToList(),
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
        Dictionary<string, (List<OperationLog> logs, double totalMs)> familyResults,
        double totalMs
    ) {
        // Summary log with grouped errors
        var summary = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalSecondsElapsed = Math.Round(totalMs / 1000.0, 3),
            ProcessedFamilies = familyResults.Select(kvp => new {
                FamilyName = kvp.Key,
                TotalSecondsElapsed = Math.Round(kvp.Value.totalMs / 1000.0, 3),
                Operations = kvp.Value.logs.Select(log => {
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
            }).ToList()
        };

        // Detailed log with all entries
        var detailed = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ProcessedFamilies = familyResults.Select(kvp => new {
                FamilyName = kvp.Key,
                Operations = kvp.Value.logs.Select(log => new {
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
            }).ToList()
        };

        return (summary, detailed);
    }

    public static void OutputDryRunResults<TProfile>(
        Storage storage,
        TProfile profile,
        string currentProfile,
        Document doc,
        OperationQueue<TProfile> queue,
        bool openOutputFiles
    ) where TProfile : BaseProfileSettings, new() {
        var (summary, detailed) = GenerateDryRunData(profile, currentProfile, doc, queue);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"dry-run_{timestamp}.json";
        var detailedFilename = $"dry-run_{timestamp}_detailed.json";

        storage.Output().Json<object>(filename).Write(summary);
        storage.Output().Json<object>(detailedFilename).Write(detailed);

        if (openOutputFiles)
            FileUtils.OpenInDefaultApp(Path.Combine(storage.Output().GetFolderPath(), filename)); // TODO: change back to plain filename
    }

    public static string OutputProcessingResults(
        Storage storage,
        Dictionary<string, (List<OperationLog> logs, double totalMs)> familyResults,
        double totalMs,
        bool openOutputFiles
    ) {
        var (summary, detailed) = GenerateLogData(familyResults, totalMs);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"{timestamp}.json";
        var detailedFilename = $"{timestamp}_detailed.json";

        storage.Output().Json<object>(filename).Write(summary);
        storage.Output().Json<object>(detailedFilename).Write(detailed);

        var logPath = Path.Combine(storage.Output().GetFolderPath(), filename);
        if (openOutputFiles)
            FileUtils.OpenInDefaultApp(logPath);

        return logPath;
    }
}