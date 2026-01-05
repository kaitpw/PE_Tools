using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;

namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Inter-operation state container for coordinating log entries across operations within an OperationGroup.
///     Created by OperationGroup, reset per-family by the OperationProcessor to ensure clean state.
/// </summary>
public class OperationContext {
    private readonly Dictionary<string, LogEntry> _entries = new();
    private readonly HashSet<string> _touchedThisOperation = new();

    public IEnumerable<LogEntry> All => this._entries.Values;
    public IEnumerable<LogEntry> Pending => this.All.Where(e => !e.IsComplete);

    public LogEntry GetOrCreate(string name) {
        if (this._entries.TryGetValue(name, out var entry)) {
            // Only mark as touched if not already complete (operation is modifying it)
            if (!entry.IsComplete)
                _ = this._touchedThisOperation.Add(name);
            return entry;
        }

        // New entry - definitely touched
        _ = this._touchedThisOperation.Add(name);
        return this._entries[name] = new LogEntry(name);
    }

    public LogEntry Get(string name) =>
        this._entries.GetValueOrDefault(name);

    /// <summary>
    ///     Gets a snapshot of logs touched by the current operation, then clears the touched set.
    ///     Clones LogEntry objects to prevent Context pollution from TypeOperations.
    ///     Clears messages from the original entries after cloning to prevent accumulation across types.
    /// </summary>
    public List<LogEntry> TakeSnapshot() {
        var snapshot = this._touchedThisOperation
            .Select(name => {
                var entry = this._entries[name];
                var clone = entry.Clone();
                // Clear messages from original entry to prevent accumulation across types
                entry.ClearMessages();
                return clone;
            })
            .ToList();
        this._touchedThisOperation.Clear();
        return snapshot;
    }

    public void Reset() {
        this._entries.Clear();
        this._touchedThisOperation.Clear();
    }
}

/// <summary>
///     Context for a single family's processing run. Properties populated by pipeline and immutable after completion.
/// </summary>
public class FamilyProcessingContext {
    public required string FamilyName { get; init; }

    /// <summary>Snapshot collected before processing.</summary>
    public FamilySnapshot PreProcessSnapshot { get; internal set; }

    /// <summary>Snapshot collected after processing.</summary>
    public FamilySnapshot PostProcessSnapshot { get; internal set; }

    /// <summary>Operation logs from processing, or an error if processing failed.</summary>
    public Result<List<OperationLog>> OperationLogs { get; internal set; }

    /// <summary>Total processing time in milliseconds.</summary>
    public double TotalMs { get; internal set; }


    /// <summary>Finds a parameter in the pre-process snapshot by name.</summary>
    public ParamSnapshot FindParam(string paramName) {
        var parameters = this.PreProcessSnapshot?.Parameters?.Data;
        if (parameters is null || parameters.Count == 0)
            return null;

        return parameters
            .Where(p => string.Equals(p.Name, paramName, StringComparison.Ordinal))
            .OrderByDescending(this.CountTypesWithValue)
            .FirstOrDefault();
    }

    /// <summary>Checks if a parameter has a (non-empty) value for all family types.</summary>
    public bool HasValueForAllTypes(string paramName) =>
        this.HasValueForAllTypes(this.FindParam(paramName));

    /// <summary>Checks if a parameter has a (non-empty) value for all family types.</summary>
    public bool HasValueForAllTypes(ParamSnapshot p) {
        if (p is null) return false;
        var familyTypes = p.ValuesPerType.Count;
        if (familyTypes == 0) return false;
        return familyTypes == this.CountTypesWithValue(p);
    }

    /// <summary>Gets the count of types that have a value for the specified parameter.</summary>
    public int CountTypesWithValue(string paramName) =>
        this.CountTypesWithValue(this.FindParam(paramName));

    /// <summary>Gets the count of types that have a value for the specified parameter.</summary>
    public int CountTypesWithValue(ParamSnapshot p) => this.GetTypesWithValue(p).Count;

    /// <summary>Gets the list of family types that have a value for the specified parameter.</summary>
    public List<string> GetTypesWithValue(string paramName) =>
        this.GetTypesWithValue(this.FindParam(paramName));

    /// <summary>Gets the list of family types that have a value for the specified parameter.</summary>
    public List<string> GetTypesWithValue(ParamSnapshot p) {
        if (p is null
            || this.PreProcessSnapshot?.Parameters?.Data == null
            || this.PreProcessSnapshot.Parameters.Data.Count == 0)
            return [];

        return string.IsNullOrWhiteSpace(p.Formula)
            ? p.ValuesPerType
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => kv.Key)
                .ToList()
            : p.ValuesPerType.Keys.ToList();
    }
}