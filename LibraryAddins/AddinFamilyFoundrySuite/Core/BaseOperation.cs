namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Base interface for all operations. Provides metadata and execution.
/// </summary>
public interface IOperation {
    /// <summary>
    ///     The type of operation to perform, either a document-level operation or a type-level operation.
    ///     Document-level operations are executed on the entire family document all at once.
    ///     Type-level operations are executed for each type in the family document.
    ///     The OperationEnqueuer batches consecutive type-operations for better performance.
    /// </summary>
    OperationType Type { get; }

    /// <summary>
    ///     The name of the operation to perform.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     The description of the operation to perform.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Execute the operation.
    /// </summary>
    void Execute(Document doc);
}

public interface IOperation<TSettings> : IOperation {
    TSettings Settings { get; set; }
}

public enum OperationType {
    Doc,
    Type
}

/// <summary>
///     Metadata about an operation for frontend display and debugging.
/// </summary>
public record OperationMetadata(
    string Name,
    string Description,
    OperationType Type,
    int BatchGroup
);