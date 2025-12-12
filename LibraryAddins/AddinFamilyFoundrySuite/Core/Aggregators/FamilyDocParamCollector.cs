using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.GetValue;
using PeExtensions.PolyFill;
using System.Globalization;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Collects parameter snapshots directly from a FamilyDocument.
///     Iterates FamilyManager.Types and reads values without temporary instances.
/// </summary>
public class FamilyDocParamCollector : IFamilyDocSnapshotCollector {
    public void Collect(FamilyDocument input, FamilySnapshot snapshot) {
        var famDoc = input;
        var fm = famDoc.FamilyManager;

        var types = fm.Types.Cast<FamilyType>().ToList();
        var typeNames = types.Select(t => t.Name).Distinct(StringComparer.Ordinal).ToList();

        var familyParameters = fm.GetParameters().ToList();
        var snapshots = new Dictionary<string, ParamSnapshot>(StringComparer.Ordinal);

        foreach (var p in familyParameters) {
            var key = GetKey(p.Definition.Name, p.IsInstance);

            var isBuiltIn = ParameterUtils.IsBuiltInParameter(p.Id);
            Guid? sharedGuid = null;
            if (p.IsShared)
                try { sharedGuid = p.GUID; } catch {
                    /* GUID access can throw */
                }

            var values = typeNames.ToDictionary(t => t, _ => (string?)null, StringComparer.Ordinal);

            snapshots[key] = new ParamSnapshot {
                Name = p.Definition.Name,
                IsInstance = p.IsInstance,
                PropertiesGroup = p.Definition.GetGroupTypeId(),
                DataType = p.Definition.GetDataType(),
                Formula = string.IsNullOrWhiteSpace(p.Formula) ? null : p.Formula,
                ValuesPerType = values,
                IsBuiltIn = isBuiltIn,
                SharedGuid = sharedGuid,
                StorageType = p.StorageType
            };
        }

        // Wrap in transaction since fm.CurrentType setter uses a sub-transaction internally
        using var tx = new Transaction(famDoc.Document, "Snapshot Collection");
        _ = tx.Start();

        try {
            foreach (var t in types) {
                fm.CurrentType = t;

                foreach (var p in familyParameters) {
                    var key = GetKey(p.Definition.Name, p.IsInstance);
                    if (!snapshots.TryGetValue(key, out var snap))
                        continue;

                    if (!string.IsNullOrWhiteSpace(snap.Formula)) {
                        snap.ValuesPerType[t.Name] = null;
                        continue;
                    }

                    var value = famDoc.GetValue(p);
                    snap.ValuesPerType[t.Name] = CoerceValueToString(value);
                }
            }
        } finally {
            // Rollback to restore original CurrentType and avoid any side effects
            if (tx.HasStarted())
                _ = tx.RollBack();
        }

        snapshot.Parameters = snapshots.Values
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(s => s.IsInstance)
            .ToList();
    }

    private static string GetKey(string name, bool isInstance) => $"{name}|{isInstance}";

    private static string? CoerceValueToString(object value) {
        if (value is null)
            return null;

        if (value is string s)
            return string.IsNullOrWhiteSpace(s) ? null : s;

        if (value is double d)
            return d.ToString(CultureInfo.InvariantCulture);

        if (value is int i)
            return i.ToString(CultureInfo.InvariantCulture);

        if (value is ElementId id)
            return id == ElementId.InvalidElementId ? null : id.Value().ToString(CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}