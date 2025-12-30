using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PeServices.Storage;
using PeServices.Storage.Core.Json.ContractResolvers;
using PeServices.Storage.Core.Json.Converters;
using PeUtils.Files;

namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Fluent builder for generating processing result output files.
/// </summary>
public class ProcessingResultBuilder {
    private static readonly JsonSerializerSettings JsonSettings = new() {
        Formatting = Formatting.Indented,
        ContractResolver = new RequiredAwareContractResolver(),
        Converters = [new ForgeTypeIdConverter(), new StringEnumConverter()]
    };

    private readonly Storage _storage;
    private List<FamilyProcessingContext> _familyContexts = [];
    private List<(string Name, string Description, string Type, string IsMerged)> _operationMetadata = [];
    private string _profileName;
    private object _profileSettings;
    private double _totalMs;

    public ProcessingResultBuilder(Storage storage) => this._storage = storage;

    public ProcessingResultBuilder WithProfile<T>(T settings, string profileName) where T : BaseProfileSettings {
        this._profileSettings = settings;
        this._profileName = profileName;
        return this;
    }

    public ProcessingResultBuilder WithOperationMetadata(OperationQueue queue) {
        this._operationMetadata = queue.GetExecutableMetadata();
        return this;
    }

    public ProcessingResultBuilder WithFamilyResults(List<FamilyProcessingContext> contexts) {
        this._familyContexts = contexts;
        return this;
    }

    public ProcessingResultBuilder WithTotalTime(double totalMs) {
        this._totalMs = totalMs;
        return this;
    }

    /// <summary>
    ///     Writes a timestamped run directory with per-family output files:
    ///     - presnapshot.json / presnapshot.csv
    ///     - postsnapshot.json / postsnapshot.csv
    ///     - abridged.json / detailed.json
    ///     - settings.json
    /// </summary>
    /// <returns>Path to the run directory.</returns>
    public string WriteOutput(bool openOnFinish) {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var runDir = Path.Combine(this._storage.OutputDir().DirectoryPath, timestamp);
        _ = Directory.CreateDirectory(runDir);

        string firstAbridgedPath = null;

        foreach (var ctx in this._familyContexts) {
            var familyDirName = SanitizeDirName(ctx.FamilyName);
            var familyDir = Path.Combine(runDir, familyDirName);
            _ = Directory.CreateDirectory(familyDir);

            var pre = ctx.PreProcessSnapshot?.Parameters?.Data ?? [];
            var post = ctx.PostProcessSnapshot?.Parameters?.Data ?? [];

            File.WriteAllText(Path.Combine(familyDir, "presnapshot.json"), pre.ToJson());
            File.WriteAllText(Path.Combine(familyDir, "presnapshot.csv"), pre.ToCsv());
            File.WriteAllText(Path.Combine(familyDir, "postsnapshot.json"), post.ToJson());
            File.WriteAllText(Path.Combine(familyDir, "postsnapshot.csv"), post.ToCsv());

            var abridgedPath = Path.Combine(familyDir, "abridged.json");
            var detailedPath = Path.Combine(familyDir, "detailed.json");
            var settingsPath = Path.Combine(familyDir, "settings.json");

            WriteJson(settingsPath, new { Profile = this._profileName, ProfileSettings = this._profileSettings });
            WriteJson(abridgedPath, BuildAbridged(ctx));
            WriteJson(detailedPath, this.BuildDetailed(ctx));

            firstAbridgedPath ??= abridgedPath;
        }

        if (openOnFinish && firstAbridgedPath is not null)
            FileUtils.OpenInDefaultApp(firstAbridgedPath);

        return runDir;
    }

    private static object BuildAbridged(FamilyProcessingContext ctx) {
        var (logs, err) = ctx.OperationLogs;
        if (err is not null) {
            return new {
                Family = ctx.FamilyName,
                TotalSecondsElapsed = Math.Round(ctx.TotalMs / 1000.0, 3),
                Error = err.Message
            };
        }

        var operationLogs = logs ?? [];
        return new {
            Family = ctx.FamilyName,
            TotalSecondsElapsed = Math.Round(ctx.TotalMs / 1000.0, 3),
            Operations = operationLogs.Select(log => new {
                log.OperationName,
                SuccessTotal = $"{log.SuccessCount}/{log.SuccessCount + log.ErrorCount}",
                Errors = BuildMessages(log.Entries, LogStatus.Error),
            }).ToList()
        };
    }

    private object BuildDetailed(FamilyProcessingContext ctx) {
        var (logs, err) = ctx.OperationLogs;
        var operationLogs = err != null ? new List<OperationLog>() : logs ?? [];

