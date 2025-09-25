using PeServices.Aps;
using PeServices.Storage;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;


namespace AddinFamilyFoundrySuite.Core.Settings;

public class ParamsAddPS
{
    [Required] public List<string> IncludeNamesEqualing { get; init; } = [];
    [Required] public List<string> ExcludeNamesEqualing { get; init; } = [];
    [Required] public List<string> IncludeNamesContaining { get; init; } = [];
    [Required] public List<string> ExcludeNamesContaining { get; init; } = [];
    [Required] public List<string> IncludeNamesStartingWith { get; init; } = [];
    [Required] public List<string> ExcludeNamesStartingWith { get; init; } = [];
    public bool Filter(ParamModelRes p) =>
        Include(this.IncludeNamesEqualing, p.Name.Equals)
        && Exclude(this.ExcludeNamesEqualing, p.Name.Equals)
        && Include(this.IncludeNamesContaining, p.Name.Contains)
        && Exclude(this.ExcludeNamesContaining, p.Name.Contains)
        && Include(this.IncludeNamesStartingWith, p.Name.StartsWith)
        && Exclude(this.ExcludeNamesStartingWith, p.Name.StartsWith);

    private static bool Include<T>(List<T> list, Func<T, bool> predicate) =>
    list.Count == 0 || list.Any(predicate);  // Pass if empty OR condition met

    private static bool Exclude<T>(List<T> list, Func<T, bool> predicate) =>
        list.Count == 0 || !list.Any(predicate);  // Pass if empty OR condition NOT met

    public PsRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();

    public class PsRecoverFromErrorSettings
    {
        public bool ReplaceParameterWithMatchingName { get; init; } = true;
    }
}

public class AddParamsFP
{
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    [Required]
    public bool OverrideExistingValues { get; set; } = true;
    // public FpRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();
    //
    // public class FpRecoverFromErrorSettings {
    //     public bool DangerouslyReplaceParameterWithMatchingName;
    // }
}
