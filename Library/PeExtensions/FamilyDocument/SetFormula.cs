using PeExtensions.FamDocument;

namespace PeExtensions;

public static class FamilyDocumentSetFormula {
    /// <summary>
    ///     Set a formula on a family parameter, validating that all referenced parameters exist and have
    ///     the same type/instance designation as the target parameter.
    /// </summary>
    /// <param name="famDoc">The family document</param>
    /// <param name="targetParam">The parameter to set the formula on</param>
    /// <param name="formula">The formula string, use null or empty string to clear the formula</param>
    /// <returns>True if the formula was set successfully</returns>
    /// <exception cref="ArgumentException">Thrown when formula references a non-existent parameter</exception>
    /// <exception cref="InvalidOperationException">Thrown when formula references parameters with mismatched type/instance designation</exception>
    public static bool SetFormula(this FamilyDocument famDoc, FamilyParameter targetParam, string formula) {
        if (string.IsNullOrWhiteSpace(formula)) {
            famDoc.FamilyManager.SetFormula(targetParam, null);
            return true;
        }

        var allParams = famDoc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .ToList();

        var referencedParams = allParams
            .Where(p => p.IsReferencedInFormula(formula))
            .ToList();

        // Validate all referenced parameters have the same type/instance designation
        var targetIsInstance = targetParam.IsInstance;
        var mismatchedParams = referencedParams
            .Where(p => p.IsInstance != targetIsInstance)
            .ToList();

        if (mismatchedParams.Count > 0) {
            var targetDesignation = targetParam.GetTypeInstanceDesignation();
            var mismatchedNames = mismatchedParams
                .Select(p => $"'{p.Name()}' ({p.GetTypeInstanceDesignation()})")
                .ToList();

            throw new InvalidOperationException(
                $"Cannot set formula on {targetDesignation} parameter '{targetParam.Name()}'. " +
                $"Formula references parameters with different type/instance designation: {string.Join(", ", mismatchedNames)}");
        }

        famDoc.FamilyManager.SetFormula(targetParam, formula);
        return true;
    }
}

