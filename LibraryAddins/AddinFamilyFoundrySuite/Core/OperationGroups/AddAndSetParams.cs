using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using Nice3point.Revit.Extensions;

namespace AddinFamilyFoundrySuite.Core.OperationGroups;

/// <summary>
///     Operation group that optionally creates missing parameters, then sets their values/formulas.
///     Execution order:
///     1. AddParamsFromSettings (if CreateIfMissing=true) - creates missing family params
///     2. SetParamValues - sets formulas (default) or global values based on SetAsFormula property
///     3. SetParamValuesPerType - handles explicit per-type values and failed global value fallbacks
/// </summary>
public class AddAndSetParams : OperationGroup<AddAndSetParamsSettings> {
    public AddAndSetParams(AddAndSetParamsSettings settings) : base(
        InitializeDescription(settings), InitializeOperations(settings, out var sharedState)
    ) => this.SharedState = sharedState;

    /// <summary>
    ///     Shared state for coordinating fallback between SetParamValues and SetParamValuesPerType.
    /// </summary>
    public SetParamSharedState SharedState { get; }

    public static string InitializeDescription(AddAndSetParamsSettings settings) =>
        $"Set a parameter within the family to a value or formula. " +
        $"By default, values are set as formulas (even simple numbers/text). Use <{nameof(SetParamModel.SetAsFormula)}>=false to set as values instead. " +
        $"If <{nameof(settings.OverrideExistingValues)}> is true, then existing parameter values will be overwritten. " +
        $"If <{nameof(settings.CreateFamParamIfMissing)}> is true, then a family parameter will be created " +
        $"with <{nameof(AddAndSetData.Name)}>. The default values of the parameter are:" +
        $"\n\t<{nameof(AddAndSetData.PropertiesGroup)}>: <{new AddAndSetData().PropertiesGroup.ToLabel()}>" +
        $"\n\t<{nameof(AddAndSetData.DataType)}>: <{new AddAndSetData().DataType.ToLabel()}>>" +
        $"\n\t<{nameof(AddAndSetData.IsInstance)}>: <{GetDesignation(new AddAndSetData().IsInstance)}>";

    private static string GetDesignation(bool isInstance) => isInstance ? "Instance" : "Type";

    private static List<IOperation<AddAndSetParamsSettings>> InitializeOperations(
        AddAndSetParamsSettings settings,
        out SetParamSharedState sharedState
    ) {
        var ops = new List<IOperation<AddAndSetParamsSettings>>();
        sharedState = new SetParamSharedState();

        // 1. Optionally create missing params first
        if (settings.CreateFamParamIfMissing)
            ops.Add(new AddParamsFromSettings(settings));

        // 2. Set global/formula values (with per-type fallback tracking)
        if (settings.Parameters.Any()) {
            ops.Add(new SetParamValues(settings, sharedState));

            // 3. Set explicit per-type values AND handle fallbacks from SetParamValues failures
            if (settings.ParametersPerType.Any())
                ops.Add(new SetParamValuesPerType(settings, sharedState));
        }

        return ops;
    }
}

/// <summary>
///     Shared state between SetParamValues and SetParamValuesPerType operations.
///     Tracks which parameters failed SetGlobalValue and need per-type fallback.
/// </summary>
public class SetParamSharedState {
    public HashSet<string> FailedGlobalValueParams { get; } = [];
}