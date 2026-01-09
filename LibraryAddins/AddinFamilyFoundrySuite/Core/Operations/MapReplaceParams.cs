using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamParameter;
using PeExtensions.FamParameter.Formula;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapReplaceParams : DocOperation<MapParamsSettings> {
    private readonly Dictionary<string, SharedParameterDefinition> _sharedParamsDict;

    private readonly List<ForgeTypeId> IgnoreCoercionDataTypes = new() {
        SpecTypeId.Number, SpecTypeId.String.Text, SpecTypeId.Length
    };

    public MapReplaceParams(
        MapParamsSettings settings,
        IEnumerable<SharedParameterDefinition> sharedParams
    ) : base(settings) => this._sharedParamsDict = sharedParams.ToDictionary(p => p.ExternalDefinition.Name);

    public override string Description => "Replace a family's existing parameters with APS shared parameters";

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
            var filteredCurrNames = this.Settings.GetRankedCurrParams(
                mapping.CurrNames,
                fm,
                processingContext
            );

            _ = this._sharedParamsDict.TryGetValue(mapping.NewName, out var sharedParam);
            if (sharedParam == null) continue;

            // Try each CurrName in priority order until one succeeds
            var foundMatch = false;
            foreach (var currParam in filteredCurrNames.TakeWhile(_ => !foundMatch)) {
                try {
                    if (currParam.IsBuiltInParameter()) continue;
                    var currParamDataType = currParam.Definition.GetDataType();
                    if (currParamDataType != sharedParam.ExternalDefinition.GetDataType()) continue;

                    var replaced = fm.ReplaceParameter(
                        currParam,
                        sharedParam.ExternalDefinition,
                        sharedParam.GroupTypeId,
                        sharedParam.IsInstance
                    );
                    if (replaced == null) continue;
                    foundMatch = true;
                    this.UnwrapAndLog(doc, currParam, replaced, mapping, log);
                } catch (Exception ex) {
                    logs.Add(new LogEntry(mapping.NewName).Error($"{currParam.Definition.Name} → {mapping.NewName}", ex));
                }
            }
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }

    private void UnwrapAndLog(
        FamilyDocument doc,
        FamilyParameter currParam,
        FamilyParameter replaced,
        MappingData mapping,
        LogEntry log
    ) {
        var parameters = doc.FamilyManager.Parameters;
        var singleReference = parameters.TryGetSingleReference(replaced.Formula);
        if (singleReference != null) {
            var refName = singleReference.Definition.Name;
            var refIsBuiltIn = singleReference.IsBuiltInParameter();
            var refIsInCurrNames = mapping.CurrNames.Contains(refName);

            if (refIsBuiltIn && refIsInCurrNames) {
                // NewName inherited formula pointing to a built-in that's in CurrNames.
                _ = doc.UnsetFormula(replaced);
                const string backlinkOp = nameof(BacklinkParamsToBuiltIn);
                // Don't mark as fully handled - BacklinkParamsToBuiltIn will handle it
                _ = log.Defer($"Replaced {currParam.Definition.Name}, deferred backlink to {refName} to {backlinkOp}");
            } else {
                // Standard unwrap: formula points to non-built-in or not in CurrNames
                _ = doc.UnsetFormula(singleReference);
                _ = log.Success($"{currParam.Definition.Name} → {replaced.Definition.Name}");
            }
        } else if (replaced.Formula == null || parameters.IsConstant(replaced.Formula)) {
            _ = doc.UnsetFormula(replaced);
            var isTerminal = currParam.Definition.GetDataType() == replaced.Definition.GetDataType()
                             || this.IgnoreCoercionDataTypes.Contains(replaced.Definition.GetDataType());
            if (isTerminal) {
                _ = log.Success($"Replaced {currParam.Definition.Name} → {replaced.Definition.Name}");
            } else {
                // Don't mark as handled - MapParams will handle coercion
                _ = log.Defer($"Replaced {currParam.Definition.Name}, awaiting coercion");
            }
        } else {
            // Fallback: formula exists but has no dependencies and is not constant (edge case)
            // we already replaced the parameter, so this is a success
            _ = log.Success($"Replaced {currParam.Definition.Name} → {replaced.Definition.Name}");
        }
    }
}