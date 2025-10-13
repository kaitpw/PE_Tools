using PeServices.Storage;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddApsParamsOperationTyped : IOperation<AddApsParamsSettings> {
    public AddApsParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Add APS Parameters";
    public string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public void Execute(Document doc) =>
        _ = doc.AddApsParams(this.Settings.ApsParams, this.Settings.Filter);
}

public class AddApsParamsSettings {
    public ParamModel ApsParams => GetAPSParams();

    [Required] public List<string> IncludeNamesEqualing { get; init; } = [];
    [Required] public List<string> ExcludeNamesEqualing { get; init; } = [];
    [Required] public List<string> IncludeNamesContaining { get; init; } = [];
    [Required] public List<string> ExcludeNamesContaining { get; init; } = [];
    [Required] public List<string> IncludeNamesStartingWith { get; init; } = [];
    [Required] public List<string> ExcludeNamesStartingWith { get; init; } = [];

    public PsRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();

    public bool Filter(ParamModelRes p) =>
        Include(this.IncludeNamesEqualing, p.Name.Equals)
        && Exclude(this.ExcludeNamesEqualing, p.Name.Equals)
        && Include(this.IncludeNamesContaining, p.Name.Contains)
        && Exclude(this.ExcludeNamesContaining, p.Name.Contains)
        && Include(this.IncludeNamesStartingWith, p.Name.StartsWith)
        && Exclude(this.ExcludeNamesStartingWith, p.Name.StartsWith);

    private static bool Include<T>(List<T> list, Func<T, bool> predicate) =>
        list.Count == 0 || list.Any(predicate); // Pass if empty OR condition met

    private static bool Exclude<T>(List<T> list, Func<T, bool> predicate) =>
        list.Count == 0 || !list.Any(predicate); // Pass if empty OR condition NOT met

    public static ParamModel GetAPSParams() {
        var apsParams = Storage.GlobalState("parameters-service-cache.json").Json<ParamModel>().Read();
        if (apsParams.Results != null) return apsParams;

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }

    public class PsRecoverFromErrorSettings {
        public bool ReplaceParameterWithMatchingName { get; init; } = true;
    }
}

