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
    ///     The name of the operation, defaults to the implementing class name.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    ///     The description of the operation to perform.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Execute the operation.
    /// </summary>
    OperationLog Execute(Document doc);
}

public interface ICompoundOperation<TSettings> : IOperation where TSettings : IOperationSettings {
    List<IOperation<TSettings>> Operations { get; set; }
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
///     Log result from an operation execution
/// </summary>
public class OperationLog(string operationName, List<LogEntry> entries) {
    public string OperationName { get; init; } = operationName;
    public List<LogEntry> Entries { get; init; } = entries;
    public double MsElapsed { get; set; }
    public int SuccessCount => this.Entries.Count(e => e.Error is null);
    public int FailedCount => this.Entries.Count - this.SuccessCount;
}

/// <summary>
///     Individual log entry for an operation
/// </summary>
public class LogEntry {
    public string Item { get; init; }
    public string Context { get; set; } = null;
    public string Error { get; init; } = null;
}