namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Collects parameter metadata by placing a temporary family instance and rolling back.
///     This is more performant than EditFamily for read-only parameter inspection.
/// </summary>
public class TempInstanceParamCollector : IFamilyParamCollector {
    public List<ParamCollectionResult> CollectParams(Document doc, Family family) {
        var results = new List<ParamCollectionResult>();

        var symbol = GetFirstSymbol(family);
        if (symbol == null) return results;

        using var tx = new Transaction(doc, "Temp Instance for Param Collection");
        _ = tx.Start();

        try {
            if (!symbol.IsActive) symbol.Activate();

            var tempInstance = doc.Create.NewFamilyInstance(
                XYZ.Zero,
                symbol,
                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            if (tempInstance == null) {
                _ = tx.RollBack();
                return results;
            }

            // Collect instance parameters
            var instanceParams = tempInstance.GetOrderedParameters()
                .Where(p => p.Definition != null);

            foreach (var p in instanceParams) {
                var result = CreateResult(p, isInstance: true);
                if (result != null) results.Add(result);
            }

            // Collect type parameters via Symbol
            foreach (Parameter p in tempInstance.Symbol.Parameters) {
                if (p.Definition == null) continue;

                // Skip if we already have this param from instance (avoid duplicates)
                if (results.Any(r => r.ParamName == p.Definition.Name && !r.IsInstance)) continue;

                var result = CreateResult(p, isInstance: false);
                if (result != null) results.Add(result);
            }
        } finally {
            // Always rollback - instance is never committed
            if (tx.HasStarted()) _ = tx.RollBack();
        }

        return results;
    }

    private static FamilySymbol? GetFirstSymbol(Family family) {
        var symbolIds = family.GetFamilySymbolIds();
        if (symbolIds == null || symbolIds.Count == 0) return null;

        return family.Document.GetElement(symbolIds.First()) as FamilySymbol;
    }

    private static ParamCollectionResult? CreateResult(Parameter param, bool isInstance) {
        var definition = param.Definition;
        if (definition == null) return null;

        var isBuiltIn = ParameterUtils.IsBuiltInParameter(param.Id);
        Guid? sharedGuid = null;

        if (param.IsShared) {
            try {
                sharedGuid = param.GUID;
            } catch {
                // GUID access can throw if parameter is not actually shared
            }
        }

        return new ParamCollectionResult(
            ParamName: definition.Name,
            DataType: definition.GetDataType(),
            IsInstance: isInstance,
            StorageType: param.StorageType,
            IsBuiltIn: isBuiltIn,
            SharedGuid: sharedGuid
        );
    }
}
