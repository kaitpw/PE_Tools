using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using Autodesk.Revit.DB.Structure;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.GetValue;
using PeExtensions.PolyFill;
using System.Globalization;

namespace AddinFamilyFoundrySuite.Core.Snapshots;

/// <summary>
///     Collects parameter snapshots with strategy-based source selection.
///     Prefers project document (faster - no type cycling), falls back to family document.
///     Family doc collection runs if: no data exists, data is empty, or data is partial.
/// </summary>
public class ParamSectionCollector : IProjectCollector, IFamilyDocCollector {
    // IProjectCollector implementation (preferred - runs first)
    bool IProjectCollector.ShouldCollect(FamilySnapshot snapshot) =>
        snapshot.Parameters?.Data?.Count == 0 || snapshot.Parameters == null;

    // IFamilyDocCollector implementation (fallback - runs if project collection was partial or skipped)
    bool IFamilyDocCollector.ShouldCollect(FamilySnapshot snapshot) =>
        snapshot.Parameters == null ||
        snapshot.Parameters.Data?.Count == 0 ||
        snapshot.Parameters.IsPartial;

    public void Collect(FamilySnapshot snapshot, Document projectDoc, Family family) =>
        snapshot.Parameters = this.CollectFromProject(projectDoc, family);

    // IFamilyDocCollector implementation (fallback)
    void IFamilyDocCollector.Collect(FamilySnapshot snapshot, FamilyDocument famDoc) =>
        snapshot.Parameters = this.CollectFromFamilyDoc(famDoc);

