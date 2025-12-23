using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core;

public interface IExecutable {
    Func<FamilyDocument, List<OperationLog>> ToFunc();
}

public interface IOperation : IExecutable {
    string Name { get; set; }
}

/// <summary>
///     Base abstract class for document-level operations.
///     Document-level operations are executed on the entire family document all at once.
/// </summary>
public abstract class DocOperation : IOperation {
    private string _nameOverride;

    public abstract string Description { get; }

    /// <summary>
    ///     Gets the operation name. Returns the type name by default, or the override value if set.
    ///     Setting a value creates an override that will be returned instead of the type name.
    /// </summary>
    public string Name {
        get => this._nameOverride ?? this.GetType().Name;
        set => this._nameOverride = value;
    }

    public Func<FamilyDocument, List<OperationLog>> ToFunc() => famDoc => {
        try {
            var sw = Stopwatch.StartNew();
            var log = this.Execute(famDoc);
            log ??= new OperationLog("IGNORE", []);
            sw.Stop();
            log.MsElapsed = sw.Elapsed.TotalMilliseconds;
            return [log];
        } catch (Exception ex) {
            return [
                new OperationLog(
                    $"{this.Name}: (FATAL ERROR)",
                    [new LogEntry(ex.GetType().Name).Error(ex)])
            ];
        }
    };

    public abstract OperationLog Execute(FamilyDocument doc);
}

/// <summary>
///     Base abstract class for type-level operations.
///     Type-level operations are executed for each type in the family document.
///     The OperationEnqueuer batches consecutive type-operations for better performance.
/// </summary>
public abstract class TypeOperation : IOperation {
    private string _nameOverride;

    public abstract string Description { get; }

    /// <summary>
    ///     Gets the operation name. Returns the type name by default, or the override value if set.
    ///     Setting a value creates an override that will be returned instead of the type name.
    /// </summary>
    public string Name {
        get => this._nameOverride ?? this.GetType().Name;
        set => this._nameOverride = value;
    }

    public Func<FamilyDocument, List<OperationLog>> ToFunc() => famDoc => {
        try {
            var fm = famDoc.FamilyManager;
            var typeLogs = new List<OperationLog>();

            // Loop over types and execute the operation for each type
            foreach (FamilyType famType in fm.Types) {
                var swType = Stopwatch.StartNew();
                fm.CurrentType = famType;
                var typeLog = this.Execute(famDoc);
                swType.Stop();

                typeLog.MsElapsed = swType.Elapsed.TotalMilliseconds;
                foreach (var entry in typeLog.Entries) entry.Context = famType.Name;
                typeLogs.Add(typeLog);
            }

            return [
                new OperationLog(
                    this.Name,
                    typeLogs.SelectMany(log => log.Entries).ToList()
                ) { MsElapsed = typeLogs.Sum(log => log.MsElapsed) }
            ];
        } catch (Exception ex) {
            return [new OperationLog(this.Name, [new LogEntry(ex.GetType().Name).Error(ex)])];
        }
    };

    public abstract OperationLog Execute(FamilyDocument doc);
}

public class MergedTypeOperation(List<TypeOperation> operations) : IExecutable {
    public List<TypeOperation> Operations { get; set; } = operations;

    public Func<FamilyDocument, List<OperationLog>> ToFunc() => famDoc => {
        string currFamTypeName = null;
        string currOpName = null;
        try {
            var fm = famDoc.FamilyManager;
            var operationLogs = new List<OperationLog>();

            // Switch types once, executing all operations per type
            foreach (FamilyType famType in fm.Types) {
                currFamTypeName = famType.Name;
                var typeSwitchSw = Stopwatch.StartNew();
                fm.CurrentType = famType;
                typeSwitchSw.Stop();
                var amortizedSwitchMs = typeSwitchSw.Elapsed.TotalMilliseconds / this.Operations.Count;

                // Execute all operations for this type
                foreach (var op in this.Operations) {
                    currOpName = op.Name;
                    var opSw = Stopwatch.StartNew();
                    var log = op.Execute(famDoc);
                    opSw.Stop();

                    log.MsElapsed = opSw.Elapsed.TotalMilliseconds + amortizedSwitchMs;
                    foreach (var entry in log.Entries) entry.Context = currFamTypeName;
                    operationLogs.Add(log);
                }
            }

            // Combine logs by operation name
            return operationLogs
                .GroupBy(log => log.OperationName)
                .Select(group => new OperationLog(group.Key, group.SelectMany(log => log.Entries).ToList()) {
                    MsElapsed = group.Sum(log => log.MsElapsed)
                })
                .ToList();
        } catch (Exception ex) {
            var errorLog = new LogEntry(currFamTypeName ?? "Unknown Family Type").Error(ex);
            return [
                new OperationLog(
                    $"Operation {currOpName ?? "Unknown Operation"} (FATAL ERROR)",
                    [errorLog])
            ];
        }
    };
}

