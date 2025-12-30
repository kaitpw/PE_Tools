using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;

/// <summary>
///     Canonical parameter snapshot - single source of truth for:
///     - Parameter definition (can recreate the param)
///     - Assignment mode (formula vs values)
///     - Per-type values (audit + replay)
/// </summary>
public record ParamSnapshot {
    // Identity
    [Required] public required string Name { get; init; }
    [Required] public required bool IsInstance { get; init; }

    // Definition (enough to create the parameter)
    public ForgeTypeId PropertiesGroup { get; init; } = new("");
    public ForgeTypeId DataType { get; init; } = SpecTypeId.String.Text;

    // Assignment mode - if Formula != null, it is the authoritative assignment
    public string Formula { get; init; } = null;

    // Per-type values: TypeName -> setter-acceptable string value
    // Null/empty means no value for that type. TODO: verify that this doesn't serialize null as empty string
    public Dictionary<string, string> ValuesPerType { get; init; } = new(StringComparer.Ordinal);

    // Audit metadata (not required for replay, but useful)
    public bool IsBuiltIn { get; init; } = false;
    public Guid? SharedGuid { get; init; } = null;
    public StorageType? StorageType { get; init; } = null;

    /// <summary>
    ///     Indicates if this is a project parameter (exists in Document.ParameterBindings).
    ///     Only populated when collecting from project document. Always false for family doc collection.
    /// </summary>
    public bool IsProjectParameter { get; init; } = false;
}