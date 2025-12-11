using AddinFamilyFoundrySuite.Core.OperationSettings;
using AddinFamilyFoundrySuite.Core.OperationGroups;
using PeExtensions;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamDocument.GetValue;
using PeExtensions.FamManager;
using PeExtensions.FamParameter;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Sets parameter values on a per-type basis.
///     Handles two scenarios:
///     1. Explicit per-type values from PerTypeParameters (different value per named type)
///     2. Fallback for Parameters that failed SetGlobalValue (uses same value for all types)
///
///     Values are context-aware but must NOT contain parameter references
///     (formulas with param refs should use SetParamValues instead).
/// </summary>
public class SetParamValuesPerType(AddAndSetParamsSettings settings, SetParamSharedState sharedState = null)
    : TypeOperation<AddAndSetParamsSettings>(settings) {
    public override string Description =>
        "Set parameter values per family type (explicit per-type values or fallback for failed global values).";

    public override OperationLog Execute(FamilyDocument famDoc) {
        var logs = new Dictionary<string, LogEntry>();
        var fm = famDoc.FamilyManager;
        var currentTypeName = fm.CurrentType?.Name;

        // 1. Handle explicit per-type parameters
        foreach (var p in this.Settings.ParametersPerType) {
            // Skip if this type isn't in the dictionary
            if (currentTypeName is null || !p.ValuesPertype.TryGetValue(currentTypeName, out var value))
                continue;

            if (string.IsNullOrWhiteSpace(value))
                continue;

            var parameter = fm.FindParameter(p.Name);
            if (parameter is null) {
                logs[$"{p.Name}:{currentTypeName}"] =
                    new LogEntry { Item = p.Name, Error = $"Parameter '{p.Name}' not found" };
                continue;
            }

            if (!this.Settings.OverrideExistingValues && famDoc.HasValue(parameter))
                continue;

            try {
                SetPerTypeValue(famDoc, parameter, value);
                logs[$"{p.Name}:{currentTypeName}"] = new LogEntry { Item = p.Name };
            } catch (Exception ex) {
                logs[$"{p.Name}:{currentTypeName}"] = new LogEntry { Item = p.Name, Error = ex.Message };
            }
        }

        // 2. Handle fallback for failed global values (SetGlobalValue failures)
        if (sharedState is not null) {
            foreach (var p in this.Settings.Parameters.Where(
                         param => sharedState.FailedGlobalValueParams.Contains(param.Name))) {
                if (string.IsNullOrWhiteSpace(p.ValueOrFormula))
                    continue;

                var parameter = fm.FindParameter(p.Name);
                if (parameter is null)
                    continue; // Already logged in SetParamValues

                try {
                    // Use the same value detection as explicit per-type
                    SetPerTypeValue(famDoc, parameter, p.ValueOrFormula);
                    logs[$"{p.Name}:{currentTypeName}:fallback"] = new LogEntry { Item = p.Name };
                } catch (Exception ex) {
                    logs[$"{p.Name}:{currentTypeName}:fallback"] =
                        new LogEntry { Item = p.Name, Error = ex.Message };
                }
            }
        }

        return new OperationLog(this.Name, logs.Values.ToList());
    }

    /// <summary>
    ///     Set a per-type parameter value using a user-provided string value.
    ///     Rejects values that contain parameter references and strips double-quotes from string literals.
    /// </summary>
    private static void SetPerTypeValue(FamilyDocument famDoc, FamilyParameter parameter, string userValue) {
        var fm = famDoc.FamilyManager;

        // Reject values that contain parameter references (should use SetParamValues for formulas)
        var referencedParams = FormulaUtils.GetReferencedParameters(userValue, fm).ToList();
        if (referencedParams.Any())
            throw new InvalidOperationException(
                $"Per-type value '{userValue}' contains parameter references. Use {nameof(SetParamModel.ValueOrFormula)} (not {nameof(SetParamPerTypeModel.ValuesPertype)}) for formulas.");

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



