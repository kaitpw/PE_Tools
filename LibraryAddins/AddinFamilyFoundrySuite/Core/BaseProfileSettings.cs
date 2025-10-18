using AddinFamilyFoundrySuite.Core.Operations.Settings;
using System.ComponentModel.DataAnnotations;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using PeServices.Storage;
using PeRevit.Utils;


namespace AddinFamilyFoundrySuite.Core;

public class BaseProfileSettings {
    [Required] public FilterFamiliesSettings FilterFamilies { get; init; } = new();
    [Required] public FilterApsParamsSettings FilterApsParams { get; init; } = new();

    public List<Family> GetFamilies(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(this.FilterFamilies.Filter)
            .ToList();

    public List<ParamModelRes> GetAPSParams() {
        var apsParams = Storage.GlobalState("parameters-service-cache.json").Json<ParamModel>().Read();
        if (apsParams.Results != null) return apsParams.Results
            .Where(this.FilterApsParams.Filter)
            .Where(p => !p.TypedMetadata.IsArchived)
            .ToList();

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }

    public class FilterFamiliesSettings {
        [Required] public List<string> IncludeCategoriesEqualing { get; init; } = [];
        [Required] public Include IncludeNames { get; init; } = new();
        [Required] public Exclude ExcludeNames { get; init; } = new();

        public bool Filter(Family f) {
            var familyName = f.Name;
            var categoryName = f.Category?.Name;

            var anyIncludeNameFilters = this.IncludeNames.Equaling.Any() ||
                                        this.IncludeNames.Containing.Any() ||
                                        this.IncludeNames.StartingWith.Any();

            var nameIncluded = !anyIncludeNameFilters || this.IsNameIncluded(familyName);
            var namePasses = nameIncluded && !this.IsNameExcluded(familyName);

            // Category filter: must check for null because of category-less families like Mullions
            var categoryPasses = categoryName == null ||
                !this.IncludeCategoriesEqualing.Any() ||
                this.IncludeCategoriesEqualing.Any(categoryName.Equals);

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

    // TODO: maybe move this intot he library somewhere
    private Result<ExternalDefinition> GetApsParameterDefinition(
        DefinitionGroup group,
        ParamModelRes apsParamModel) {
        try {
            if (group is null) throw new ArgumentNullException(nameof(group));

            // Extract the actual GUID from Parameters Service ID (similar to your FindParameter method)
            var parameterTypeId = apsParamModel.DownloadOptions.ParameterTypeId;
            var typeId = parameterTypeId.TypeId;
            var typeIdParts = typeId?.Split(':');
            if (typeIdParts == null || typeIdParts.Length < 2)
                throw new ArgumentException($"ParameterTypeId is not of the Parameters Service format: {typeId}");

            var parameterPart = typeIdParts[1];
            var dashIndex = parameterPart.IndexOf('-');
            var guidText = dashIndex > 0 ? parameterPart[..dashIndex] : parameterPart;

            if (!Guid.TryParse(guidText, out var guid))
                throw new ArgumentException($"Could not extract GUID from parameterTypeId: {typeId}");

            // Use the correct data type from Parameters Service
            var dataTypeId = new ForgeTypeId(apsParamModel.SpecId);
            var options = new ExternalDefinitionCreationOptions(apsParamModel.Name, dataTypeId) {
                GUID = guid, // Use the actual Parameters Service GUID
                Visible = apsParamModel.DownloadOptions.Visible,
                UserModifiable = !apsParamModel.ReadOnly,
                Description = apsParamModel.Description ?? ""
            };

            return group.Definitions.Create(options) as ExternalDefinition;

        } catch (Exception ex) {
            return ex;
        }
    }
}