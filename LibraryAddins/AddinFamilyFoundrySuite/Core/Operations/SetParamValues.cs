using AddinFamilyFoundrySuite.Core.OperationSettings;
using AddinFamilyFoundrySuite.Core.OperationGroups;
using PeExtensions;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;
using PeExtensions.FamDocument.GetValue;

namespace AddinFamilyFoundrySuite.Core.Operations;



/// <summary>
///     Sets parameter values or formulas based on SetAsFormula property.
///     - If SetAsFormula is true (default) → SetFormula (applies to all types, "locks" the parameter)
///     - If SetAsFormula is false → SetGlobalValue (fast path for all types at once)
///     On SetGlobalValue failure, allows the error to pass through - SetParamValuesPerType will pick it up as fallback.
/// </summary>
public class SetParamValues(AddAndSetParamsSettings settings, SetParamSharedState sharedState = null)
    : DocOperation<AddAndSetParamsSettings>(settings) {

    public override string Description =>
        "Set parameter values or formulas based on SetAsFormula property.";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new Dictionary<string, LogEntry>();
        var fm = doc.FamilyManager;

        foreach (var p in this.Settings.Parameters) {
            if (string.IsNullOrWhiteSpace(p.ValueOrFormula)) continue;

            var parameter = fm.FindParameter(p.Name);
            if (parameter is null) {
                logs[p.Name] = new LogEntry { Item = p.Name, Error = $"Parameter '{p.Name}' not found" };
                continue;
            }

            if (!this.Settings.OverrideExistingValues && doc.HasValue(parameter)) {
                logs[p.Name] = new LogEntry { Item = p.Name };
                continue;
            }

            try {
                var result = SetValueOrFormula(doc, parameter, p.ValueOrFormula, p.SetAsFormula);
                if (result.NeedsFallback && sharedState is not null)
                    _ = sharedState.FailedGlobalValueParams.Add(p.Name);

                logs[p.Name] = new LogEntry { Item = p.Name };
            } catch (Exception ex) {
                logs[p.Name] = new LogEntry { Item = p.Name, Error = ex.Message };
            }
        }

        return new OperationLog(this.Name, logs.Values.ToList());
    }

    private static SetResult SetValueOrFormula(FamilyDocument doc, FamilyParameter param, string valueOrFormula, bool setAsFormula) {
        if (setAsFormula) {
            _ = doc.SetFormula(param, valueOrFormula);
            return SetResult.Success;
        }

        try {
            var result = doc.SetGlobalValue(param, valueOrFormula);
            return result is not null ? SetResult.Success : SetResult.NeedsFallbackResult;
        } catch {
            // SetGlobalValue failed (e.g., Force datatype issues) - needs per-type fallback
            return SetResult.NeedsFallbackResult;
        }
    }

    private readonly struct SetResult(bool needsFallback) {
        public bool NeedsFallback { get; } = needsFallback;
        public static SetResult Success => new(false);
        public static SetResult NeedsFallbackResult => new(true);
    }
}



