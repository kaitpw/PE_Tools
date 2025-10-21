namespace PeExtensions;

public static class FamilyParameterGetAssociated {
    /// <summary>
    ///     Get the associated linear, radial, and angular dimensions for a family parameter
    /// </summary>
    /// <param name="param">The family parameter</param>
    /// <param name="doc">The family document</param>
    /// <returns>The associated dimensions</returns>
    public static IEnumerable<Dimension> AssociatedDimensions(this FamilyParameter param, Document doc) {
        if (!doc.IsFamilyDocument) throw new Exception("Document is not a family document");

        var provider = new ParameterValueProvider(new ElementId(BuiltInParameter.DIM_LABEL));
        var rule = new FilterElementIdRule(provider, new FilterNumericEquals(), param.Id);
        var paramFilter = new ElementParameterFilter(rule);

        var dimensionTypes = new List<Type> {
            typeof(LinearDimension), typeof(RadialDimension), typeof(AngularDimension)
        };
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
    public static IEnumerable<BaseArray> AssociatedArrays(this FamilyParameter param, Document doc) {
        if (!doc.IsFamilyDocument) throw new Exception("Document is not a family document");

        if (param.Definition.GetDataType() != SpecTypeId.Int.Integer)
            return new List<BaseArray>();

        return new FilteredElementCollector(doc)
            .OfClass(typeof(BaseArray))
            .Cast<BaseArray>()
            .Where(array => array.Label?.Id == param.Id);
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
        Document doc,
        bool excludeUnused = false) {
        if (!doc.IsFamilyDocument) throw new Exception("Document is not a family document");

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
                    return !string.IsNullOrEmpty(formula) && IsParameterNameInFormula(parameterName, formula);
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
            return false;
        });
    }

    /// <summary>
    ///     Checks if a parameter name is contained in a formula with strict boundary validation
    /// </summary>
    /// <param name="parameterName">The parameter name to search for</param>
    /// <param name="formula">The formula to search in</param>
    /// <returns>True if the parameter name is properly bounded in the formula</returns>
    private static bool IsParameterNameInFormula(string parameterName, string formula) {
        if (string.IsNullOrEmpty(parameterName) || string.IsNullOrEmpty(formula))
            return false;

        // Possible characters sandwiching a param name: =, +, -, *, /, ^, space,(, ), <, >, ", comma
        var besideChars = new[] { '=', '+', '-', '*', '/', '^', ' ', '(', ')', '<', '>', '"', ',' };

        var leftIndex = formula.IndexOf(parameterName, StringComparison.Ordinal);
        if (leftIndex == -1) return false;
        var leftValid = leftIndex == 0 || besideChars.Contains(formula[leftIndex - 1]);

        var rightIndex = leftIndex + parameterName.Length;
        var rightValid = rightIndex >= formula.Length || besideChars.Contains(formula[rightIndex]);

        return leftValid && rightValid;
    }

    /// <summary>
    ///     Get the associated elements for a family parameter
    /// </summary>
    /// <param name="param">The family parameter</param>
    /// <param name="doc">The family document</param>
    /// <returns>The associated elements</returns>
    public static bool HasAssociation(this FamilyParameter param, Document doc) =>
        param.AssociatedParameters.Cast<Parameter>().Any() || param.AssociatedArrays(doc).Any() ||
        param.AssociatedDimensions(doc).Any() || param.AssociatedFamilyParameters(doc).Any();
}