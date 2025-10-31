using AddinFamilyFoundrySuite.Core.Operations.Settings;
using PeServices.Storage;
using PeUtils.Files;
using System.ComponentModel.DataAnnotations;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using System.ComponentModel;


namespace AddinFamilyFoundrySuite.Core;

public class BaseProfileSettings {

    [Required] public ExecutionOptionsSettings ExecutionOptions { get; init; } = new();
    [Required] public FilterFamiliesSettings FilterFamilies { get; init; } = new();
    [Required] public FilterApsParamsSettings FilterApsParams { get; init; } = new();
 
    public List<Family> GetFamilies(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(this.FilterFamilies.Filter)
            .ToList();

    public List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        GetAPSParams(TempSharedParamFile tempFile) {
        var apsParams = Storage.GlobalState("parameters-service-cache.json").Json<ParamModel>().Read();
        if (apsParams.Results != null) {
            return apsParams.Results
                .Where(this.FilterApsParams.Filter)
                .Where(p => !p.IsArchived)
                .Select(p => {
                    var dlOpts = p.DownloadOptions;
                    return (dlOpts.GetExternalDefinition(tempFile.TempGroup), dlOpts.GetGroupTypeId(),
                        dlOpts.IsInstance);
                })
                .ToList();
        }

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }

    public class ExecutionOptionsSettings {
        [Description("When enabled, the command will output a JSON file with all APS parameters and families that would be processed, without actually processing them.")]
        [Required]
        public bool PreviewRun { get; set; } = false;

        [Description("When enabled, the command will bundle the operations into a single transaction.")]
        public bool SingleTransaction { get; set; } = true;

        [Description("When enabled, consecutive type operations will be batched together for better performance.")]
        public bool OptimizeTypeOperations { get; set; } = true;

        [Description("When enabled, the command will get the state of certain relevant data in a family.")]
        public string Mode { get; set; } = "";
    }

    public class FilterFamiliesSettings {
        [Required] public List<string> IncludeCategoriesEqualing { get; init; } = [];
        [Required] public Include IncludeNames { get; init; } = new();
        [Required] public Exclude ExcludeNames { get; init; } = new();

        public bool Filter(Family f) {
            var familyName = f.Name;
            var categoryName = f.FamilyCategory?.Name;

            var anyIncludeNameFilters = this.IncludeNames.Equaling.Any() ||
                                        this.IncludeNames.Containing.Any() ||
                                        this.IncludeNames.StartingWith.Any();

            var nameIncluded = !anyIncludeNameFilters || this.IsNameIncluded(familyName);
            var namePasses = nameIncluded && !this.IsNameExcluded(familyName);

            // Category filter: if no category filters specified, all pass; otherwise only matching categories pass
            var categoryPasses = !this.IncludeCategoriesEqualing.Any() ||
                                 (categoryName != null && this.IncludeCategoriesEqualing.Any(categoryName.Equals));

            return namePasses && categoryPasses;
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
        [Required] public Include IncludeNames { get; init; } = new();
        [Required] public Exclude ExcludeNames { get; init; } = new();

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