using PeServices.Documents;
using PeServices.Storage.Core.Json.SchemaProcessors;

namespace AddinFamilyFoundrySuite.Core.SchemaProviders;

/// <summary>
///     Provides category names from the active Revit document for JSON schema examples.
///     Used to enable LSP autocomplete for category name properties.
///     Returns empty list if no document is available (schema generation context).
/// </summary>
public class CategoryNamesProvider : ISchemaExamplesProvider {
    public IEnumerable<string> GetExamples() {
        try {
            var doc = DocumentManager.GetActiveDocument();
            if (doc == null) return [];

            var categories = doc.Settings.Categories;
            return categories.Cast<Category>()
                .Select(c => c.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name);
        } catch {
            // No document available or error - no examples, no crash
            return [];
        }
    }
}

