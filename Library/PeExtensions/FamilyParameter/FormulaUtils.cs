using PeExtensions.FamDocument;
using System.Text.RegularExpressions;

namespace PeExtensions;

public static class FormulaUtils {
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
        if (string.IsNullOrEmpty(parameterName) || string.IsNullOrEmpty(formula)) return false;

        var leftIndex = formula.IndexOf(parameterName, StringComparison.Ordinal);
        if (leftIndex == -1) return false;
        var leftValid = leftIndex == 0 || BoundaryChars.Contains(formula[leftIndex - 1]);

        var rightIndex = leftIndex + parameterName.Length;
        var rightValid = rightIndex >= formula.Length || BoundaryChars.Contains(formula[rightIndex]);

        return leftValid && rightValid;
    }

    /// <summary>
    ///     Gets all family parameters referenced in THIS formula string.
    ///     Use this when validating a formula before setting it on a parameter.
    /// </summary>
    /// <returns>Collection of family parameters referenced in the formula</returns>
    public static IEnumerable<FamilyParameter> GetReferencedParameters(
        string formula,
        FamilyManager familyManager
    ) {
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
    /// <returns>Collection of family parameters referenced in the formula, empty if no formula or no references</returns>
    public static IEnumerable<FamilyParameter> FormulaDependencies(
        this FamilyParameter param,
        FamilyManager familyManager
    ) => GetReferencedParameters(param.Formula, familyManager);

    /// <summary>
    ///     Gets all family parameters that reference THIS parameter in their formulas.
    ///     Direction: Who depends on me? (upstream dependents)
    /// </summary>
    /// <returns>Collection of family parameters that use this parameter in their formulas</returns>
    public static IEnumerable<FamilyParameter> FormulaDependents(
        this FamilyParameter param,
        FamilyDocument doc
    ) => doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .Where(p => param.IsReferencedInFormula(p.Formula));

    /// <summary>
    ///     Checks if this parameter's formula references another parameter.
    /// </summary>
    /// <returns>True if otherParam is referenced in this parameter's formula</returns>
    public static bool ReferencesParameter(this FamilyParameter thisParam, FamilyParameter otherParam) {
        var formula = thisParam.Formula;
        if (string.IsNullOrWhiteSpace(formula))
            return false;

        return otherParam.IsReferencedInFormula(formula);
    }

    /// <summary>
    ///     Validates that all parameter-like tokens in a formula reference existing parameters.
    ///     Returns empty list if valid, otherwise returns the invalid parameter names.
    /// </summary>
    /// <returns>Collection of invalid parameter names, empty if all tokens are valid</returns>
    public static IEnumerable<string> GetInvalidParameterReferences(
        string formula,
        FamilyManager familyManager
    ) {
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

    #region Formula Analysis

    /// <summary>
    ///     Checks if a formula is a constant expression (contains no parameter references).
    ///     Constant formulas include literals like "20", "7.75\"", "60 Hz", "\"text\"",
    ///     and constant expressions like "2 A + 5 A".
    /// </summary>
    /// <returns>True if the formula has no parameter references</returns>
    public static bool IsConstantFormula(string formula, FamilyManager familyManager) {
        if (string.IsNullOrWhiteSpace(formula)) return false;
        return !GetReferencedParameters(formula, familyManager).Any();
    }

    /// <summary>
    ///     Checks if a formula is just a single parameter reference (no operators, no functions).
    ///     Returns the referenced parameter if so, null otherwise.
    /// </summary>
    /// <param name="formula">The formula string to check</param>
    /// <param name="familyManager">The family manager containing all parameters</param>
    /// <returns>The single referenced parameter, or null if not a single reference</returns>
    public static FamilyParameter TryGetSingleParameterReference(string formula, FamilyManager familyManager) {
        if (string.IsNullOrWhiteSpace(formula)) return null;

        var referencedParams = GetReferencedParameters(formula, familyManager).ToList();
        if (referencedParams.Count != 1) return null;

        var param = referencedParams[0];
        // Formula must be EXACTLY the parameter name (trimmed)
        return formula.Trim() == param.Definition.Name ? param : null;
    }

    /// <summary>
    ///     Follows a chain of single-parameter-reference formulas to find the ultimate source.
    ///     Stops when hitting: no formula, a constant formula, or a complex formula.
    ///     Revit guarantees no cycles exist in formula chains.
    /// </summary>
    /// <param name="param">The starting parameter</param>
    /// <param name="familyManager">The family manager containing all parameters</param>
    /// <returns>Result containing ultimate source and any intermediate parameters</returns>
    public static FormulaChainResult ResolveFormulaChain(FamilyParameter param, FamilyManager familyManager) {
        var intermediates = new List<FamilyParameter>();
        var current = param;

        while (true) {
            var formula = current.Formula;

            // Terminal: no formula - current is the source
            if (string.IsNullOrWhiteSpace(formula))
                return new FormulaChainResult(current, intermediates, false);

            // Terminal: constant formula - current is the source
            if (IsConstantFormula(formula, familyManager))
                return new FormulaChainResult(current, intermediates, true);

            // Check for single parameter reference to continue chain
            var nextParam = TryGetSingleParameterReference(formula, familyManager);
            if (nextParam == null)
                // Terminal: complex formula - current is the source
                return new FormulaChainResult(current, intermediates, false);

            // Continue following the chain
            intermediates.Add(current);
            current = nextParam;
        }
    }

    #endregion

    #region Cycle Detection

    /// <summary>
    ///     Checks if setting a formula on a target parameter would create a cycle.
    ///     A cycle occurs if any parameter referenced in the formula (transitively) depends on the target.
    /// </summary>
    /// <param name="targetParam">The parameter that would receive the formula</param>
    /// <param name="formula">The formula to check</param>
    /// <param name="familyManager">The family manager containing all parameters</param>
    /// <returns>Result with cycle details if a cycle would be created, or WouldCycle=false if safe</returns>
    public static CycleDetectionResult DetectCycle(FamilyParameter targetParam, string formula,
        FamilyManager familyManager) {
        if (string.IsNullOrWhiteSpace(formula))
            return CycleDetectionResult.NoCycle;

        var referencedParams = GetReferencedParameters(formula, familyManager).ToList();

        foreach (var param in referencedParams) {
            var path = new List<FamilyParameter>();
            if (FindCyclePath(param, targetParam, familyManager, path, new HashSet<ElementId>())) {
                // Path goes: param -> ... -> target
                // Full cycle is: target --(formula)--> param -> ... -> target
                return new CycleDetectionResult(true, param, path);
            }
        }

        return CycleDetectionResult.NoCycle;
    }

    /// <summary>
    ///     Recursively finds the path from 'current' to 'target' through formula dependencies.
    ///     Returns true if a path exists, populating 'path' with the parameters in the cycle.
    /// </summary>
    private static bool FindCyclePath(FamilyParameter current, FamilyParameter target, FamilyManager familyManager,
        List<FamilyParameter> path, HashSet<ElementId> visited) {
        path.Add(current);

        if (current.Id == target.Id)
            return true;

        if (!visited.Add(current.Id)) {
            path.RemoveAt(path.Count - 1);
            return false;
        }

        var formula = current.Formula;
        if (string.IsNullOrWhiteSpace(formula)) {
            path.RemoveAt(path.Count - 1);
            return false;
        }

        var dependencies = GetReferencedParameters(formula, familyManager);
        foreach (var dep in dependencies) {
            if (FindCyclePath(dep, target, familyManager, path, visited))
                return true;
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    #endregion
}

/// <summary>
///     Result of resolving a formula chain.
/// </summary>
public class FormulaChainResult {
    /// <summary>The final parameter in the chain (has value, constant formula, or complex formula)</summary>
    public FamilyParameter UltimateSource { get; }

    /// <summary>Parameters between the start and ultimate source (empty if start IS the source)</summary>
    public IReadOnlyList<FamilyParameter> Intermediates { get; }

    /// <summary>True if the ultimate source has a constant formula that should be unwrapped</summary>
    public bool SourceHasConstantFormula { get; }

    public FormulaChainResult(FamilyParameter ultimateSource, IReadOnlyList<FamilyParameter> intermediates,
        bool sourceHasConstantFormula) {
        this.UltimateSource = ultimateSource;
        this.Intermediates = intermediates;
        this.SourceHasConstantFormula = sourceHasConstantFormula;
    }
}

/// <summary>
///     Result of cycle detection when checking if a formula would create a circular reference.
/// </summary>
public class CycleDetectionResult {
    /// <summary>True if setting the formula would create a cycle</summary>
    public bool WouldCycle { get; }

    /// <summary>The parameter directly referenced in the formula that leads to the cycle (null if no cycle)</summary>
    public FamilyParameter DirectReference { get; }

    /// <summary>
    ///     The path of parameters forming the cycle, from DirectReference back to the target.
    ///     Example: If setting A.Formula = B and B.Formula = C and C.Formula = A,
    ///     DirectReference = B, CyclePath = [B, C, A]
    /// </summary>
    public IReadOnlyList<FamilyParameter> CyclePath { get; }

    public CycleDetectionResult(bool wouldCycle, FamilyParameter directReference,
        IReadOnlyList<FamilyParameter> cyclePath) {
        this.WouldCycle = wouldCycle;
        this.DirectReference = directReference;
        this.CyclePath = cyclePath;
    }

    public static CycleDetectionResult NoCycle => new(false, null, null);

    /// <summary>
    ///     Formats the cycle path as a readable string for error messages.
    ///     Example: "B → C → A" or "B (references A directly)"
    /// </summary>
    public string FormatCyclePath() {
        if (!this.WouldCycle || this.CyclePath == null || this.CyclePath.Count == 0)
            return string.Empty;

        var names = this.CyclePath.Select(p => p.Definition.Name).ToList();
        return string.Join(" → ", names);
    }
}