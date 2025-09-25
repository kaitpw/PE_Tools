namespace AddinFamilyFoundrySuite.Core;

/// <summary>
/// Fluent processor that batches document and type operations for optimal execution
/// </summary>
public class OperationEnqueuer {
    private readonly List<IOperation> _operations = new();
    public readonly Document doc;

    internal OperationEnqueuer(Document document) => doc = document;

    /// <summary>
    /// Add a (family) document-level operation to the queue
    /// </summary>
    /// <param name="action">The action to perform on the document</param>
    /// <returns>The enqueuer</returns>
    public OperationEnqueuer DocOperation(Action<Document> action) {
        this._operations.Add(new DocOperation(action));
        return this;
    }

    /// <summary>
    /// Add a family-type-level operation to the queue
    /// </summary>
    /// <param name="action">The action to perform on the type</param>
    /// <returns>The enqueuer</returns>
    public OperationEnqueuer TypeOperation(Action<Document> action) {
        this._operations.Add(new TypeOperation(action));
        return this;
    }


    /// <summary>
    /// Converts the queued operations into optimized family document callbacks for FamUtils
    /// </summary>
    /// <returns>Array of callbacks that FamUtils can execute</returns>
    public Action<Document>[] ToFamilyActions() {
        var batches = this.BatchOperations(this._operations);
        var familyActions = new List<Action<Document>>();

        foreach (var batch in batches) {
            switch (batch) {
            case DocOperationBatch docBatch:
                familyActions.Add(famDoc => {
                    foreach (var docOp in docBatch.Operations) {
                        docOp.Action(famDoc);
                    }
                });
                break;

            case TypeOperationBatch typeBatch:
                familyActions.Add(famDoc => {
                    var fm = famDoc.FamilyManager;
                    var familyTypes = fm.Types.Cast<FamilyType>().ToList(); // Evaluate once

                    foreach (var famType in familyTypes) {
                        fm.CurrentType = famType;
                        foreach (var typeOp in typeBatch.Operations) {
                            typeOp.Action(famDoc);
                        }
                    }
                });
                break;
            }
        }

        return familyActions.ToArray();
    }

    private List<IOperationBatch> BatchOperations(List<IOperation> operations) {
        var batches = new List<IOperationBatch>();
        var currentDocOps = new List<DocOperation>();
        var currentTypeOps = new List<TypeOperation>();

        foreach (var op in operations) {
            switch (op) {
            case DocOperation docOp:
                // Flush any pending type operations
                if (currentTypeOps.Count > 0) {
                    batches.Add(new TypeOperationBatch(currentTypeOps));
                    currentTypeOps = new List<TypeOperation>();
                }
                currentDocOps.Add(docOp);
                break;

            case TypeOperation typeOp:
                // Flush any pending doc operations
                if (currentDocOps.Count > 0) {
                    batches.Add(new DocOperationBatch(currentDocOps));
                    currentDocOps = new List<DocOperation>();
                }
                currentTypeOps.Add(typeOp);
                break;
            }
        }

        // Flush remaining operations
        if (currentDocOps.Count > 0) {
            batches.Add(new DocOperationBatch(currentDocOps));
        }
        if (currentTypeOps.Count > 0) {
            batches.Add(new TypeOperationBatch(currentTypeOps));
        }

        return batches;
    }
}

// Supporting interfaces and classes
public interface IOperation { }

public interface IOperationBatch { }

public record DocOperation(Action<Document> Action) : IOperation;

public record TypeOperation(Action<Document> Action) : IOperation;

public record DocOperationBatch(List<DocOperation> Operations) : IOperationBatch;

public record TypeOperationBatch(List<TypeOperation> Operations) : IOperationBatch;