public interface IOperationSettings {
    bool Enabled { get; init; }
}

public class DefaultOperationSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
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

public abstract class DocOperation<TOpSettings>(TOpSettings settings) : DocOperation, IOperation<TOpSettings>
    where TOpSettings : IOperationSettings {
    public TOpSettings Settings { get; set; } = settings;
}

public abstract class TypeOperation<TOpSettings>(TOpSettings settings) : TypeOperation, IOperation<TOpSettings>
    where TOpSettings : IOperationSettings {
    public TOpSettings Settings { get; set; } = settings;
}

/// <summary>
///     DocOperation with FamilyProcessingContext injected.
///     Use when operation needs access to snapshots or family-level data.
/// </summary>
public abstract class DocOperationWithContext<TSettings>(TSettings settings) : DocOperation<TSettings>(settings)
    where TSettings : IOperationSettings {
    internal FamilyProcessingContext Context { get; set; }

    public sealed override OperationLog Execute(FamilyDocument doc)
        => this.Execute(doc, this.Context);

    public abstract OperationLog Execute(FamilyDocument doc, FamilyProcessingContext context);
}

/// <summary>
///     DocOperation with OperationContext (group) injected.
///     Use when operation needs to coordinate with other operations in a group.
/// </summary>
public abstract class DocOperationWithGroup<TSettings>(TSettings settings) : DocOperation<TSettings>(settings)
    where TSettings : IOperationSettings {
    internal OperationContext GroupContext { get; set; }

    public sealed override OperationLog Execute(FamilyDocument doc)
        => this.Execute(doc, this.GroupContext);

    public abstract OperationLog Execute(FamilyDocument doc, OperationContext groupContext);
}

/// <summary>
///     TypeOperation with FamilyProcessingContext injected.
///     Use when type-level operation needs access to snapshots or family-level data.
/// </summary>
public abstract class TypeOperationWithContext<TSettings>(TSettings settings) : TypeOperation<TSettings>(settings)
    where TSettings : IOperationSettings {
    internal FamilyProcessingContext Context { get; set; }

    public sealed override OperationLog Execute(FamilyDocument doc)
        => this.Execute(doc, this.Context);

    public abstract OperationLog Execute(FamilyDocument doc, FamilyProcessingContext context);
}

/// <summary>
///     TypeOperation with OperationContext (group) injected.
///     Use when type-level operation needs to coordinate with other operations in a group.
/// </summary>
public abstract class TypeOperationWithGroup<TSettings>(TSettings settings) : TypeOperation<TSettings>(settings)
    where TSettings : IOperationSettings {
    internal OperationContext GroupContext { get; set; }

    public sealed override OperationLog Execute(FamilyDocument doc)
        => this.Execute(doc, this.GroupContext);

    public abstract OperationLog Execute(FamilyDocument doc, OperationContext groupContext);
}

