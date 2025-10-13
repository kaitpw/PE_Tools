using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public class BaseProfileSettings
{
    [Required] public FilterFamiliesSettings FilterFamilies { get; init; } = new();

    public class FilterFamiliesSettings
    {
        [Required] public List<string> IncludeCategoriesEqualing { get; init; } = [];
        [Required] public List<string> ExcludeCategoriesEqualing { get; init; } = [];
        [Required] public List<string> IncludeNamesEqualing { get; init; } = [];
        [Required] public List<string> ExcludeNamesEqualing { get; init; } = [];
        [Required] public List<string> IncludeNamesContaining { get; init; } = [];
        [Required] public List<string> ExcludeNamesContaining { get; init; } = [];
        [Required] public List<string> IncludeNamesStartingWith { get; init; } = [];
        [Required] public List<string> ExcludeNamesStartingWith { get; init; } = [];

        public bool Filter(Family f)
        {
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