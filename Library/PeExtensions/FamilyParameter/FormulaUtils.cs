using PeExtensions.FamDocument;
using System.Text.RegularExpressions;

namespace PeExtensions;

public static class FamilyParameterFormulaUtils {
    // Revit formula functions (case-insensitive)
    public static readonly HashSet<string> RevitFunctions = new(StringComparer.OrdinalIgnoreCase) {
        "sin",
        "cos",
        "tan",
        "asin",
        "acos",
        "atan",
        "exp",
        "log",
        "sqrt",
        "abs",
        "if",
        "or",
        "and",
        "not",
        "text_file_lookup_obsoleted",
        "pi",
        "ConduitSize_Lookup_obsoleted",
        "round",
        "roundup",
        "rounddown",
        "size_lookup",
        "ln"
    };

    // Boundary chars: operators + structural formula characters
    public static readonly char[] BoundaryChars = [
        '+', '-', '*', '/', '^', '=', '>', '<', ' ', '[', ']', '(', ')', '"', ',', '\t', '\r', '\n'
    ];

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

        var leftIndex = formula.IndexOf(parameterName, StringComparison.Ordinal);
        if (leftIndex == -1) return false;
        var leftValid = leftIndex == 0 || BoundaryChars.Contains(formula[leftIndex - 1]);

        var rightIndex = leftIndex + parameterName.Length;
        var rightValid = rightIndex >= formula.Length || BoundaryChars.Contains(formula[rightIndex]);

        return leftValid && rightValid;
    }

    /// <summary>
    ///     Gets all family parameters referenced in a formula string.
    ///     Use this when validating a formula before setting it on a parameter.
    /// </summary>
    /// <param name="formula">The formula string to analyze</param>
    /// <param name="familyManager">The family manager containing all parameters</param>
    /// <returns>Collection of family parameters referenced in the formula</returns>
    public static IEnumerable<FamilyParameter> GetReferencedParameters(string formula, FamilyManager familyManager) {
        if (string.IsNullOrWhiteSpace(formula))
            return [];

        return familyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => p.IsReferencedInFormula(formula));
    }

    /// <summary>
    ///     Gets all family parameters that THIS parameter's formula references.
    ///     Direction: What do I depend on? (downstream dependencies)
    /// </summary>
    /// <param name="param">The family parameter whose formula to analyze</param>
    /// <param name="familyManager">The family manager containing all parameters</param>
    /// <returns>Collection of family parameters referenced in the formula, empty if no formula or no references</returns>
    public static IEnumerable<FamilyParameter> FormulaDependencies(this FamilyParameter param,
        FamilyManager familyManager) =>
        GetReferencedParameters(param.Formula, familyManager);

    /// <summary>
    ///     Gets all family parameters that reference THIS parameter in their formulas.
    ///     Direction: Who depends on me? (upstream dependents)
    /// </summary>
    /// <param name="param">The family parameter to find dependents for</param>
    /// <param name="doc">The family document</param>
    /// <returns>Collection of family parameters that use this parameter in their formulas</returns>
    public static IEnumerable<FamilyParameter> FormulaDependents(this FamilyParameter param, FamilyDocument doc) =>
        doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .Where(p => param.IsReferencedInFormula(p.Formula));

    /// <summary>
    ///     Checks if this parameter's formula references another parameter.
    /// </summary>
    /// <param name="param">The family parameter whose formula to check</param>
    /// <param name="otherParam">The parameter to check for in the formula</param>
    /// <returns>True if otherParam is referenced in this parameter's formula</returns>
    public static bool ReferencesParameter(this FamilyParameter param, FamilyParameter otherParam) {
        var formula = param.Formula;
        if (string.IsNullOrWhiteSpace(formula))
            return false;

        return otherParam.IsReferencedInFormula(formula);
    }

    /// <summary>
    ///     Validates that all parameter-like tokens in a formula reference existing parameters.
    ///     Returns empty list if valid, otherwise returns the invalid parameter names.
    /// </summary>
    /// <param name="formula">The formula string to validate</param>
    /// <param name="familyManager">The family manager containing all parameters</param>
    /// <returns>Collection of invalid parameter names, empty if all tokens are valid</returns>
    public static IEnumerable<string> GetInvalidParameterReferences(string formula, FamilyManager familyManager) {
        if (string.IsNullOrWhiteSpace(formula))
            return [];

        var allParamNames = familyManager.Parameters
            .OfType<FamilyParameter>()
            .Select(p => p.Definition.Name)
            .ToHashSet();

        var tokens = ExtractFormulaTokens(formula);
        return tokens.Where(t => !allParamNames.Contains(t));
    }

    /// <summary>
    ///     Extract potential parameter name tokens from a formula string.
    ///     Returns unvalidated string tokens - prefer GetReferencedParameters for validated parameter references.
    /// </summary>
    internal static IEnumerable<string> ExtractFormulaTokens(string formula) {
        // Strip string literals (content between quotes) before tokenizing
        // This prevents "2025_12_10 18:19:51" from being parsed as parameter names
        var withoutStrings = Regex.Replace(formula, "\"[^\"]*\"", " ");
        Debug.WriteLine($"Without strings: {withoutStrings}");
        var tokens = withoutStrings.Split(BoundaryChars, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Where(t => !IsNumericOrFunction(t)).Distinct();
    }

    /// <summary>
    ///     Check if a token is a number or a known Revit function.
    /// </summary>
    public static bool IsNumericOrFunction(string token) {
        if (double.TryParse(token, out _)) return true;
        if (RevitFunctions.Contains(token)) return true;
        return false;
    }
}