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
    OperationLog Execute(Document doc, FamilyType typeContext = null);
}

public interface IOperation<TSettings> : IOperation where TSettings : IOperationSettings {
    TSettings Settings { get; set; }
}

public interface IOperationSettings {
    bool Enabled { get; init; }
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

/// <summary>
///     Log result from an operation execution
/// </summary>
public class OperationLog {
    public string OperationName { get; init; }
    public List<LogEntry> Entries { get; init; } = new();
    public double MsTotalElapsed { get; set; }
    public double? MsAvgPerType { get; set; } = null;

    public int SuccessCount => this.Entries.Count(e => e.Error is null);
    public int FailedCount => this.Entries.Count - this.SuccessCount;
}

/// <summary>
///     Individual log entry for an operation
/// </summary>
public class LogEntry {
    public string Item { get; init; }
    public FamilyType Context { get; init; } = null;
    public string Error { get; init; } = null;
}