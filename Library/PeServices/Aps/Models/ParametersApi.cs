using JetBrains.Annotations;
using Newtonsoft.Json;

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
        [UsedImplicitly] public List<ParametersResult> Results { get; set; }

        public class ParametersResult {
            [UsedImplicitly] public string Id { get; set; }
            [UsedImplicitly] public string Name { get; init; }
            [UsedImplicitly] public string Description { get; init; }
            [UsedImplicitly] public string SpecId { get; init; }
            [UsedImplicitly] public string ValueTypeId { get; init; }
            [UsedImplicitly] public bool ReadOnly { get; init; }

            [UsedImplicitly] public List<RawMetadataValue> Metadata { get; init; }

            [UsedImplicitly] public string CreatedBy { get; init; }
            [UsedImplicitly] public string CreatedAt { get; init; }

            [UsedImplicitly][JsonIgnore] public ParametersResultMetadata TypedMetadata => new(this.Metadata);

            [JsonIgnore] public ParameterDownloadOpts DownloadOptions => new(this.Id, this.TypedMetadata);

            // TODO: evaluate is this is the best place for this to be
            public ExternalDefinition GetExternalDefinition(DefinitionGroup group) {
                if (group is null) throw new ArgumentNullException(nameof(group));

                // TODO: move this into a utility class for ForgeTypeId's at some point
                // Extract the actual GUID from Parameters Service ID (similar to your FindParameter method)
                var parameterTypeId = this.DownloadOptions.ParameterTypeId;
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
                var dataTypeId = new ForgeTypeId(this.SpecId);
                var options = new ExternalDefinitionCreationOptions(this.Name, dataTypeId) {
                    GUID = guid, // Use the actual Parameters Service GUID
                    Visible = this.DownloadOptions.Visible,
                    UserModifiable = !this.ReadOnly,
                    Description = this.Description ?? ""
                };

                return group.Definitions.Create(options) as ExternalDefinition;
            }

            public class RawMetadataValue {
                [UsedImplicitly] public string Id { get; init; }
                [UsedImplicitly] public object Value { get; init; }
            }

            public class ParametersResultMetadata {
                // NOTE: Intentionally uses default de/serialization settings inherent in JToken.ToObject
                public ParametersResultMetadata(List<RawMetadataValue> metadata) {
                    foreach (var item in metadata) {
                        _ = item.Id switch {
                            "isHidden" => this.IsHidden = item.Value is bool v && v,
                            "isArchived" => this.IsArchived = item.Value is bool v && v,
                            "instanceTypeAssociation" => this.InstanceTypeAssociation =
                                item.Value is string v ? v : "NONE",
                            "categories" => this.Categories = item.Value is List<Binding> v ? v : null,
                            "labelIds" => this.LabelIds = item.Value is string[] v ? v.ToList() : null,
                            "group" => this.Group = item.Value is Binding v ? v : null,
                            _ => default(object)
                        };
                    }
                }

                public bool IsHidden { get; }
                public string InstanceTypeAssociation { get; }
                public List<Binding> Categories { get; }
                public Binding Group { get; }
                public List<string> LabelIds { get; init; }
                public bool IsArchived { get; init; }

                public class Binding {
                    [UsedImplicitly] public string BindingId { get; init; }
                    [UsedImplicitly] public string Id { get; init; }
                }
            }

            public class ParameterDownloadOpts(string Id, ParametersResultMetadata metadata) {
                public ForgeTypeId ParameterTypeId => new(Id);

                public ForgeTypeId GroupTypeId => // check this logic in testing
                    metadata.Group?.Id != null
                        ? new ForgeTypeId(metadata.Group.Id)
                        : new ForgeTypeId("");

                public bool IsInstance =>
                    metadata.InstanceTypeAssociation?.Equals("INSTANCE", StringComparison.OrdinalIgnoreCase) ?? true;

                public bool Visible => !metadata.IsHidden;

                public ISet<ElementId> CategorySet(Document doc) => // check this logic in testing
                    metadata.Categories?.Any() == true
                        ? MapCategoriesToElementIds(doc, metadata.Categories)
                        : null;

                /// <summary>
                ///     Maps APS category bindings to Revit category ElementIds.
                ///     Extracts category names from APS category IDs and converts them to Revit categories.
                /// </summary>
                private static ISet<ElementId> MapCategoriesToElementIds(
                    Document doc,
                    List<ParametersResultMetadata.Binding> categories
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
                ///     TODO: fix this, it does not do anything
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
            }
        }
    }
}