/// <summary>
///     Container for grouping related operations that share settings.
///     Groups are not operations themselves - they are unwrapped into individual operations when added to the queue.
///     The name is automatically derived from the type name.
///     Groups create a shared OperationContext for inter-operation coordination.
/// </summary>
public class OperationGroup<TSettings> where TSettings : IOperationSettings {
    /// <summary>
    ///     Creates an operation group with the name automatically derived from the type name.
    ///     Injects group context into operations that use context-aware base classes.
    /// </summary>
    protected OperationGroup(string description, List<IOperation<TSettings>> operations) {
        this.Description = description;
        this.Operations = operations;

        // Inject group context into operations that need it
        foreach (var op in operations) {
            switch (op) {
            case DocOperationWithGroup<TSettings> docWithGroup:
                docWithGroup.GroupContext = this.GroupContext;
                break;
            case TypeOperationWithGroup<TSettings> typeWithGroup:
                typeWithGroup.GroupContext = this.GroupContext;
                break;
            }
        }
    }

    public string Name => this.GetType().Name;

    /// <summary>
    ///     Shared context for inter-operation coordination within this group.
    ///     Reset per-family by the OperationProcessor.
    /// </summary>
    public OperationContext GroupContext { get; } = new();
    public string Description { get; init; }
    public List<IOperation<TSettings>> Operations { get; init; }
}

/// <summary>
///     Log result from an operation execution
/// </summary>
public class OperationLog(string operationName, List<LogEntry> entries) {
    public string OperationName { get; init; } = operationName;
    public List<LogEntry> Entries { get; init; } = entries;
    public double MsElapsed { get; set; }
    public int SuccessCount => this.Entries.Count(e => e.Status == LogStatus.Success);
    public int SkippedCount => this.Entries.Count(e => e.Status == LogStatus.Skipped);
    public int ErrorCount => this.Entries.Count(e => e.Status == LogStatus.Error);
    public int PendingCount => this.Entries.Count(e => e.Status == LogStatus.Pending);
}

public enum LogStatus { Pending, Success, Skipped, Error }

/// <summary>
///     Individual log entry for an operation with semantic state tracking.
/// </summary>
public class LogEntry {
    public LogEntry(string name) => this.Name = name;

    // Identity (immutable)
    public string Name { get; }
    public string Context { get; set; }  // Preserved for type-level context

    // Accumulated messages
    private List<string> MessageList { get; } = [];
    public string Message => this.MessageList.Count != 0 ? string.Join("; ", this.MessageList) : null;

    // Final state
    public LogStatus Status { get; private set; } = LogStatus.Pending;
    // public string Message { get; private set; }
    public Exception Exception { get; private set; }

    // Computed
    public bool IsComplete => this.Status != LogStatus.Pending;

    // Terminal methods (mark complete)
    public LogEntry Success(string message = null) {
        this.EnsurePending();
        this.Status = LogStatus.Success;
        if (message != null) this.MessageList.Add(message);
        return this;
    }

    public LogEntry Skip(string message = null) {
        this.EnsurePending();
        this.Status = LogStatus.Skipped;
        if (message != null) this.MessageList.Add(message);
        return this;
    }

    public LogEntry Error(Exception ex) {
        this.EnsurePending();
        this.Status = LogStatus.Error;
        this.MessageList.Add(ex.Message);
        this.Exception = ex;
        return this;
    }

    public LogEntry Error(string message) {
        this.EnsurePending();
        this.Status = LogStatus.Error;
        this.MessageList.Add(message);
        return this;
    }

    public LogEntry Error(string message, Exception ex) {
        this.EnsurePending();
        this.Status = LogStatus.Error;
        this.MessageList.Add(message);
        this.Exception = ex;
        return this;
    }

    // Non-terminal (stays Pending)
    public LogEntry Defer(string action) {
        this.EnsurePending();
        this.MessageList.Add(action);
        return this;
    }

    /// <summary>
    ///     Creates a deep clone of this LogEntry, preserving all state except Exception.
    ///     Used to snapshot logs at operation completion to prevent Context pollution.
    /// </summary>
    public LogEntry Clone() {
        var clone = new LogEntry(this.Name) {
            Context = this.Context,
            Status = this.Status,
            Exception = this.Exception
        };
        foreach (var msg in this.MessageList)
            clone.MessageList.Add(msg);
        return clone;
    }

    private void EnsurePending() {
        if (this.IsComplete)
            throw new InvalidOperationException(
                $"LogEntry '{this.Name}' is already complete with status {this.Status}");
    }
}
