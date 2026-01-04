using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeServices.Storage;
using PeServices.Storage.Core.Json.SchemaProcessors;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using PeServices.Storage.Core.Json.SchemaProviders;


namespace AddinFamilyFoundrySuite.Core;

public class BaseProfileSettings {
    [Required] public ExecutionOptions ExecutionOptions { get; init; } = new();
    [Required] public FilterFamiliesSettings FilterFamilies { get; init; } = new();
    [Required] public FilterApsParamsSettings FilterApsParams { get; init; } = new();

    public List<Family> GetFamilies(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(this.FilterFamilies.Filter)
            .ToList();

    /// <summary>
    ///     Gets filtered APS parameter models (no Revit API dependencies).
    ///     These can be safely cached and converted to SharedParameterDefinitions later.
    /// </summary>
    public List<ParamModelRes> GetFilteredApsParamModels() {
        var apsParams = Storage.GlobalDir().StateJson<ParamModel>("parameters-service-cache").Read();
        if (apsParams.Results != null) {
            return apsParams.Results
                .Where(this.FilterApsParams.Filter)
                .Where(p => !p.IsArchived)
                .ToList();
        }

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }

    /// <summary>
    ///     Converts raw APS parameter models to SharedParameterDefinitions using a TempSharedParamFile.
    ///     The TempSharedParamFile must remain alive while the SharedParameterDefinitions are in use.
    /// </summary>
    public static List<SharedParameterDefinition> ConvertToSharedParameterDefinitions(
        List<ParamModelRes> apsParamModels,
        TempSharedParamFile tempFile
    ) =>
        apsParamModels.Select(p => {
            var dlOpts = p.DownloadOptions;
            return new SharedParameterDefinition(
                dlOpts.GetExternalDefinition(tempFile.TempGroup),
                dlOpts.GetGroupTypeId(),
                dlOpts.IsInstance);
        }).ToList();

    public class FilterFamiliesSettings {
        [Required]
        public List<Category> IncludeCategoriesEqualing { get; init; } = [];

        [Required]
        [Description(
            "Filter families by name inclusion. If any include filters are specified (Equaling, Containing, or StartingWith), only families matching at least one filter will pass. If all include filters are empty, all families pass the include check (exclude filters may still apply).")]
        public IncludeFamilies IncludeNames { get; init; } = new();

        [Required]
        [Description(
            "Filter families by name exclusion. If any exclude filters are specified (Equaling, Containing, or StartingWith), families matching any filter will be removed. If all exclude filters are empty, no families are excluded by this filter.")]
        public ExcludeFamilies ExcludeNames { get; init; } = new();

        public bool Filter(Family f) {
            var familyName = f.Name;
            var familyCategory = f.FamilyCategory;

            // Step 1: Filter by category if specified
            if (this.IncludeCategoriesEqualing.Any()) {
                if (familyCategory == null || !this.IncludeCategoriesEqualing.Any(familyCategory.Equals))
                    return false;
            }

            return this.IsNameIncluded(familyName) && !this.IsNameExcluded(familyName);
        }

        private bool IsNameIncluded(string familyName) {
            var hasIncludeFilters = this.IncludeNames.Equaling.Any() ||
                                    this.IncludeNames.Containing.Any() ||
                                    this.IncludeNames.StartingWith.Any();

            if (!hasIncludeFilters) return false;

            return this.IncludeNames.Equaling.Any(familyName.Equals) ||
                   this.IncludeNames.Containing.Any(familyName.Contains) ||
                   this.IncludeNames.StartingWith.Any(familyName.StartsWith);
        }

        private bool IsNameExcluded(string familyName) {
            var hasExcludeFilters = this.ExcludeNames.Equaling.Any() ||
                                    this.ExcludeNames.Containing.Any() ||
                                    this.ExcludeNames.StartingWith.Any();

            if (!hasExcludeFilters) return false;

            return this.ExcludeNames.Equaling.Any(familyName.Equals) ||
                   this.ExcludeNames.Containing.Any(familyName.Contains) ||
                   this.ExcludeNames.StartingWith.Any(familyName.StartsWith);
        }
    }

    public class FilterApsParamsSettings {
        [Required]
        [Description(
            "Filter shared parameters by name inclusion. Exclude everything by default. Only parameters matching at least one include filter will be considered. If all include filters are empty, no parameters pass the include check.")]
        public IncludeSharedParameter IncludeNames { get; init; } = new();

        [Required]
        [Description(
            "Filter shared parameters by name exclusion. Parameters matching any exclude filter are removed from those that passed the include filter.")]
        public ExcludeSharedParameter ExcludeNames { get; init; } = new();

        public bool Filter(ParamModelRes p) {
            if (p == null || string.IsNullOrEmpty(p.Name)) return false;
            return this.IsIncluded(p) && !this.IsExcluded(p);
        }

        private bool IsIncluded(ParamModelRes p) {
            if (this.IncludeNames == null) return false;

            var equaling = this.IncludeNames.Equaling ?? [];
            var containing = this.IncludeNames.Containing ?? [];
            var startingWith = this.IncludeNames.StartingWith ?? [];

            var hasIncludeFilters = equaling.Any() || containing.Any() || startingWith.Any();

            // Exclude everything by default - only include if there are include filters AND the parameter matches
            if (!hasIncludeFilters) return false;

            return equaling.Any(p.Name.Equals) ||
                   containing.Any(p.Name.Contains) ||
                   startingWith.Any(p.Name.StartsWith);
        }

        private bool IsExcluded(ParamModelRes p) {
            if (this.ExcludeNames == null) return false;

            var equaling = this.ExcludeNames.Equaling ?? [];
            var containing = this.ExcludeNames.Containing ?? [];
            var startingWith = this.ExcludeNames.StartingWith ?? [];

            var hasExcludeFilters = equaling.Any() || containing.Any() || startingWith.Any();

            if (!hasExcludeFilters) return false;

            return equaling.Any(p.Name.Equals) ||
                   containing.Any(p.Name.Contains) ||
                   startingWith.Any(p.Name.StartsWith);
        }
    }
}