using PeServices.Storage;
using PeUtils.Files;

namespace AddinFamilyFoundrySuite.Core;

public class OperationLogger {
    public static (object summary, object detailed) GenerateDryRunData<TProfile>(
        OperationProcessor<TProfile> processor,
        OperationQueue queue,
        Func<Document, List<Family>> getFamilies,
        string currentProfile
    ) where TProfile : BaseProfileSettings, new() {
        var apsParams = processor.GetApsParams();
        var families = getFamilies(processor.doc);
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
        OperationProcessor<TProfile> processor,
        OperationQueue queue,
        Func<Document, List<Family>> getFamilies,
        Storage storage,
        string currentProfile,
        bool openOutputFilesOnCommandFinish
    ) where TProfile : BaseProfileSettings, new() {
        var (summary, detailed) = GenerateDryRunData(processor, queue, getFamilies, currentProfile);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"dry-run_{timestamp}.json";
        var detailedFilename = $"dry-run_{timestamp}_detailed.json";

        storage.Output().Json<object>(filename).Write(summary);
        storage.Output().Json<object>(detailedFilename).Write(detailed);

        if (openOutputFilesOnCommandFinish)
            FileUtils.OpenInDefaultApp(Path.Combine(storage.Output().GetFolderPath(), filename));
    }

    public static string OutputProcessingResults(
        Dictionary<string, (List<OperationLog>, double)> familyResults,
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

        var logPath = Path.Combine(storage.Output().GetFolderPath(), filename);
        if (openOutputFilesOnCommandFinish)
            FileUtils.OpenInDefaultApp(logPath);

        return logPath;
    }
}