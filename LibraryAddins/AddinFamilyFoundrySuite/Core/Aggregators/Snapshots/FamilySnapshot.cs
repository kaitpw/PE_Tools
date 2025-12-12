namespace AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;

/// <summary>
/// Container for all snapshot data collected from a family.
/// Designed to be extensible - future collectors add their sections here.
/// </summary>
public class FamilySnapshot {
    public required string FamilyName { get; init; }
    public DateTime CollectedAt { get; init; } = DateTime.Now;

    // Parameter snapshots (current scope)
    public List<ParamSnapshot> Parameters { get; set; } = [];
}
