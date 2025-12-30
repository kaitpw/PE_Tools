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

        // 1. Handle explicit per-type parameters
        foreach (var p in this.Settings.ParametersPerType) {
            // Skip if this type isn't in the dictionary
            if (currentTypeName is null
                || !p.ValuesPertype.TryGetValue(currentTypeName, out var value)
               ) continue;

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

        // 2. Handle fallback for failed global values (check GroupContext for deferred entries)
        foreach (var p in this.Settings.Parameters) {
            var log = groupContext.Get(p.Name);
            if (log?.IsComplete == true) continue; // Already handled
            if (string.IsNullOrWhiteSpace(p.ValueOrFormula)) continue;
            var parameter = fm.FindParameter(p.Name);
            if (parameter is null) continue; // Already logged in SetParamValues

            try {
                // Use the same value detection as explicit per-type
                SetPerTypeValue(famDoc, parameter, p.ValueOrFormula);
                _ = log.Success("Set per-type (fallback)");
            } catch (Exception ex) {
                _ = log.Error(ex);
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

        // Reject values that contain parameter references (should use SetParamValues for formulas)
        var referencedParams = fm.Parameters.GetReferencedIn(userValue).ToList();
        if (referencedParams.Any()) {
            throw new InvalidOperationException(
                $"Per-type value '{userValue}' contains parameter references. Use {nameof(SetParamModel.ValueOrFormula)} (not {nameof(SetParamPerTypeModel.ValuesPertype)}) for formulas.");
        }

        // Check for double-quoted string literal: "\"text\"" → strip quotes
        var actualValue = IsQuotedStringLiteral(userValue) ? userValue.Trim()[1..^1] : userValue;

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