        return new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            RunTotalSecondsElapsed = Math.Round(this._totalMs / 1000.0, 3),
            Family = ctx.FamilyName,
            FamilyTotalSecondsElapsed = Math.Round(ctx.TotalMs / 1000.0, 3),
            Error = err?.Message,
            Profile = this._profileName,
            OperationMetadata =
                this._operationMetadata.Select(op => new { op.Name, op.Description, op.Type, op.IsMerged }).ToList(),
            Operations = operationLogs.Select(log => new {
                log.OperationName,
                SecondsElapsed = Math.Round(log.MsElapsed / 1000.0, 3),
                Successes = BuildMessages(log.Entries, LogStatus.Success),
                Skipped = BuildMessages(log.Entries, LogStatus.Skipped),
                Errors = BuildMessages(log.Entries, LogStatus.Error),
            }).ToList()
        };
    }

    private static List<string> BuildMessages(IEnumerable<LogEntry> entries, LogStatus status) =>
        entries.Where(e => e.Status == status)
            .GroupBy(e => new { e.Name, e.Message })
            .Select(g => {
                var contexts = g.Select(e => e.Context).Where(c => c != null).ToList();
                var contextsStr = contexts.Any() ? $"[{string.Join(", ", contexts)}] " : string.Empty;
                var messageStr = !string.IsNullOrEmpty(g.Key.Message) ? $" : {g.Key.Message}" : "";
                return $"{contextsStr}{g.Key.Name}{messageStr}";
            }).ToList();


    private static void WriteJson(string path, object data) {
        var json = JsonConvert.SerializeObject(data, JsonSettings);
        File.WriteAllText(path, json);
    }

    private static string SanitizeDirName(string name) {
        if (string.IsNullOrWhiteSpace(name))
            return "Unnamed";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray();

        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Unnamed" : sanitized;
    }
}

/// <summary>
///     Builder for dry-run output.
/// </summary>
public class DryRunResultBuilder {
    private readonly Storage _storage;
    private List<SharedParameterDefinition> _apsParams = [];
    private List<Family> _families = [];
    private List<(string Name, string Description, string Type, string IsMerged)> _operationMetadata = [];
    private string _profileName;
    private object _profileSettings;

    public DryRunResultBuilder(Storage storage) => this._storage = storage;

    public DryRunResultBuilder WithProfile<T>(T settings, string profileName) where T : BaseProfileSettings {
        this._profileSettings = settings;
        this._profileName = profileName;
        return this;
    }

    public DryRunResultBuilder WithApsParams(List<SharedParameterDefinition> apsParams) {
        this._apsParams = apsParams;
        return this;
    }

    public DryRunResultBuilder WithFamilies(List<Family> families) {
        this._families = families;
        return this;
    }

    public DryRunResultBuilder WithOperationMetadata(OperationQueue queue) {
        this._operationMetadata = queue.GetExecutableMetadata();
        return this;
    }

    public string WriteOutput(bool openOnFinish) {
        var (summary, detailed) = this.GenerateDryRunData();

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"dry-run_{timestamp}.json";
        var detailedFilename = $"dry-run_{timestamp}_detailed.json";

        _ = this._storage.OutputDir().Json<object>(filename).Write(summary);
        _ = this._storage.OutputDir().Json<object>(detailedFilename).Write(detailed);

        var logPath = Path.Combine(this._storage.OutputDir().DirectoryPath, filename);
        if (openOnFinish)
            FileUtils.OpenInDefaultApp(logPath);

        return logPath;
    }

    private (object summary, object detailed) GenerateDryRunData() {
        var summary = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Profile = this._profileName,
            Operations = this._operationMetadata.Select(op =>
                new { Operation = $"[Batch {op.IsMerged}] ({op.Type}) {op.Name}", op.Description }).ToList(),
            ApsParameters = this._apsParams.Select(p => p.ExternalDefinition.Name).ToList(),
            Families = this._families.Select(f => f.Name).ToList(),
            Summary = new { TotalApsParameters = this._apsParams.Count, TotalFamilies = this._families.Count }
        };

        var detailed = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Profile = this._profileName,
            ProfileSettings = this._profileSettings,
            Operations = this._operationMetadata.Select(op =>
                new { Operation = $"[Batch {op.IsMerged}] ({op.Type}) {op.Name}", op.Description }).ToList(),
            ApsParameters = this._apsParams.Select(p => new {
                p.ExternalDefinition.Name,
                GUID = p.ExternalDefinition.GUID.ToString(),
                GroupTypeId = p.GroupTypeId.TypeId,
                DataType = p.ExternalDefinition.GetDataType().TypeId,
                IsInstance = p.IsInstance,
                p.ExternalDefinition.Description
            }).ToList(),
            Families = this._families.Select(f => new {
                f.Name,
                Id = f.Id.ToString(),
                CategoryName = f.FamilyCategory?.Name,
                CategoryId = f.FamilyCategory?.Id.ToString(),
                f.IsEditable,
                f.IsUserCreated
            }).ToList(),
            Summary = new { TotalApsParameters = this._apsParams.Count, TotalFamilies = this._families.Count }
        };

        return (summary, detailed);
    }
}