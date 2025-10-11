using PeServices.Storage;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ParamServiceModel = PeServices.Aps.Models.ParametersApi.Parameters;

namespace AddinFamilyFoundrySuite.Core.Settings;

public class BaseSettings<T> where T : BaseProfileSettings, new() {
    [Description(
        "Use cached Parameters Service data instead of downloading from APS on every run. " +
        "Only set to true if you are sure no one has changed the param definitions since the last time you opened Revit " +
        "and/or you are running this command in quick succession.")]
    [Required]
    public bool UseCachedParametersServiceData { get; set; } = true;

    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

    [Description(
        "Current profile to use for the command. This determines which profile is used in the next launch of a command.")]
    [Required]
    public string CurrentProfile { get; set; } = "Default";

    [Description(
        "Profiles for the command. The profile that a command uses is determined by the `CurrentProfile` property.")]
    [Required]
    public Dictionary<string, T> Profiles { get; set; } = new() { { "Default", new T() } };

    public T GetProfile() => this.Profiles[this.CurrentProfile];

    public ParamServiceModel GetAPSParams() =>
        Storage.GlobalState("parameters-service-cache.json").Json<ParamServiceModel>().Read();
}

public class BaseProfileSettings {
    [Required] public FilterFamiliesSettings FilterFamilies { get; init; } = new();

    public class FilterFamiliesSettings {
        [Required] public List<string> IncludeCategoriesEqualing { get; init; } = [];
        [Required] public List<string> ExcludeCategoriesEqualing { get; init; } = [];
        [Required] public List<string> IncludeNamesEqualing { get; init; } = [];
        [Required] public List<string> ExcludeNamesEqualing { get; init; } = [];
        [Required] public List<string> IncludeNamesContaining { get; init; } = [];
        [Required] public List<string> ExcludeNamesContaining { get; init; } = [];
        [Required] public List<string> IncludeNamesStartingWith { get; init; } = [];
        [Required] public List<string> ExcludeNamesStartingWith { get; init; } = [];

        public bool Filter(Family f) {
            var categoryName = f.Category?.Name;
            var familyName = f.Name;

            // must check for null because of category-less families like Mullions
            return (categoryName == null || Include(this.IncludeCategoriesEqualing, categoryName.Equals))
                   && (categoryName == null || Exclude(this.ExcludeCategoriesEqualing, categoryName.Equals))
                   && Include(this.IncludeNamesEqualing, familyName.Equals)
                   && Exclude(this.ExcludeNamesEqualing, familyName.Equals)
                   && Include(this.IncludeNamesContaining, familyName.Contains)
                   && Exclude(this.ExcludeNamesContaining, familyName.Contains)
                   && Include(this.IncludeNamesStartingWith, familyName.StartsWith)
                   && Exclude(this.ExcludeNamesStartingWith, familyName.StartsWith);
        }

        private static bool Include<T>(List<T> list, Func<T, bool> predicate) =>
            list.Count == 0 || list.Any(predicate); // Pass if empty OR condition met

        private static bool Exclude<T>(List<T> list, Func<T, bool> predicate) =>
            list.Count == 0 || !list.Any(predicate); // Pass if empty OR condition NOT met
    }
}