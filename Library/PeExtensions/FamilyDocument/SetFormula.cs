using PeExtensions.FamDocument;

namespace PeExtensions;

public static class Formula {
    /// <summary>
    ///     Unset a formula on a family parameter. The same as calling
    ///     <see cref="SetFormula(FamilyDocument, FamilyParameter, string)" /> with null or empty string.
    /// </summary>
    /// <returns>True if the formula was set successfully</returns>
    /// <exception cref="Autodesk.Revit.Exceptions.InvalidOperationException">
    ///     Thrown when a type parameter formula references
    ///     instance parameters
    /// </exception>
    public static bool UnsetFormula(this FamilyDocument famDoc, FamilyParameter targetParam) {
        famDoc.FamilyManager.SetFormula(targetParam, null);
        if (targetParam.Formula is null) return true;
        return false;
    }

    /// <summary>
    ///     Set a formula on a family parameter, validating that type parameter formulas
    ///     only reference other type parameters. Instance parameter formulas can reference
    ///     both instance and type parameters.
    /// </summary>
    /// <param name="famDoc">The family document</param>
    /// <param name="targetParam">The parameter to set the formula on</param>
    /// <param name="formula">The formula string, use null or empty string to clear the formula</param>
    /// <returns>True if the formula was set successfully</returns>
    /// <exception cref="Autodesk.Revit.Exceptions.InvalidOperationException">
    ///     Thrown when a type parameter formula references instance parameters,
    ///     there is no valid family type, the parameter cannot be assigned a formula, or the operation make a circular chain
    ///     of references among the formulas.
    /// </exception>
    public static bool SetFormula(this FamilyDocument famDoc, FamilyParameter targetParam, string formula) {
        if (string.IsNullOrWhiteSpace(formula)) {
            famDoc.FamilyManager.SetFormula(targetParam, null);
            return true;
        }

        var familyManager = famDoc.FamilyManager;

        // Validate all parameter-like tokens in the formula reference existing parameters
        var invalidParams = FamilyParameterFormulaUtils.GetInvalidParameterReferences(formula, familyManager).ToList();
        if (invalidParams.Any()) {
            throw new InvalidOperationException(
                $"Cannot set formula on parameter '{targetParam.Name()}'. " +
                $"Formula references non-existent parameters: {string.Join(", ", invalidParams.Select(p => $"'{p}'"))}");
        }

        // Type parameters can only reference other type parameters
        if (!targetParam.IsInstance) {
            var referencedParams = FamilyParameterFormulaUtils.GetReferencedParameters(formula, familyManager);
            var instanceParams = referencedParams.Where(p => p.IsInstance).ToList();

            if (instanceParams.Count > 0) {
                var instanceNames = instanceParams.Select(p => $"'{p.Name()}'");
                throw new InvalidOperationException(
                    $"Cannot set formula on type parameter '{targetParam.Name()}'. " +
                    $"Type parameter formulas cannot reference instance parameters: {string.Join(", ", instanceNames)}");
            }
        }

        famDoc.FamilyManager.SetFormula(targetParam, formula);
        return true;
    }
}