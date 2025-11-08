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
            if (log == null) log = new OperationLog("IGNORE", new List<LogEntry>());
            sw.Stop();
            log.MsElapsed = sw.Elapsed.TotalMilliseconds;
            return [log];
        } catch (Exception ex) {
            return [
                new OperationLog(
                    $"{this.Name}: (FATAL ERROR)",
                    [new LogEntry { Item = ex.GetType().Name, Error = ex.Message }])
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
            return [new OperationLog(this.Name, [new LogEntry { Item = ex.GetType().Name, Error = ex.Message }])];
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
            return [
                new OperationLog(
                    $"Operation {currOpName ?? "Unknown Operation"} (FATAL ERROR)",
                    [new LogEntry { Item = currFamTypeName ?? "Unknown Family Type", Error = ex.Message }])
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
///     Container for grouping related operations that share settings.
///     Groups are not operations themselves - they are unwrapped into individual operations when added to the queue.
///     The name is automatically derived from the type name.
/// </summary>
public class OperationGroup<TSettings> where TSettings : IOperationSettings {
    /// <summary>
    ///     Creates an operation group with the name automatically derived from the type name.
    /// </summary>
    protected OperationGroup(string description, List<IOperation<TSettings>> operations) {
        // Automatically derive name from the actual type (not OperationGroup<TSettings>)
        this.Description = description;
        this.Operations = operations;
    }

    public string Name => this.GetType().Name;
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
    public int SuccessCount => this.Entries.Count(e => e.Error is null);
    public int FailedCount => this.Entries.Count - this.SuccessCount;
}

/// <summary>
///     Individual log entry for an operation
/// </summary>
public class LogEntry {
    public string Item { get; init; }
    public string Context { get; set; }
    public string Error { get; init; }
}