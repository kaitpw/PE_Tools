using AddinFamilyFoundrySuite.Core.OperationSettings;
using AddinFamilyFoundrySuite.Core.SchemaProviders;
using PeServices.Storage;
using PeServices.Storage.Core.Json.SchemaProcessors;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;


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

    public List<SharedParameterDefinition> GetAPSParams(TempSharedParamFile tempFile) {
        var apsParams = Storage.GlobalDir().StateJson<ParamModel>("parameters-service-cache").Read();
        if (apsParams.Results != null) {
            return apsParams.Results
                .Where(this.FilterApsParams.Filter)
                .Where(p => !p.IsArchived)
                .Select(p => {
                    var dlOpts = p.DownloadOptions;
                    return new SharedParameterDefinition(
                        dlOpts.GetExternalDefinition(tempFile.TempGroup),
                        dlOpts.GetGroupTypeId(),
                        dlOpts.IsInstance);
                })
                .ToList();
        }

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }

    public class FilterFamiliesSettings {
        [Required]
        [SchemaExamples(typeof(CategoryNamesProvider))]
        public List<string> IncludeCategoriesEqualing { get; init; } = [];
        [Required]
        [Description("Filter families by name inclusion. If any include filters are specified (Equaling, Containing, or StartingWith), only families matching at least one filter will pass. If all include filters are empty, all families pass the include check (exclude filters may still apply).")]
        public IncludeFamilies IncludeNames { get; init; } = new();
        [Required]
        [Description("Filter families by name exclusion. If any exclude filters are specified (Equaling, Containing, or StartingWith), families matching any filter will be removed. If all exclude filters are empty, no families are excluded by this filter.")]
        public ExcludeFamilies ExcludeNames { get; init; } = new();

        public bool Filter(Family f) {
            var familyName = f.Name;
            var categoryName = f.FamilyCategory?.Name;

            // Step 1: Filter by category if specified
            if (this.IncludeCategoriesEqualing.Any()) {
                if (categoryName == null || !this.IncludeCategoriesEqualing.Any(categoryName.Equals))
                    return false;
            }

            // Step 2: Filter by includes if specified (otherwise all pass)
            var hasIncludeFilters = this.IncludeNames.Equaling.Any() ||
                                    this.IncludeNames.Containing.Any() ||
                                    this.IncludeNames.StartingWith.Any();

            if (hasIncludeFilters) {
                if (!this.IsNameIncluded(familyName))
                    return false;
            }

            // Step 3: Filter by excludes if specified (otherwise all pass)
            var hasExcludeFilters = this.ExcludeNames.Equaling.Any() ||
                                    this.ExcludeNames.Containing.Any() ||
                                    this.ExcludeNames.StartingWith.Any();

            if (hasExcludeFilters) {
                if (this.IsNameExcluded(familyName))
                    return false;
            }

            return true;
        }

        private bool IsNameIncluded(string familyName) =>
            this.IncludeNames.Equaling.Any(familyName.Equals) ||
            this.IncludeNames.Containing.Any(familyName.Contains) ||
            this.IncludeNames.StartingWith.Any(familyName.StartsWith);

        private bool IsNameExcluded(string familyName) =>
            this.ExcludeNames.Equaling.Any(familyName.Equals) ||
            this.ExcludeNames.Containing.Any(familyName.Contains) ||
            this.ExcludeNames.StartingWith.Any(familyName.StartsWith);
    }

    public class FilterApsParamsSettings {
        [Required]
        [Description("Filter shared parameters by name inclusion. Used with ExcludeNames in an OR logic: parameters pass if they match any include filter OR if they don't match any exclude filter. If all include filters are empty, only the exclude filter applies.")]
        public IncludeSharedParameter IncludeNames { get; init; } = new();
        [Required]
        [Description("Filter shared parameters by name exclusion. Used with IncludeNames in an OR logic: parameters pass if included OR not excluded. Parameters matching any exclude filter are removed only if they also don't match any include filter.")]
        public ExcludeSharedParameter ExcludeNames { get; init; } = new();

        public bool Filter(ParamModelRes p) => this.IsIncluded(p) || !this.IsExcluded(p);

        private bool IsIncluded(ParamModelRes p) =>
            this.IncludeNames.Equaling.Any(p.Name.Equals) ||
            this.IncludeNames.Containing.Any(p.Name.Contains) ||
            this.IncludeNames.StartingWith.Any(p.Name.StartsWith);

        private bool IsExcluded(ParamModelRes p) =>
            this.ExcludeNames.Equaling.Any(p.Name.Equals) ||
            this.ExcludeNames.Containing.Any(p.Name.Contains) ||
            this.ExcludeNames.StartingWith.Any(p.Name.StartsWith);
    }
}