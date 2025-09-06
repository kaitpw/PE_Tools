using JetBrains.Annotations;
using System.Text.Json;

namespace PeServices.Aps.Models;

public class ParametersApi {
    public class Groups {
        [UsedImplicitly] public object Pagination { get; }
        [UsedImplicitly] public List<GroupResults> Results { get; }

        public class GroupResults {
            [UsedImplicitly] public string Id { get; }
            [UsedImplicitly] public string Title { get; }
            [UsedImplicitly] public string Description { get; }
            [UsedImplicitly] public string CreatedBy { get; } // make date?
            [UsedImplicitly] public string CreatedAt { get; } // make date?
            [UsedImplicitly] public string UpdatedBy { get; }
            [UsedImplicitly] public string UpdatedAt { get; } // make date?
        }
    }

    public class Collections {
        [UsedImplicitly] public object Pagination { get; }

        [UsedImplicitly] public List<CollectionResults> Results { get; }

        public class CollectionResults {
            [UsedImplicitly] public string Id { get; }
            [UsedImplicitly] public string Title { get; }
            [UsedImplicitly] public string Description { get; }
            [UsedImplicitly] public FieldId Group { get; }
            [UsedImplicitly] public FieldId Account { get; }
            [UsedImplicitly] public bool IsArchived { get; }
            [UsedImplicitly] public string CreatedBy { get; }
            [UsedImplicitly] public string CreatedAt { get; }
            [UsedImplicitly] public string UpdatedBy { get; }
            [UsedImplicitly] public string UpdatedAt { get; }

            public class FieldId {
                [UsedImplicitly] public string Id { get; }
            }
        }
    }
    
    public class Parameters {
    [UsedImplicitly] public List<ParametersResult> Results { get; }
    
    public class ParametersResult {
        [UsedImplicitly] public string Id { get; }
        [UsedImplicitly] public string Name { get; }
        [UsedImplicitly] public string Description { get; }
        [UsedImplicitly] public string SpecId { get; }
        [UsedImplicitly] public string ValueTypeId { get; }
        [UsedImplicitly] public bool ReadOnly { get; }
        [UsedImplicitly] private List<RawMetadataValue> RawMetadata { get; }
        [UsedImplicitly] public string CreatedBy { get; }
        [UsedImplicitly] public string CreatedAt { get; }

        public ParametersResultMetadata Metadata => new(this.RawMetadata);

        public class RawMetadataValue {
            [UsedImplicitly] public string Id { get; }
            [UsedImplicitly] public object Value { get; }
        }

        public class ParametersResultMetadata {
            public ParametersResultMetadata(List<RawMetadataValue> metadata) {
                foreach (var item in metadata) {
                    _ = item.Id switch {
                        "isHidden" => this.IsHidden = Convert.ToBoolean(item.Value),
                        "isArchived" => this.IsArchived = Convert.ToBoolean(item.Value),
                        "instanceTypeAssociation" => this.InstanceTypeAssociation = item.Value.ToString() ?? "INSTANCE",
                        "categories" => this.Categories = item.Value is JsonElement {
                            ValueKind: JsonValueKind.Array
                        } json
                            ? JsonSerializer.Deserialize<List<Binding>>(json.GetRawText())
                            : null,
                        "labelIds" => this.LabelIds = item.Value is JsonElement { ValueKind: JsonValueKind.Array } json
                            ? JsonSerializer.Deserialize<List<string>>(json.GetRawText())
                            : null,
                        "group" => this.Group = item.Value is JsonElement groupElem
                            ? JsonSerializer.Deserialize<Binding>(groupElem.GetRawText())
                            : null,
                        _ => default(object)
                    };
                }
            }

            public bool IsHidden { get; }
            public bool IsArchived { get; }
            public string InstanceTypeAssociation { get; }
            public List<Binding> Categories { get; }
            public List<string> LabelIds { get; }
            public Binding Group { get; }

            public class Binding {
                [UsedImplicitly] public string BindingId { get; }
                [UsedImplicitly] public string Id { get; }
            }
        }
    }
}
}