    private SnapshotSection<ParamSnapshot> CollectFromProject(Document doc, Family family) {
        var symbols = GetAllSymbols(family);
        if (symbols.Count == 0)
            return new SnapshotSection<ParamSnapshot> { Source = SnapshotSource.Project, IsPartial = true };

        var typeNames = symbols
            .Select(s => s.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Build set of project parameter names from ParameterBindings
        var projectParamNames = GetProjectParameterNames(doc);

        var snapshots = new Dictionary<string, ParamSnapshot>(StringComparer.Ordinal);
        var instanceCollectionCount = 0;

        using var tx = new Transaction(doc, "Temp Instance for Param Snapshot Collection");
        _ = tx.Start();

        try {
            foreach (var symbol in symbols) {
                if (!symbol.IsActive)
                    symbol.Activate();

                var typeName = symbol.Name;

                // Collect type parameters from FamilySymbol (IsInstance = false)
                CollectTypeParams(symbol, typeName, typeNames, snapshots, projectParamNames);

                // Collect instance parameters from temp FamilyInstance (IsInstance = true)
                var tempInstance = doc.Create.NewFamilyInstance(
                    XYZ.Zero,
                    symbol,
                    StructuralType.NonStructural);

                if (tempInstance is not null) {
                    CollectInstanceParams(tempInstance, typeName, typeNames, snapshots, projectParamNames);
                    instanceCollectionCount++;
                }
            }
        } finally {
            if (tx.HasStarted())
                _ = tx.RollBack();
        }

        // Mark as partial if we couldn't create temp instances for all symbols
        // This allows family doc collection to run as fallback for complete data
        var isPartial = instanceCollectionCount < symbols.Count;

        return new SnapshotSection<ParamSnapshot> {
            Source = SnapshotSource.Project,
            IsPartial = isPartial,
            Data = [.. snapshots.Values
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(s => s.IsInstance)]
        };
    }

    private SnapshotSection<ParamSnapshot> CollectFromFamilyDoc(FamilyDocument famDoc) {
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

            var values = typeNames.ToDictionary(t => t, _ => (string)null, StringComparer.Ordinal);

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

        return new SnapshotSection<ParamSnapshot> {
            Source = SnapshotSource.FamilyDoc,
            Data = snapshots.Values
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(s => s.IsInstance)
                .ToList()
        };
    }

    // ==================== Helpers ====================

    private static void CollectTypeParams(
        FamilySymbol symbol,
        string typeName,
        List<string> allTypeNames,
        Dictionary<string, ParamSnapshot> snapshots,
        HashSet<string> projectParamNames
    ) {
        foreach (var p in symbol.Parameters.OfType<Parameter>().Where(p => p.Definition != null)) {
            var key = GetKey(p.Definition.Name, false);
            var snap = GetOrCreateSnapshot(p, false, allTypeNames, snapshots, key, projectParamNames);
            snap.ValuesPerType[typeName] = GetValueString(p);
        }
    }

    private static void CollectInstanceParams(
        FamilyInstance instance,
        string typeName,
        List<string> allTypeNames,
        Dictionary<string, ParamSnapshot> snapshots,
        HashSet<string> projectParamNames
    ) {
        foreach (var p in instance.Parameters.OfType<Parameter>().Where(p => p.Definition != null)) {
            var key = GetKey(p.Definition.Name, true);
            var snap = GetOrCreateSnapshot(p, true, allTypeNames, snapshots, key, projectParamNames);
            snap.ValuesPerType[typeName] = GetValueString(p);
        }
    }

    private static ParamSnapshot GetOrCreateSnapshot(
        Parameter param,
        bool isInstance,
        List<string> allTypeNames,
        Dictionary<string, ParamSnapshot> snapshots,
        string key,
        HashSet<string> projectParamNames
    ) {
        if (snapshots.TryGetValue(key, out var existing))
            return existing;

        var def = param.Definition ?? throw new InvalidOperationException("Parameter.Definition is null.");

        var isBuiltIn = ParameterUtils.IsBuiltInParameter(param.Id);
        Guid? sharedGuid = null;
        if (param.IsShared) {
            try { sharedGuid = param.GUID; } catch {
                /* GUID access can still throw sometimes */
            }
        }

        var values = allTypeNames.ToDictionary(t => t, _ => (string)null, StringComparer.Ordinal);

        var created = new ParamSnapshot {
            Name = def.Name,
            IsInstance = isInstance,
            PropertiesGroup = def.GetGroupTypeId(),
            DataType = def.GetDataType(),
            Formula = null, // Formula not available in project context
            ValuesPerType = values,
            IsBuiltIn = isBuiltIn,
            SharedGuid = sharedGuid,
            StorageType = param.StorageType,
            IsProjectParameter = projectParamNames.Contains(def.Name)
        };

        snapshots[key] = created;
        return created;
    }

    private static string GetValueString(Parameter param) {
        if (!param.HasValue)
            return null;

        return param.StorageType switch {
            StorageType.String => string.IsNullOrWhiteSpace(param.AsString()) ? null : param.AsString(),
            StorageType.Double => param.AsDouble().ToString(CultureInfo.InvariantCulture),
            StorageType.Integer => param.AsInteger().ToString(CultureInfo.InvariantCulture),
            StorageType.ElementId => param.AsElementId() == ElementId.InvalidElementId
                ? null
                : param.AsElementId().Value().ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static string GetKey(string name, bool isInstance) => $"{name}|{isInstance}";

    private static string CoerceValueToString(object value) {
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

    private static List<FamilySymbol> GetAllSymbols(Family family) {
        var symbolIds = family.GetFamilySymbolIds();
        if (symbolIds == null || symbolIds.Count == 0)
            return [];

        return symbolIds
            .Select(id => family.Document.GetElement(id) as FamilySymbol)
            .Where(s => s != null)
            .ToList()!;
    }

    /// <summary>
    ///     Gets the names of all project parameters from Document.ParameterBindings.
    ///     Returns empty set if doc is a family document (ParameterBindings throws for family docs).
    /// </summary>
    private static HashSet<string> GetProjectParameterNames(Document doc) {
        if (doc.IsFamilyDocument)
            return new HashSet<string>(StringComparer.Ordinal);

        var projectParamNames = new HashSet<string>(StringComparer.Ordinal);

        try {
            var bindingMap = doc.ParameterBindings;
            var iterator = bindingMap.ForwardIterator();
            iterator.Reset();

            while (iterator.MoveNext()) {
                var definition = iterator.Key as Definition;
                if (definition != null)
                    _ = projectParamNames.Add(definition.Name);
            }
        } catch {
            // ParameterBindings can throw InvalidOperationException for family documents
            // or other edge cases - return empty set
        }

        return projectParamNames;
    }
}

