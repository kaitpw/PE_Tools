using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;
using PeExtensions.FamParameter;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Creates backlinks from built-in parameters to their mapped shared parameter targets.
///     Sets formulas like: Model = PE_G___Model, so the built-in derives from the shared param.
/// </summary>
public class BacklinkParamsToBuiltIn(MapParamsSettings settings)
    : DocOperation<MapParamsSettings>(settings) {
    public override string Description => "Create backlinks from built-in params to their mapped targets";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        if (groupContext is null)
            throw new InvalidOperationException($"{this.Name} requires a GroupContext (must be used within an OperationGroup)");

        var fm = doc.FamilyManager;

        var data = groupContext.GetAllInComplete().Select(e => {
            var mapping = this.Settings.MappingData.First(m => e.Key == m.NewName);
            return (mapping, e.Value);
        });

        foreach (var (mapping, log) in data) {
            var tgtParam = fm.FindParameter(mapping.NewName);
            if (tgtParam == null) continue;

            // Find first built-in in CurrNames (priority order) and backlink it
            foreach (var currName in mapping.CurrNames) {
                var srcParam = fm.FindParameter(currName);
                if (srcParam == null) continue;
                if (!srcParam.IsBuiltInParameter()) continue;

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