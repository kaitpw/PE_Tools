using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.GetValue;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Sets parameter values or formulas based on SetAsFormula property.
///     - If SetAsFormula is true (default) → SetFormula (applies to all types, "locks" the parameter)
///     - If SetAsFormula is false → SetGlobalValue (fast path for all types at once)
///     On SetGlobalValue failure, defers to SetParamValuesPerType via GroupContext.
/// </summary>
public class SetParamValues(AddAndSetParamsSettings settings)
    : DocOperation<AddAndSetParamsSettings>(settings) {
    public override string Description =>
        "Set parameter values or formulas based on SetAsFormula property.";

    public override OperationLog Execute(FamilyDocument doc, FamilyProcessingContext processingContext, OperationContext groupContext) {
        var fm = doc.FamilyManager;

        foreach (var p in this.Settings.Parameters) {
            var log = groupContext.GetOrCreate(p.Name);

            if (string.IsNullOrWhiteSpace(p.ValueOrFormula)) continue;

            var parameter = fm.FindParameter(p.Name);
            if (parameter is null) {
                _ = log.Error($"Parameter '{p.Name}' not found");
                continue;
            }

            if (!this.Settings.OverrideExistingValues && doc.HasValue(parameter)) {
                _ = log.Skip("Already has value");
                continue;
            }

            try {
                var result = SetValueOrFormula(doc, parameter, p.ValueOrFormula, p.SetAsFormula);
                _ = result.NeedsFallback
                    ? log.Defer("Needs per-type fallback")
                    : log.Success("Set global value");
            } catch (Exception ex) {
                _ = log.Error(ex);
            }
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }

    private static SetResult SetValueOrFormula(FamilyDocument doc,
        FamilyParameter param,
        string valueOrFormula,
        bool setAsFormula
    ) {
        try {
            if (setAsFormula) {
                var success = doc.TrySetFormula(param, valueOrFormula, out _);
                return success ? SetResult.Success : SetResult.NeedsFallbackResult;
            } else {
                var success = doc.SetGlobalValue(param, valueOrFormula);
                return success ? SetResult.Success : SetResult.NeedsFallbackResult;
            }
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