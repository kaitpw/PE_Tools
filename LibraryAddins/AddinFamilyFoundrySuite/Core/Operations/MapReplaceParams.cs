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
        if (groupContext is null) {
            throw new InvalidOperationException(
                $"{this.Name} requires a GroupContext (must be used within an OperationGroup)");
        }

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

            if (mapping.NewName == "PE_E___Voltage") filteredCurrNames.ForEach(x => Debug.WriteLine(x));

            _ = this._sharedParamsDict.TryGetValue(mapping.NewName, out var sharedParam);
            if (sharedParam == null) continue;

            // Try each CurrName in priority order until one succeeds
            var foundMatch = false;
            foreach (var currParam in filteredCurrNames.TakeWhile(_ => !foundMatch)) {
                try {
                    var currParamName = currParam.Definition.Name;
                    var currParamDataType = currParam.Definition.GetDataType();
                    if (currParam.IsBuiltInParameter()) continue;
                    if (currParamDataType != sharedParam.ExternalDefinition.GetDataType()) {
                        // Log a message to show user that their priority is respected.
                        _ = log.Defer(
                            $"{sharedParam.ExternalDefinition.Name} cannot replace {currParamName} due to datatype mismatch");
                        continue;
                    }

                    var replaced = fm.ReplaceParameter(
                        currParam,
                        sharedParam.ExternalDefinition,
                        sharedParam.GroupTypeId,
                        sharedParam.IsInstance
                    );
                    if (replaced == null) continue;
                    foundMatch = true;
                    this.LogAndUnwrap(doc, log, mapping, currParamName, currParamDataType, replaced);
                } catch {
                    // Not terminated as error because we must allow retrying later
                    _ = log.Defer($"Failed to map {currParam.Definition.Name} → {mapping.NewName}");
                }
            }
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }

    /// <summary>
    ///     Logs the replaced parameter and attempts to unwrap it.
    /// </summary>
    private void LogAndUnwrap(
        FamilyDocument doc,
        LogEntry log,
        MappingData mapping,
        string currParamName,
        ForgeTypeId currParamDataType,
        FamilyParameter replaced
    ) {
        var parameters = doc.FamilyManager.Parameters;
        var singleReference = parameters.TryGetSingleReference(replaced.Formula);

        if (singleReference != null) {
            var refName = singleReference.Definition.Name;
            var refIsBuiltIn = singleReference.IsBuiltInParameter();
            var refIsInCurrNames = mapping.CurrNames.Contains(refName);

            if (refIsBuiltIn && refIsInCurrNames) {
                // Don't mark as fully handled - BacklinkParamsToBuiltIn will handle it
                const string backlinkOp = nameof(BacklinkParamsToBuiltIn);
                _ = log.Defer($"Replaced {currParamName}, deferred backlink to {refName} to {backlinkOp}");
                _ = doc.UnsetFormula(replaced); // NewName inherited formula pointing to a CurrNames built-in
            } else {
                _ = log.Success($"{currParamName} → {replaced.Definition.Name}");
                _ = doc.UnsetFormula(singleReference); // Unwrap, formula points to non-CurrNames OR non-built-in 
            }
        } else if (replaced.Formula == null || parameters.IsConstant(replaced.Formula)) {
            var isTerminal = currParamDataType == replaced.Definition.GetDataType()
                             || this.IgnoreCoercionDataTypes.Contains(replaced.Definition.GetDataType());
            _ = isTerminal
                ? log.Success($"Replaced {currParamName} → {replaced.Definition.Name}")
                : log.Defer($"Replaced {currParamName}, awaiting coercion");
            _ = doc.UnsetFormula(replaced);
        } else {
            // Fallback: formula exists but has no dependencies and is not constant (edge case)
            // we already replaced the parameter, so this is a success
            _ = log.Success($"Replaced {currParamName} → {replaced.Definition.Name}");
        }
    }
}