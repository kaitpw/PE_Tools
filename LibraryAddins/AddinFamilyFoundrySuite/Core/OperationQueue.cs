using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Fluent processor that batches document and type operations for optimal execution
/// </summary>
public class OperationQueue {
    private readonly List<IOperation> _operations = new();

    /// <summary>
    ///     Gets all operations in the queue for inspection/injection.
    /// </summary>
    public IReadOnlyList<IOperation> Operations => this._operations;

    public OperationQueue Add(
        IOperation operation,
        bool internalOperation = false
    ) {
        if (operation.Settings?.Enabled == false) return this;
        if (internalOperation) operation.Name = $"INTERNAL OPERATION: {operation.Name}";
        this._operations.Add(operation);
        return this;
    }

    /// <summary>
    ///     Add an operation group to the queue with explicit settings from the profile.
    ///     Groups are unwrapped into individual operations, with names prefixed by the group name.
    /// </summary>
    public OperationQueue Add<TSettings>(
        OperationGroup<TSettings> group
    ) where TSettings : IOperationSettings {
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
    public List<(string Name, string Description, string Type, string IsMerged)> GetExecutableMetadata() {
        var ops = this.ToTypeOptimizedExecutableList();
        var result = new List<(string Name, string Description, string Type, string IsMerged)>();
        foreach (var op in ops) {
            switch (op) {
            case MergedTypeOperation mergedOp:
                result.AddRange(mergedOp.Operations.Select(o =>
                    (o.Name, o.Description, GetOperationType(o), "Merged")));
                break;
            case IOperation operation:
                result.Add((operation.Name, operation.Description, GetOperationType(operation), "Single"));
                break;
            default:
                throw new InvalidOperationException($"Unknown operation type: {op.GetType().Name}");
            }
        }

        return result;
    }

    public string GetExecutableMetadataString() {
        var op = this.GetExecutableMetadata();
        var result = "";
        foreach (var o in op) result += $"[Batch {o.IsMerged}] {o.Type}: {o.Name} - {o.Description}\n";
        return result;
    }

    private static string GetOperationType(IOperation op) {
        var opType = op.GetType();
        // Check if it's a generic type based on DocOperation<> or TypeOperation<>
        while (opType != null) {
            if (opType.IsGenericType) {
                var genericDef = opType.GetGenericTypeDefinition();
                if (genericDef.Name.StartsWith("DocOperation")) return "Doc";
                if (genericDef.Name.StartsWith("TypeOperation")) return "Type";
            }
            opType = opType.BaseType;
        }
        throw new InvalidOperationException(
            $"Operation {op.GetType().Name} does not inherit from DocOperation<T> or TypeOperation<T>");
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
    public Func<FamilyDocument, FamilyProcessingContext, List<OperationLog>>[] ToFuncs(
        bool optimizeTypeOperations = true,
        bool singleTransaction = true
    ) {
        var executableOps = optimizeTypeOperations
            ? this.ToTypeOptimizedExecutableList()
            : this.ToExecutableList();
        var funcs = executableOps.Select(op => op.ToFunc()).ToArray();

        return singleTransaction
            ? this.BundleFuncs(funcs)
            : funcs.ToArray();
    }

    private List<IExecutable> ToExecutableList() => [.. this._operations];

    public List<IExecutable> ToTypeOptimizedExecutableList() {
        var finalOps = new List<IExecutable>();
        var currentBatch = new List<IOperation>();

        foreach (var op in this._operations) {
            var isTypeOp = IsTypeOperation(op);

            if (isTypeOp) {
                currentBatch.Add(op);
            } else {
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

    private static bool IsTypeOperation(IOperation op) {
        var opType = op.GetType();
        while (opType != null) {
            if (opType.IsGenericType) {
                var genericDef = opType.GetGenericTypeDefinition();
                if (genericDef.Name.StartsWith("TypeOperation")) return true;
            }
            opType = opType.BaseType;
        }
        return false;
    }

    /// <summary>
    ///     Bundles all family actions into a single action to replicate single-transaction behavior.
    ///     When ProcessFamily receives this single action, it will run all operations within one transaction.
    /// </summary>
    private Func<FamilyDocument, FamilyProcessingContext, List<OperationLog>>[] BundleFuncs(
        Func<FamilyDocument, FamilyProcessingContext, List<OperationLog>>[] actions
    ) {
        if (actions.Length == 0) return actions;

        // Create a single action that executes all actions sequentially and collects logs
        List<OperationLog> BundleActions(FamilyDocument famDoc, FamilyProcessingContext context) {
            var allLogs = new List<OperationLog>();
            foreach (var action in actions) allLogs.AddRange(action(famDoc, context));
            return allLogs;
        }

        return [BundleActions];
    }
}
