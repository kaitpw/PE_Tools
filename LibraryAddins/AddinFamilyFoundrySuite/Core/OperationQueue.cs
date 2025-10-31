namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Fluent processor that batches document and type operations for optimal execution
/// </summary>
public class OperationQueue {
    private readonly List<IOperation> _operations = new();

    public OperationQueue Add<TOpSettings>(
        IOperation<TOpSettings> operation,
        bool internalOperation = false
    ) where TOpSettings : class, IOperationSettings, new() {
        if (operation.Settings?.Enabled == false) return this;
        if (internalOperation) operation.Name = $"INTERNAL OPERATION: {operation.Name}";
        this._operations.Add(operation);
        return this;
    }

    /// <summary>
    ///     Add an operation group to the queue with explicit settings from the profile.
    ///     Groups are unwrapped into individual operations, with names prefixed by the group name.
    /// </summary>
    public OperationQueue Add<TOpSettings>(
        OperationGroup<TOpSettings> group
    ) where TOpSettings : class, IOperationSettings, new() {
        foreach (var operation in group.Operations) {
            operation.Name = $"{group.Name}: {operation.Name}";
            if (operation.Settings?.Enabled == false) continue;
            this._operations.Add(operation);
        }

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
                result.AddRange(mergedOp.Operations.Select(o =>
                    (o.Name, o.Description, GetOperationType(o), "Merged")));
                break;
            case TypeOperation typeOp:
                result.Add((typeOp.Name, typeOp.Description, GetOperationType(typeOp), "Single"));
                break;
            case DocOperation docOp:
                result.Add((docOp.Name, docOp.Description, GetOperationType(docOp), "Single"));
                break;
            default:
                throw new InvalidOperationException($"Unknown operation type: {op.GetType().Name}");
            }
        }

        return result;
    }


    private static string GetOperationType(IOperation op) {
        var opType = op.GetType();
        if (typeof(DocOperation).IsAssignableFrom(opType)) return "Doc";
        if (typeof(TypeOperation).IsAssignableFrom(opType)) return "Type";
        throw new InvalidOperationException(
            $"Operation {op.GetType().Name} does not inherit from DocOperation or TypeOperation");
    }

    public List<IActionable> ToTypeOptimizedList() {
        var finalOps = new List<IActionable>();
        var currentBatch = new List<TypeOperation>();

        foreach (var op in this._operations) {
            if (typeof(TypeOperation).IsAssignableFrom(op.GetType()))
                currentBatch.Add(op as TypeOperation);
            else {
                if (currentBatch.Count > 0) {
                    finalOps.Add(new MergedTypeOperation(currentBatch));
                    currentBatch = [];
                }

                finalOps.Add(op);
            }
        }

        // Flush remaining
        if (currentBatch.Count > 0)
            finalOps.Add(new MergedTypeOperation(currentBatch));

        return finalOps;
    }

    /// <summary>
    ///     Converts the queued operations into family actions, optionally bundling them for single-transaction behavior.
    /// </summary>
    /// <param name="optimizeTypeOperations">
    ///     If true, optimizes type operations for better performance. If false, runs all
    ///     operations on a one-to-one basis.
    /// </param>
    /// <param name="singleTransaction">
    ///     If true, bundles all actions into a single action for one transaction. If false, each
    ///     action runs in its own transaction.
    /// </param>
    /// <returns>An array of family actions that return logs when executed.</returns>
    public Func<Document, List<OperationLog>>[] ToFamilyActions(bool optimizeTypeOperations = true,
        bool singleTransaction = true) {
        var executableOps = optimizeTypeOperations
            ? this.ToTypeOptimizedList()
            : this._operations.Cast<IActionable>().ToList();
        var actions = this.ConvertToActions(executableOps);

        return singleTransaction
            ? this.BundleFamilyActions(actions)
            : actions;
    }

    /// <summary>
    ///     Converts a list of executable operations into family actions on a one-to-one basis.
    ///     Batched actions are faster if there are consecutive TypeOperations in a processing cycle.
    ///     However it allows you to set a transaction boundary around each operation.
    /// </summary>
    private Func<Document, List<OperationLog>>[] ConvertToActions(List<IActionable> operations) =>
        operations.Select(op => op.ToAction()).ToArray();

    /// <summary>
    ///     Bundles all family actions into a single action to replicate single-transaction behavior.
    ///     When ProcessFamily receives this single action, it will run all operations within one transaction.
    /// </summary>
    private Func<Document, List<OperationLog>>[] BundleFamilyActions(
        Func<Document, List<OperationLog>>[] actions) {
        if (actions.Length == 0) return actions;

        // Create a single action that executes all actions sequentially and collects logs
        List<OperationLog> BundleActions(Document famDoc) {
            var allLogs = new List<OperationLog>();
            foreach (var action in actions) allLogs.AddRange(action(famDoc));
            return allLogs;
        }

        return [BundleActions];
    }
}