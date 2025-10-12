namespace AddinFamilyFoundrySuite.Core.Operations;

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

/// <summary>
///     Strongly-typed operation with settings. Operations should inherit from Operation of TSettings.
/// </summary>
public abstract class Operation<TSettings> : IOperation
    where TSettings : class, new() {
    /// <summary>
    ///     Settings for this operation instance.
    /// </summary>
    public TSettings Settings { get; set; }

    public abstract OperationType Type { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }

    /// <summary>
    ///     Execute implementation (calls ExecuteCore with settings).
    /// </summary>
    public void Execute(Document doc) => this.ExecuteCore(doc, this.Settings);

    /// <summary>
    ///     Execute the operation with settings.
    /// </summary>
    protected abstract void ExecuteCore(Document doc, TSettings settings);
}

/// <summary>
///     Marker interface for operation settings to enable type discovery.
/// </summary>
public interface IOperationSettings {
}

/// <summary>
///     Special settings type for operations that require no configuration.
/// </summary>
public class NoSettings : IOperationSettings {
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