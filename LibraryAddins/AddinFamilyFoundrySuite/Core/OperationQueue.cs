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
    ///     Add an operation group to the queue with explicit settings from the profile.
    ///     Groups are unwrapped into individual operations, with names prefixed by the group name.
    /// </summary>
    public OperationQueue<TProfile> Add<TOpSettings>(
        OperationGroup<TOpSettings> group,
        Func<TProfile, TOpSettings> settingsSelector
    ) where TOpSettings : class, IOperationSettings, new() {
        foreach (var operation in group.Operations) {
            operation.Name = $"{group.GetType().Name}: {operation.GetType().Name}";
            operation.Settings = settingsSelector(this._profile);
            if (operation.Settings == null || !operation.Settings.Enabled) continue;
            this._operations.Add(operation);
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
        var batches = this.GetOperationBatches();
        var metadata = new List<(string Name, string Description, OperationType Type, int BatchGroup)>();
        var batchIndex = 0;

        foreach (var batch in batches) {
            foreach (var op in batch.Operations) {
                var name = GetOperationName(op);
                metadata.Add((name, op.Description, GetOperationType(op), batchIndex));
            }

            batchIndex++;
        }

        return metadata;
    }

    // Check if the operation explicitly implements Name property, otherwise use type name
    private static string GetOperationName(IOperation op) =>
        op is IOperation<IOperationSettings> opWithSettings
            ? opWithSettings.Name
            : op.GetType().Name;

    private static OperationType GetOperationType(IOperation op) {
        var opType = op.GetType().BaseType.GetGenericTypeDefinition();
        if (opType == typeof(DocOperation<>)) return OperationType.Doc;
        if (opType == typeof(TypeOperation<>)) return OperationType.Type;
        throw new InvalidOperationException(
            $"Operation {op.GetType().Name} does not inherit from DocOperation or TypeOperation");
    }

    public List<OperationBatch> GetOperationBatches() {
        var batches = new List<OperationBatch>();
        var currentBatch = new List<IOperation>();
        OperationType? currentType = null;

        foreach (var op in this._operations) {
            var opType = GetOperationType(op);
            if (currentType != null && currentType != opType) {
                // Flush current batch
                batches.Add(new OperationBatch(currentType.Value, currentBatch));
                currentBatch = new List<IOperation>();
            }

            currentBatch.Add(op);
            currentType = opType;
        }

        // Flush remaining
        if (currentBatch.Count > 0 && currentType != null)
            batches.Add(new OperationBatch(currentType.Value, currentBatch));

        return batches;
    }

    public List<IOperation> GetOperations() => this._operations;
}

public record OperationBatch(OperationType Type, List<IOperation> Operations);