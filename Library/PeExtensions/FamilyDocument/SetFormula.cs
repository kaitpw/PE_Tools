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

        // Check for circular references before Revit throws a cryptic error
        var cycleResult = FamilyParameterFormulaUtils.DetectCycle(targetParam, formula, familyManager);
        if (cycleResult.WouldCycle) {
            var cyclePath = cycleResult.FormatCyclePath();
            var message = $"Cannot set formula '{formula}' on parameter '{targetParam.Name()}'. " +
                          $"This would create a circular reference: {targetParam.Name()} → {cyclePath}";
            throw new InvalidOperationException(message);
        }

        famDoc.FamilyManager.SetFormula(targetParam, formula);
        return true;
    }

    /// <summary>
    ///     Set a formula on a family parameter without validation.
    ///     Use this for batch operations where you trust the input and need performance.
    ///     Revit will still throw if there's a cycle, but the error will be less descriptive.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>When to use:</b> Migrations, imports, or batch operations with known-good formulas.
    ///     </para>
    ///     <para>
    ///         <b>When NOT to use:</b> User-entered formulas, untrusted input, or when you need helpful error messages.
    ///     </para>
    /// </remarks>
    /// <param name="famDoc">The family document</param>
    /// <param name="targetParam">The parameter to set the formula on</param>
    /// <param name="formula">The formula string, use null or empty string to clear the formula</param>
    /// <exception cref="Autodesk.Revit.Exceptions.InvalidOperationException">
    ///     Thrown by Revit if the formula is invalid (cryptic message).
    /// </exception>
    public static void SetFormulaNative(this FamilyDocument famDoc, FamilyParameter targetParam, string formula) {
        if (string.IsNullOrWhiteSpace(formula)) {
            famDoc.FamilyManager.SetFormula(targetParam, null);
            return;
        }

        famDoc.FamilyManager.SetFormula(targetParam, formula);
    }
}
