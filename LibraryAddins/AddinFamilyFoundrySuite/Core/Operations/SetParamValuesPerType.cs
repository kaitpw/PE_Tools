using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.GetValue;
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamManager;
using PeExtensions.FamParameter.Formula;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Sets parameter values on a per-type basis.
///     Handles two scenarios:
///     1. Explicit per-type values from PerTypeParameters (different value per named type)
///     2. Fallback for Parameters that failed SetGlobalValue (deferred via GroupContext)
///     Values are context-aware but must NOT contain parameter references
///     (formulas with param refs should use SetParamValues instead).
/// If a Family Type does not exist, it will NOT be created
/// </summary>
public class SetParamValuesPerType(AddAndSetParamsSettings settings)
    : TypeOperation<AddAndSetParamsSettings>(settings) {
    public override string Description =>
        "Set parameter values per family type (explicit per-type values or fallback for failed global values).";

    public override OperationLog Execute(FamilyDocument famDoc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        var fm = famDoc.FamilyManager;
        var currentTypeName = fm.CurrentType?.Name;

        foreach (var p in this.Settings.Parameters) {
            // 1. Handle explicit per-type parameters (ValuesPerType is set)
            if (p.ValuesPerType?.Count > 0) {
                // Skip if this type isn't in the dictionary
                if (currentTypeName is null || !p.ValuesPerType.TryGetValue(currentTypeName, out var value))
                    continue;

                if (string.IsNullOrWhiteSpace(value)) continue;

                var log = groupContext.GetOrCreate(p.Name);
                if (log.IsComplete) continue;

                var parameter = fm.FindParameter(p.Name);
                if (parameter is null) {
                    _ = log.Error($"Parameter '{p.Name}' not found");
                    continue;
                }

                if (!this.Settings.OverrideExistingValues && famDoc.HasValue(parameter))
                    continue;

                try {
                    SetPerTypeValue(famDoc, parameter, value);
                    _ = log.Success("Set per-type");
                } catch (Exception ex) {
                    _ = log.Error(ex);
                }
            }
            // 2. Handle fallback for failed global values (ValueOrFormula was set but deferred)
            else if (!string.IsNullOrWhiteSpace(p.ValueOrFormula)) {
                var log = groupContext.Get(p.Name);
                if (log?.IsComplete == true) continue; // Already handled

                var parameter = fm.FindParameter(p.Name);
                if (parameter is null) continue; // Already logged in SetParamValues

                try {
                    SetPerTypeValue(famDoc, parameter, p.ValueOrFormula);
                    _ = log.Success("Set per-type (fallback)");
                } catch (Exception ex) {
                    _ = log.Error(ex);
                }
            }
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }

    /// <summary>
    ///     Set a per-type parameter value using a user-provided string value.
    ///     Rejects values that contain parameter references and strips double-quotes from string literals.
    /// </summary>
    private static void SetPerTypeValue(FamilyDocument famDoc, FamilyParameter parameter, string userValue) {
        var fm = famDoc.FamilyManager;

        // Check for double-quoted string literal: "\"text\"" → strip quotes
        var actualValue = IsQuotedStringLiteral(userValue) ? userValue.Trim()[1..^1] : userValue;

        // Reject values that contain parameter references (check AFTER stripping quotes)
        var referencedParams = fm.Parameters.GetReferencedIn(actualValue).ToList();
        if (referencedParams.Any()) {
            throw new InvalidOperationException(
                $"Per-type value '{actualValue}' contains parameter references. Use ValueOrFormula with SetAsFormula=true for formulas, not ValuesPerType.");
        }

        _ = famDoc.SetValue(parameter, actualValue, ValueCoercionStrategy.CoerceSimple);
    }

    /// <summary>
    ///     Checks if the value is a double-quoted string literal: starts and ends with quotes.
    /// </summary>
    private static bool IsQuotedStringLiteral(string value) {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed.StartsWith("\"") && trimmed.EndsWith("\"");
    }
}