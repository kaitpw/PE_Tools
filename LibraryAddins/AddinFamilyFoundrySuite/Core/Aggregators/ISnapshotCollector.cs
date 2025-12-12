using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Generic collector interface. TInput varies by context:
///     - (Document, Family) for project-based collection
///     - FamilyDocument for family-doc-based collection
/// </summary>
public interface ISnapshotCollector<in TInput> {
    void Collect(TInput input, FamilySnapshot snapshot);
}

// Convenience interfaces for the two collection contexts
public interface IProjectSnapshotCollector : ISnapshotCollector<(Document doc, Family family)> {
}

public interface IFamilyDocSnapshotCollector : ISnapshotCollector<FamilyDocument> {
}