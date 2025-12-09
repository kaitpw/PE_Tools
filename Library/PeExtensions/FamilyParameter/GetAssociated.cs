using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using PeExtensions.FamDocument;

namespace PeExtensions;

public static class FamilyParameterGetAssociated {
    /// <summary>
    ///     Get the associated linear, radial, and angular dimensions for a family parameter
    /// </summary>
    /// <param name="param">The family parameter</param>
    /// <param name="doc">The family document</param>
    /// <returns>The associated dimensions</returns>
    public static IEnumerable<Dimension> AssociatedDimensions(this FamilyParameter param, FamilyDocument doc) {
        var provider = new ParameterValueProvider(new ElementId(BuiltInParameter.DIM_LABEL));
        var rule = new FilterElementIdRule(provider, new FilterNumericEquals(), param.Id);
        var paramFilter = new ElementParameterFilter(rule);

        var dimensionTypes = new List<Type> { typeof(Dimension) };
        var dimensionFilter = new ElementMulticlassFilter(dimensionTypes);

        var combinedFilter = new LogicalAndFilter(dimensionFilter, paramFilter);

        return new FilteredElementCollector(doc)
            .WherePasses(combinedFilter)
            .Cast<Dimension>();
    }


    /// <summary>
    ///     Get the associated arrays for a family parameter
    /// </summary>
    /// <param name="param">The family parameter</param>
    /// <param name="doc">The family document</param>
    /// <returns>The associated arrays</returns>
    public static IEnumerable<BaseArray> AssociatedArrays(this FamilyParameter param, FamilyDocument doc) {
        if (param.Definition.GetDataType() != SpecTypeId.Int.Integer)
            return new List<BaseArray>();

        return new FilteredElementCollector(doc)
            .OfClass(typeof(BaseArray))
            .Cast<BaseArray>()
            .Where(array => array.Label?.Id == param.Id);
    }

    /// <summary>
    ///     Get the associated connectors (electrical, mechanical, piping) for a family parameter.
    ///     Returns connectors that have at least one parameter associated with the given family parameter.
    /// </summary>
    /// <param name="param">The family parameter</param>
    /// <param name="doc">The family document</param>
    /// <returns>The associated connector elements</returns>
    public static IEnumerable<ConnectorElement> AssociatedConnectors(this FamilyParameter param, FamilyDocument doc) {
        var connectors = new FilteredElementCollector(doc)
            .OfClass(typeof(ConnectorElement))
            .Cast<ConnectorElement>();

        foreach (var connector in connectors) {
            foreach (Parameter connectorParam in connector.Parameters) {
                var associated = doc.FamilyManager.GetAssociatedFamilyParameter(connectorParam);
                if (associated?.Id == param.Id) {
                    yield return connector;
                    break; // Found a match, no need to check other parameters on this connector
                }
            }
        }
    }

    /// <summary>
    ///     Get the family parameters containing this family parameter in their formula
    /// </summary>
    /// <param name="param">The family parameter</param>
    /// <param name="doc">The family document</param>
    /// <param name="excludeUnused">
    ///     If true, only return parameters that are actually being used (have direct associations like
    ///     connectors, dimensions, or arrays - NOT formula usage)
    /// </param>
    public static IEnumerable<FamilyParameter> AssociatedFamilyParameters(this FamilyParameter param,
        FamilyDocument doc,
        bool excludeUnused = false) {
        // Get the parameter name safely. Some built-in parameters throw invalid when accessing Definition properties
        string parameterName = null;
        try {
            parameterName = param.Definition.Name?.Trim();
        } catch (InvalidOperationException) { }

        if (string.IsNullOrEmpty(parameterName)) return [];

        var candidateParams = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .Where(p => {
                try {
                    var formula = p.Formula?.Trim();
                    return !string.IsNullOrEmpty(formula) && param.IsReferencedInFormula(formula);
                } catch (InvalidOperationException) {
                    return false;
                }
            });

        if (!excludeUnused) return candidateParams;

        // When excluding unused, only return parameters that have DIRECT associations (not formula usage)
        // This prevents circular dependencies where A and B reference each other in formulas
        return candidateParams.Where(p => {
            // Check if parameter has any DIRECT associations (not formula usage)
            if (p.AssociatedParameters.Cast<Parameter>().Any()) return true;
            if (p.AssociatedArrays(doc).Any()) return true;
            if (p.AssociatedDimensions(doc).Any()) return true;
            if (p.AssociatedConnectors(doc).Any()) return true;
            return false;
        });
    }

    /// <summary>
    ///     Checks if the family parameter has any associations (dimensions, arrays, connectors, or formula dependencies)
    /// </summary>
    /// <param name="param">The family parameter</param>
    /// <param name="doc">The family document</param>
    /// <returns>True if the parameter has any associations</returns>
    public static bool HasAssociation(this FamilyParameter param, FamilyDocument doc) =>
        param.AssociatedParameters.Cast<Parameter>().Any() || param.AssociatedArrays(doc).Any() ||
        param.AssociatedDimensions(doc).Any() || param.AssociatedConnectors(doc).Any() ||
        param.AssociatedFamilyParameters(doc).Any();
}