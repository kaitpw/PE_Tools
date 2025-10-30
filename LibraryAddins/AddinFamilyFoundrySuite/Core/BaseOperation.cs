namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Base interface for all operations. Provides metadata and execution.
///     This interface is necessary to enable abstractions like the OperationQueue and OperationProcessor.
/// </summary>
public interface IOperation {
    /// <summary>
    ///     The name of the operation, set by OperationProcessor.
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

/// <summary>
///     Base interface for operations with settings. It is typed with the settings type.
/// </summary>
public interface IOperation<TSettings> : IOperation where TSettings : IOperationSettings {
    /// <summary>
    ///     The settings for the operation.
    /// </summary>
    TSettings Settings { get; set; }
}

public interface IOperationSettings {
    bool Enabled { get; init; }
}

/// <summary>
///     Base abstract class for document-level operations.
///     Document-level operations are executed on the entire family document all at once.
/// </summary>
public abstract class DocOperation<TSettings> : IOperation<TSettings> where TSettings : IOperationSettings {
    public string Name { get; set; }
    public TSettings Settings { get; set; }
    public abstract string Description { get; }
    public abstract OperationLog Execute(Document doc);
}

/// <summary>
///     Base abstract class for type-level operations.
///     Type-level operations are executed for each type in the family document.
///     The OperationEnqueuer batches consecutive type-operations for better performance.
/// </summary>
public abstract class TypeOperation<TSettings> : IOperation<TSettings> where TSettings : IOperationSettings {
    public string Name { get; set; }
    public TSettings Settings { get; set; }
    public abstract string Description { get; }
    public abstract OperationLog Execute(Document doc);
}

/// <summary>
///     Container for grouping related operations that share settings.
///     Groups are not operations themselves - they are unwrapped into individual operations when added to the queue.
///     The name is automatically derived from the type name.
/// </summary>
public class OperationGroup<TSettings> where TSettings : IOperationSettings {
    public string Description { get; init; }
    public List<IOperation<TSettings>> Operations { get; init; }

    /// <summary>
    ///     Creates an operation group with the name automatically derived from the type name.
    /// </summary>
    protected OperationGroup(string description, List<IOperation<TSettings>> operations) {
        // Automatically derive name from the actual type (not OperationGroup<TSettings>)
        this.Description = description;
        this.Operations = operations;
    }
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