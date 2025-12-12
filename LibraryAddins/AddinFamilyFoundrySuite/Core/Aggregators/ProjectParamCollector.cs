using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using Autodesk.Revit.DB.Structure;
using PeExtensions.PolyFill;
using System.Globalization;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Collects parameter snapshots by placing temporary family instances and rolling back.
///     Iterates ALL family symbols to gather per-type values.
/// </summary>
public class ProjectParamCollector : IProjectSnapshotCollector {
    public void Collect((Document doc, Family family) input, FamilySnapshot snapshot) {
        var (doc, family) = input;

        var symbols = GetAllSymbols(family);
        if (symbols.Count == 0)
            return;

        var typeNames = symbols
            .Select(s => s.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var snapshots = new Dictionary<string, ParamSnapshot>(StringComparer.Ordinal);

        using var tx = new Transaction(doc, "Temp Instance for Param Snapshot Collection");
        _ = tx.Start();

        try {
            foreach (var symbol in symbols) {
                if (!symbol.IsActive)
                    symbol.Activate();

                var typeName = symbol.Name;
                var tempInstance = doc.Create.NewFamilyInstance(
                    XYZ.Zero,
                    symbol,
                    StructuralType.NonStructural);

                if (tempInstance is not null)
                    CollectInstanceParams(tempInstance, typeName, typeNames, snapshots);

                CollectTypeParams(symbol, typeName, typeNames, snapshots);
            }
        } finally {
            if (tx.HasStarted())
                _ = tx.RollBack();
        }

        snapshot.Parameters = snapshots.Values
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(s => s.IsInstance)
            .ToList();
    }

    private static void CollectInstanceParams(
        FamilyInstance instance,
        string typeName,
        List<string> allTypeNames,
        Dictionary<string, ParamSnapshot> snapshots
    ) {
        foreach (var p in instance.GetOrderedParameters().Where(p => p.Definition != null)) {
            var key = GetKey(p.Definition.Name, true);
            var snap = GetOrCreateSnapshot(p, true, allTypeNames, snapshots, key);
            snap.ValuesPerType[typeName] = GetValueString(p);
        }
    }

    private static void CollectTypeParams(
        FamilySymbol symbol,
        string typeName,
        List<string> allTypeNames,
        Dictionary<string, ParamSnapshot> snapshots
    ) {
        foreach (Parameter p in symbol.Parameters) {
            if (p.Definition is null)
                continue;

            var key = GetKey(p.Definition.Name, false);
            var snap = GetOrCreateSnapshot(p, false, allTypeNames, snapshots, key);
            snap.ValuesPerType[typeName] = GetValueString(p);
        }
    }

    private static ParamSnapshot GetOrCreateSnapshot(
        Parameter param,
        bool isInstance,
        List<string> allTypeNames,
        Dictionary<string, ParamSnapshot> snapshots,
        string key
    ) {
        if (snapshots.TryGetValue(key, out var existing))
            return existing;

        var def = param.Definition;
        if (def is null)
            throw new InvalidOperationException("Parameter.Definition is null.");

        var isBuiltIn = ParameterUtils.IsBuiltInParameter(param.Id);
        Guid? sharedGuid = null;
        if (param.IsShared)
            try { sharedGuid = param.GUID; } catch {
                /* GUID access can throw */
            }

        var values = allTypeNames.ToDictionary(t => t, _ => (string?)null, StringComparer.Ordinal);

        var created = new ParamSnapshot {
            Name = def.Name,
            IsInstance = isInstance,
            PropertiesGroup = def.GetGroupTypeId(),
            DataType = def.GetDataType(),
            Formula = null, // Formula not available in project context
            ValuesPerType = values,
            IsBuiltIn = isBuiltIn,
            SharedGuid = sharedGuid,
            StorageType = param.StorageType
        };

        snapshots[key] = created;
        return created;
    }

    private static string? GetValueString(Parameter param) {
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

    private static List<FamilySymbol> GetAllSymbols(Family family) {
        var symbolIds = family.GetFamilySymbolIds();
        if (symbolIds == null || symbolIds.Count == 0)
            return [];

        return symbolIds
            .Select(id => family.Document.GetElement(id) as FamilySymbol)
            .Where(s => s != null)
            .ToList()!;
    }
}