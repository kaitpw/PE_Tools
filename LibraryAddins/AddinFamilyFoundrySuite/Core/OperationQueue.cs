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
    public List<(string Name, string Description, string Type, string IsMerged)> GetOperationMetadata() {
        var ops = this.ToTypeOptimizedList();
        var result = new List<(string Name, string Description, string Type, string IsMerged)>();
        foreach (var op in ops) {
            switch (op) {
            case MergedTypeOperation mergedOp:
                result.AddRange(mergedOp.Operations.Select(o => (o.Name, o.Description, GetOperationType(o), "Merged")));
                break;
            case TypeOperation<IOperationSettings> typeOp:
                result.Add((typeOp.Name, typeOp.Description, GetOperationType(typeOp), "Single"));
                break;
            case DocOperation<IOperationSettings> docOp:
                result.Add((docOp.Name, docOp.Description, GetOperationType(docOp), "Single"));
                break;
            default:
                throw new InvalidOperationException($"Unknown operation type: {op.GetType().Name}");
            }
        }
        return result;
    }



    private static string GetOperationType(IOperation op) {
        var opType = op.GetType().BaseType.GetGenericTypeDefinition();
        if (opType == typeof(DocOperation<>)) return "Doc";
        if (opType == typeof(TypeOperation<>)) return "Type";
        throw new InvalidOperationException(
            $"Operation {op.GetType().Name} does not inherit from DocOperation or TypeOperation");
    }

    public List<IExecutable> ToTypeOptimizedList() {
        var finalOps = new List<IExecutable>();
        var currentBatch = new List<IOperation>();

        foreach (var op in this._operations) {
            var opType = op.GetType().BaseType.GetGenericTypeDefinition();
            if (opType == typeof(TypeOperation<>)) {
                currentBatch.Add(op);
            } else {
                if (currentBatch.Count > 0) {
                    finalOps.Add(new MergedTypeOperation(currentBatch));
                    currentBatch = new List<IOperation>();
                }
                finalOps.Add(op as IExecutable);
            }
        }

        // Flush remaining
        if (currentBatch.Count > 0)
            finalOps.Add(new MergedTypeOperation(currentBatch));

        return finalOps;
    }

    public List<IOperation> ToList() => this._operations;
}
