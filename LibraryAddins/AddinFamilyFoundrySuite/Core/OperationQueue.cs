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
        operation.Name = operation.GetType().Name;
        operation.Settings = settingsSelector(this._profile);
        if (operation.Settings == null || !operation.Settings.Enabled) return this;

        this._operations.Add(operation);
        return this;
    }

    /// <summary>
    ///     Add a compound operation to the queue with explicit settings from the profile.
    /// </summary>
    public OperationQueue<TProfile> Add<TOpSettings>(
        ICompoundOperation<TOpSettings> compoundOperation,
        Func<TProfile, TOpSettings> settingsSelector
    ) where TOpSettings : class, IOperationSettings, new() {
        foreach (var operation in compoundOperation.Operations) {
            operation.Name = $"{compoundOperation.Name}: {operation.GetType().Name}";
            operation.Settings = settingsSelector(this._profile);
            if (operation.Settings == null || !operation.Settings.Enabled) continue;
            this._operations.Add(new CompoundOperationChild<TOpSettings>(operation));
        }

        return this;
    }

    /// <summary>
    ///     Add an operation to the queue with ad-hoc settings.
    /// </summary>
    public OperationQueue<TProfile> Add<TOpSettings>(
        IOperation<TOpSettings> operation,
        TOpSettings settings
    ) where TOpSettings : class, IOperationSettings, new() {
        operation.Name = "Ad-hoc: " + operation.GetType().Name;
        operation.Settings = settings;
        if (operation.Settings == null || !operation.Settings.Enabled) return this;

        this._operations.Add(operation);
        return this;
    }

    /// <summary>
    ///     Get metadata about all queued operations for frontend display
    /// </summary>
    public List<(string Name, string Description, OperationType Type, int BatchGroup)> GetOperationMetadata() {
        var batches = this.OperationBatches(this._operations);
        var metadata = new List<(string Name, string Description, OperationType Type, int BatchGroup)>();
        var batchIndex = 0;

        foreach (var batch in batches) {
            foreach (var op in batch.Operations) {
                var name = GetOperationName(op);
                metadata.Add((name, op.Description, op.Type, batchIndex));
            }

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
                        log.MsElapsed = sw.Elapsed.TotalMilliseconds;
                        allLogs.Add(log);
                    }
                });
                break;

            case OperationType.Type:
                familyActions.Add(famDoc => {
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

    // Check if the operation explicitly implements Name property, otherwise use type name
    private static string GetOperationName(IOperation op) =>
        op.GetType().GetProperty(nameof(IOperation.Name))?.GetValue(op) as string ?? op.GetType().Name;

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

    public CompoundOperationChild(IOperation<TSettings> innerOperation) {
        this._innerOperation = innerOperation;
        this.Name = innerOperation.Name;
    }

    public string Name { get; set; }

    public TSettings Settings {
        get => this._innerOperation.Settings;
        set => this._innerOperation.Settings = value;
    }

    public OperationType Type => this._innerOperation.Type;
    public string Description => this._innerOperation.Description;

    public OperationLog Execute(Document doc) {
        var innerLog = this._innerOperation.Execute(doc);
        return new OperationLog(this.Name, innerLog.Entries);
    }
}