using AddinFamilyFoundrySuite.Core.Operations.Settings;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public class BaseProfileSettings {
    [Required] public FilterFamiliesSettings FilterFamilies { get; init; } = new();

    public class FilterFamiliesSettings {
        [Required] public List<string> IncludeCategoriesEqualing { get; init; } = [];
        [Required] public Include IncludeNames { get; init; } = new();
        [Required] public Exclude ExcludeNames { get; init; } = new();

        public bool Filter(Family f) {
            var familyName = f.Name;
            var categoryName = f.Category?.Name;

            var namePasses = this.IsNameIncluded(familyName) || !this.IsNameExcluded(familyName);
            // Category filter: must check for null because of category-less families like Mullions
            var categoryPasses = categoryName == null ||
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
}