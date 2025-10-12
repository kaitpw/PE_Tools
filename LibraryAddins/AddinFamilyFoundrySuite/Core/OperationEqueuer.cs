using AddinFamilyFoundrySuite.Core.Operations;

namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Fluent processor that batches document and type operations for optimal execution
/// </summary>
public class OperationEnqueuer {
    private readonly List<IOperation> _operations = new();
    private readonly object _profile;
    public readonly Document doc;

    public OperationEnqueuer(Document document, object profile) {
        this.doc = document;
        this._profile = profile;
    }

    /// <summary>
    ///     Add a typed operation to the queue. Settings are automatically extracted from the profile.
    /// </summary>
    public OperationEnqueuer Add<TSettings>(Operation<TSettings> operation)
        where TSettings : class, new() {
        operation.Settings = this.ExtractSettings<TSettings>();
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

    private TSettings ExtractSettings<TSettings>()
        where TSettings : class, new() {
        // Special case: NoSettings means no configuration needed
        if (typeof(TSettings) == typeof(NoSettings)) return new TSettings();

        // Find property in profile by type
        var property = this._profile.GetType()
                           .GetProperties()
                           .FirstOrDefault(p => p.PropertyType == typeof(TSettings))
                       ?? throw new InvalidOperationException(
                           $"Settings type '{typeof(TSettings).Name}' not found in profile '{this._profile.GetType().Name}'.\n\n" +
                           $"Add this property to {this._profile.GetType().Name}:\n" +
                           $"    [Required]\n" +
                           $"    public {typeof(TSettings).Name} {typeof(TSettings).Name.Replace("Settings", "")} {{ get; init; }} = new();"
                       );

        return (TSettings)property.GetValue(this._profile)!;
    }
}

internal record OperationBatch(OperationType Type, List<IOperation> Operations);