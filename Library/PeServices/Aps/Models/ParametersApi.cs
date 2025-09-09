using JetBrains.Annotations;
using System.Text.Json;

namespace PeServices.Aps.Models;

public class ParametersApi {
    public class Groups {
        [UsedImplicitly] public object Pagination { get; init; }
        [UsedImplicitly] public List<GroupResults> Results { get; init; }

        public class GroupResults {
            [UsedImplicitly] public string Id { get; init; }
            [UsedImplicitly] public string Title { get; init; }
            [UsedImplicitly] public string Description { get; init; }
            [UsedImplicitly] public string CreatedBy { get; init; } // make date?
            [UsedImplicitly] public string CreatedAt { get; init; } // make date?
            [UsedImplicitly] public string UpdatedBy { get; init; }
            [UsedImplicitly] public string UpdatedAt { get; init; } // make date?
        }
    }

    public class Collections {
        [UsedImplicitly] public object Pagination { get; init; }

        [UsedImplicitly] public List<CollectionResults> Results { get; init; }

        public class CollectionResults {
            [UsedImplicitly] public string Id { get; init; }
            [UsedImplicitly] public string Title { get; init; }
            [UsedImplicitly] public string Description { get; init; }
            [UsedImplicitly] public FieldId Group { get; init; }
            [UsedImplicitly] public FieldId Account { get; init; }
            [UsedImplicitly] public bool IsArchived { get; init; }
            [UsedImplicitly] public string CreatedBy { get; init; }
            [UsedImplicitly] public string CreatedAt { get; init; }
            [UsedImplicitly] public string UpdatedBy { get; init; }
            [UsedImplicitly] public string UpdatedAt { get; init; }

            public class FieldId {
                [UsedImplicitly] public string Id { get; init; }
            }
        }
    }

    public class Parameters {
        [UsedImplicitly] public List<ParametersResult> Results { get; init; }

        public class ParametersResult {
            [UsedImplicitly] public string Id { get; init; }
            [UsedImplicitly] public string Name { get; init; }
            [UsedImplicitly] public string Description { get; init; }
            [UsedImplicitly] public string SpecId { get; init; }
            [UsedImplicitly] public string ValueTypeId { get; init; }
            [UsedImplicitly] public bool ReadOnly { get; init; }
            [UsedImplicitly] private List<RawMetadataValue> RawMetadata { get; init; }
            [UsedImplicitly] public string CreatedBy { get; init; }
            [UsedImplicitly] public string CreatedAt { get; init; }

            public ParametersResultMetadata Metadata => new(this.RawMetadata);

            public class RawMetadataValue {
                [UsedImplicitly] public string Id { get; init; }
                [UsedImplicitly] public object Value { get; init; }
            }

            public class ParametersResultMetadata {
                public ParametersResultMetadata(List<RawMetadataValue> metadata) {
                    foreach (var item in metadata) {
                        _ = item.Id switch {
                            "isHidden" => this.IsHidden = Convert.ToBoolean(item.Value),
                            "isArchived" => this.IsArchived = Convert.ToBoolean(item.Value),
                            "instanceTypeAssociation" =>
                                this.InstanceTypeAssociation = item.Value.ToString(),
                            "categories" =>
                                this.Categories = item.Value is JsonElement {
                                    ValueKind: JsonValueKind.Array
                                } json
                                    ? JsonSerializer.Deserialize<List<Binding>>(json.GetRawText())
                                    : null,
                            "labelIds" =>
                                this.LabelIds = item.Value is JsonElement {
                                    ValueKind: JsonValueKind.Array
                                } json
                                    ? JsonSerializer.Deserialize<List<string>>(json.GetRawText())
                                    : null,
                            "group" =>
                                this.Group = item.Value is JsonElement groupElem
                                    ? JsonSerializer.Deserialize<Binding>(groupElem.GetRawText())
                                    : null,
                            _ => default(object)
                        };
                    }
                }

                private bool IsHidden { get; }
                private string InstanceTypeAssociation { get; }
                private List<Binding> Categories { get; }
                private Binding Group { get; }
                public List<string> LabelIds { get; init; }
                public bool IsArchived { get; init; }

                public ForgeTypeId GroupTypeId => // check this logic in testing
                    this.Group?.Id != null ? new ForgeTypeId(this.Group.Id) : new ForgeTypeId("ABYV-32458-BXMZ-08934");

                public bool IsInstance =>
                    this.InstanceTypeAssociation?.Equals("INSTANCE", StringComparison.OrdinalIgnoreCase) ?? true;

                public bool Visible => !this.IsHidden;

                public ISet<ElementId> CategorySet(Document doc) => // check this logic in testing
                    this.Categories?.Any() == true
                        ? MapCategoriesToElementIds(doc, this.Categories)
                        : null;

                /// <summary>
                ///     Maps APS category bindings to Revit category ElementIds.
                ///     Extracts category names from APS category IDs and converts them to Revit categories.
                /// </summary>
                private static ISet<ElementId> MapCategoriesToElementIds(
                    Document doc,
                    List<Binding> categories
                ) {
                    var categorySet = new HashSet<ElementId>();

                    foreach (var binding in categories) {
                        var categoryName = ExtractCategoryNameFromApsId(binding.Id);
                        if (string.IsNullOrEmpty(categoryName)) continue;

                        var elementId = GetRevitCategoryElementId(doc, categoryName);
                        if (elementId != null) _ = categorySet.Add(elementId);
                    }

                    return categorySet;
                }

                /// <summary>
                ///     Extracts category name from APS category ID.
                ///     Example: "autodesk.revit.category.family:ductTerminal-1.0.0" -> "ductTerminal"
                /// </summary>
                private static string ExtractCategoryNameFromApsId(string categoryId) {
                    if (string.IsNullOrEmpty(categoryId)) return null;

                    // Extract the part between the last ':' and '-'
                    var colonIndex = categoryId.LastIndexOf(':');
                    var hyphenIndex = categoryId.LastIndexOf('-');

                    return colonIndex >= 0 && hyphenIndex > colonIndex
                        ? categoryId.Substring(colonIndex + 1, hyphenIndex - colonIndex - 1)
                        : null;
                }

                /// <summary>
                ///     Converts APS category name to Revit category ElementId.
                ///     Maps common APS category names to Revit BuiltInCategory values.
                /// </summary>
                private static ElementId GetRevitCategoryElementId(Document doc, string categoryName) {
                    try {
                        // If not found in mapping, try to find by name in all categories
                        foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory))) {
                            try {
                                var category = Category.GetCategory(doc, bic);
                                if (category?.Name?.Contains(categoryName, StringComparison.OrdinalIgnoreCase) == true)
                                    return category.Id;
                            } catch {
                                // Some BuiltInCategory values may not be valid for this document
                            }
                        }

                        return null;
                    } catch (Exception ex) {
                        throw new Exception($"Failed to map category '{categoryName}' to Revit category: {ex.Message}");
                    }
                }

                private class Binding {
                    [UsedImplicitly] public string BindingId { get; init; }
                    [UsedImplicitly] public string Id { get; init; }
                }
            }
        }
    }
}