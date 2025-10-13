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
        Func<TProfile, TOpSettings> settingsSelector)
        where TOpSettings : class, new() {
        // Extract settings from profile using the selector
        operation.Settings = settingsSelector(this._profile);

        if (operation.Settings == null) {
            throw new InvalidOperationException(
                $"Operation '{operation.Name}' requires settings of type '{typeof(TOpSettings).Name}', " +
                $"but the settings selector returned null.");
        }

        this._operations.Add(operation);
        return this;
    }

    /// <summary>
    ///     Add an operation to the queue with explicit settings from the profile.
    /// </summary>
    public OperationQueue<TProfile> Add<TOpSettings>(
        IOperation<TOpSettings> operation,
        TOpSettings settings)
        where TOpSettings : class, new() {
        // Extract settings from profile using the selector
        operation.Settings = settings;

        if (operation.Settings == null) {
            throw new InvalidOperationException(
                $"Operation '{operation.Name}' requires settings of type '{typeof(TOpSettings).Name}', " +
                $"but the settings selector returned null.");
        }

        this._operations.Add(operation);
        return this;
    }

    /// <summary>
    ///     Get metadata about all queued operations for frontend display
    /// </summary>
    public List<OperationMetadata> GetOperationMetadata() {
        var batches = this.BatchOperations(this._operations);
        var metadata = new List<OperationMetadata>();
        var batchIndex = 0;

        foreach (var batch in batches) {
            foreach (var op in batch.Operations)
                metadata.Add(new OperationMetadata(op.Name, op.Description, op.Type, batchIndex));
            batchIndex++;
        }

        return metadata;
    }

    /// <summary>
    ///     Converts the queued operations into optimized family document callbacks
    /// </summary>
    public Action<Document>[] ToFamilyActions() {
        var batches = this.BatchOperations(this._operations);
        var familyActions = new List<Action<Document>>();

        foreach (var batch in batches) {
            switch (batch.Type) {
            case OperationType.Doc:
                familyActions.Add(famDoc => {
                    foreach (var op in batch.Operations) op.Execute(famDoc);
                });
                break;

            case OperationType.Type:
                familyActions.Add(famDoc => {
                    var fm = famDoc.FamilyManager;
                    var familyTypes = fm.Types.Cast<FamilyType>().ToList();

                    foreach (var famType in familyTypes) {
                        fm.CurrentType = famType;
                        foreach (var op in batch.Operations) op.Execute(famDoc);
                    }
                });
                break;
            }
        }

        return familyActions.ToArray();
    }

    private List<OperationBatch> BatchOperations(List<IOperation> operations) {
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