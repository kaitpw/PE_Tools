using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Creates backlinks from built-in parameters to their mapped shared parameter targets.
///     Sets formulas like: Model = PE_G___Model, so the built-in derives from the shared param.
/// </summary>
public class BacklinkParamsToBuiltIn(MapParamsSettings settings)
    : DocOperation<MapParamsSettings>(settings) {
    public override string Description => "Create backlinks from built-in params to their mapped targets";

    public override OperationLog Execute(FamilyDocument doc, FamilyProcessingContext processingContext, OperationContext groupContext) {
        var fm = doc.FamilyManager;

        foreach (var mapping in this.Settings.MappingData) {
            var log = groupContext.GetOrCreate(mapping.NewName);
            // Only process if deferred or pending - skip if already completed/errored
            if (log.IsComplete) continue;

            var tgtParam = fm.FindParameter(mapping.NewName);
            if (tgtParam == null) continue;

            // Find first built-in in CurrNames (priority order) and backlink it
            foreach (var currName in mapping.CurrNames) {
                var srcParam = fm.FindParameter(currName);
                if (srcParam == null) continue;
                if (!ParameterUtils.IsBuiltInParameter(srcParam.Id)) continue;

                // Set formula: BuiltIn = NewParam
                var success = doc.TrySetFormulaFast(srcParam, mapping.NewName, out var err);
                _ = success
                    ? log.Success($"Backlink {mapping.NewName} → {currName}")
                    : log.Error($"Backlink {mapping.NewName} → {currName}: {err}");
                break; // Only backlink first matching built-in per mapping
            }
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }
}
