namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Fluent processor that batches document and type operations for optimal execution
/// </summary>
public class OperationQueue<TProfile> where TProfile : new() {
    private readonly List<IOperation> _operations = new();
    private readonly TProfile _profile;

    public OperationQueue(TProfile profile) => this._profile = profile;

    /// <summary>
    ///     Add an operation to the queue with explicit settings from the profile.
    /// </summary>
    public OperationQueue<TProfile> Add<TOpSettings>(
        IOperation<TOpSettings> operation,
        Func<TProfile, TOpSettings> settingsSelector
    ) where TOpSettings : class, IOperationSettings, new() {
        // Extract settings from profile using the selector
        operation.Settings = settingsSelector(this._profile);

        if (operation.Settings == null) {
            throw new InvalidOperationException(
                $"Operation '{operation.GetType().Name}' requires settings of type '{typeof(TOpSettings).Name}', " +
                $"but the settings selector returned null.");
        }

        this._operations.Add(operation);
        return this;
    }

    public OperationQueue<TProfile> Add<TOpSettings>(
        ICompoundOperation<TOpSettings> operation,
        Func<TProfile, TOpSettings> settingsSelector
    ) where TOpSettings : class, IOperationSettings, new() {
        var parentName = operation.GetType().Name;

        foreach (var op in operation.Operations) {
            op.Settings = settingsSelector(this._profile);

            if (op.Settings == null) {
                throw new InvalidOperationException(
                    $"Operation '{op.GetType().Name}' requires settings of type '{typeof(TOpSettings).Name}', " +
                    $"but the settings selector returned null.");
            }

            // Wrap operation to prefix log name with parent compound operation
            this._operations.Add(new CompoundOperationChild<TOpSettings>(op, parentName));
        }
        return this;
    }

    /// <summary>
    ///     Add an operation to the queue with explicit settings from the profile.
    /// </summary>
    public OperationQueue<TProfile> Add<TOpSettings>(
        IOperation<TOpSettings> operation,
        TOpSettings settings
    ) where TOpSettings : class, IOperationSettings, new() {
        // Extract settings from profile using the selector
        operation.Settings = settings;

        if (operation.Settings == null) {
            throw new InvalidOperationException(
                $"Operation '{operation.GetType().Name}' requires settings of type '{typeof(TOpSettings).Name}', " +
                $"but the settings selector returned null.");
        }

        this._operations.Add(operation);
        return this;
    }

    /// <summary>
    ///     Get metadata about all queued operations for frontend display
    /// </summary>
    public List<OperationMetadata> GetOperationMetadata() {
        var batches = this.OperationBatches(this._operations);
        var metadata = new List<OperationMetadata>();
        var batchIndex = 0;

        foreach (var batch in batches) {
            foreach (var op in batch.Operations)
                metadata.Add(new OperationMetadata(op.GetType().Name, op.Description, op.Type, batchIndex));
            batchIndex++;
        }

        return metadata;
    }

    /// <summary>
    ///     Converts the queued operations into optimized family document callbacks
    /// </summary>
    public (Action<Document>[] actions, Func<List<OperationLog>> getLogs) ToFamilyActions() {
        var batches = this.OperationBatches(this._operations);
        var familyActions = new List<Action<Document>>();
        var allLogs = new List<OperationLog>();

        foreach (var batch in batches) {
            switch (batch.Type) {
            case OperationType.Doc:
                familyActions.Add(famDoc => {
                    foreach (var op in batch.Operations) {
                        var sw = Stopwatch.StartNew();
                        var log = op.Execute(famDoc);
                        sw.Stop();
                        log.MsTotalElapsed = sw.Elapsed.TotalMilliseconds;
                        allLogs.Add(log);
                    }
                });
                break;

            case OperationType.Type:
                familyActions.Add(famDoc => {
                    var fm = famDoc.FamilyManager;
                    var familyTypes = fm.Types.Cast<FamilyType>().ToList();

                    // Create one log per operation that aggregates all type executions
                    foreach (var op in batch.Operations) {
                        string operationName = null;
                        var aggregatedEntries = new List<LogEntry>();
                        var sw = Stopwatch.StartNew();

                        foreach (var famType in familyTypes) {
                            fm.CurrentType = famType;
                            var typeLog = op.Execute(famDoc);
                            operationName = typeLog.OperationName;

                            // Extract type name and add entries with context
                            var typeName = famType.Name;
                            aggregatedEntries.AddRange(
                                typeLog.Entries.Select(entry => new LogEntry {
                                    Item = entry.Item,
                                    Context = typeName,
                                    Error = entry.Error
                                }));
                        }

                        sw.Stop();
                        var aggregatedLog = new OperationLog(operationName) {
                            Entries = aggregatedEntries,
                            MsTotalElapsed = sw.Elapsed.TotalMilliseconds,
                            MsAvgPerType = sw.Elapsed.TotalMilliseconds / familyTypes.Count
                        };
                        allLogs.Add(aggregatedLog);
                    }
                });
                break;
            }
        }

        return (familyActions.ToArray(), () => allLogs);
    }

    private List<OperationBatch> OperationBatches(List<IOperation> operations) {
        var batches = new List<OperationBatch>();
        var currentBatch = new List<IOperation>();
        OperationType? currentType = null;

        foreach (var op in operations) {
            if (currentType != null && currentType != op.Type) {
                // Flush current batch
                batches.Add(new OperationBatch(currentType.Value, currentBatch));
                currentBatch = new List<IOperation>();
            }

            currentBatch.Add(op);
            currentType = op.Type;
        }

        // Flush remaining
        if (currentBatch.Count > 0 && currentType != null)
            batches.Add(new OperationBatch(currentType.Value, currentBatch));

        return batches;
    }
}

internal record OperationBatch(OperationType Type, List<IOperation> Operations);

/// <summary>
///     Wrapper that prefixes log operation names with a parent name (for compound operations)
/// </summary>
internal class CompoundOperationChild<TSettings> : IOperation<TSettings> where TSettings : IOperationSettings {
    private readonly IOperation<TSettings> _innerOperation;
    private readonly string _parentName;

    public CompoundOperationChild(IOperation<TSettings> innerOperation, string parentName) {
        this._innerOperation = innerOperation;
        this._parentName = parentName;
    }

    public TSettings Settings {
        get => this._innerOperation.Settings;
        set => this._innerOperation.Settings = value;
    }

    public OperationType Type => this._innerOperation.Type;
    public string Description => this._innerOperation.Description;

    public OperationLog Execute(Document doc) {
        var innerLog = this._innerOperation.Execute(doc);
        // Create new log with prefixed name
        var log = new OperationLog($"{this._parentName}: {this._innerOperation.GetType().Name}") {
            Entries = innerLog.Entries,
            MsTotalElapsed = innerLog.MsTotalElapsed,
            MsAvgPerType = innerLog.MsAvgPerType
        };
        return log;
    }
}