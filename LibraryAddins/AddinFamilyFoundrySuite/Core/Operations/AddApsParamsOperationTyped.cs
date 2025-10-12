using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddApsParamsSettings : IOperationSettings {
    [Description("APS Parameters data")] public ParamModel ApsParams { get; set; }

    [Description("Filter function for APS parameters")]
    [Required]
    public List<string> IncludeNamesEqualing { get; init; } = [];

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

    public class PsRecoverFromErrorSettings {
        public bool ReplaceParameterWithMatchingName { get; init; } = true;
    }
}

public class AddApsParamsOperationTyped : Operation<AddApsParamsSettings> {
    public override OperationType Type => OperationType.Doc;
    public override string Name => "Add APS Parameters";
    public override string Description => "Download and add shared parameters from Autodesk Parameters Service";

    protected override void ExecuteCore(Document doc, AddApsParamsSettings settings) =>
        _ = doc.AddApsParams(settings.ApsParams, settings.Filter);
}