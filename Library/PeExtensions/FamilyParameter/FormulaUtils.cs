namespace PeExtensions;

public static class FamilyParameterFormulaUtils {
    /// <summary>
    ///     Checks if this parameter is referenced in a formula with strict boundary validation.
    ///     Validates that the parameter name is properly bounded by formula operators/delimiters.
    /// </summary>
    /// <param name="param">The family parameter to check for</param>
    /// <param name="formula">The formula to search in</param>
    /// <returns>True if the parameter name is properly bounded in the formula</returns>
    public static bool IsReferencedInFormula(this FamilyParameter param, string formula) {
        var parameterName = param.Definition.Name;
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
}

