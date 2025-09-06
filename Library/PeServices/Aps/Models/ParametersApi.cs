using JetBrains.Annotations;
using System.Text.Json;

namespace PeServices.Aps.Models;

public class ParametersApi {
    public class Groups {
        [UsedImplicitly] public object Pagination { get; init;}
        [UsedImplicitly] public List<GroupResults> Results { get; init;}

        public class GroupResults {
            [UsedImplicitly] public string Id { get; init;}
            [UsedImplicitly] public string Title { get; init;}
            [UsedImplicitly] public string Description { get; init;}
            [UsedImplicitly] public string CreatedBy { get; init;} // make date?
            [UsedImplicitly] public string CreatedAt { get; init;} // make date?
            [UsedImplicitly] public string UpdatedBy { get; init;}
            [UsedImplicitly] public string UpdatedAt { get; init;} // make date?
        }
    }

    public class Collections {
        [UsedImplicitly] public object Pagination { get; init;}

        [UsedImplicitly] public List<CollectionResults> Results { get; init;}

        public class CollectionResults {
            [UsedImplicitly] public string Id { get; init;}
            [UsedImplicitly] public string Title { get; init;}
            [UsedImplicitly] public string Description { get; init;}
            [UsedImplicitly] public FieldId Group { get; init;}
            [UsedImplicitly] public FieldId Account { get; init;}
            [UsedImplicitly] public bool IsArchived { get; init;}
            [UsedImplicitly] public string CreatedBy { get; init;}
            [UsedImplicitly] public string CreatedAt { get; init;}
            [UsedImplicitly] public string UpdatedBy { get; init;}
            [UsedImplicitly] public string UpdatedAt { get; init;}

            public class FieldId {
                [UsedImplicitly] public string Id { get; init;}
            }
        }
    }
    
    public class Parameters {
    [UsedImplicitly] public List<ParametersResult> Results { get; init;}
    
    public class ParametersResult {
        [UsedImplicitly] public string Id { get; init;}
        [UsedImplicitly] public string Name { get; init;}
        [UsedImplicitly] public string Description { get; init;}
        [UsedImplicitly] public string SpecId { get; init;}
        [UsedImplicitly] public string ValueTypeId { get; init;}
        [UsedImplicitly] public bool ReadOnly { get; init;}
        [UsedImplicitly] private List<RawMetadataValue> RawMetadata { get; init;}
        [UsedImplicitly] public string CreatedBy { get; init;}
        [UsedImplicitly] public string CreatedAt { get; init;}

        public ParametersResultMetadata Metadata => new(this.RawMetadata);

        public class RawMetadataValue {
            [UsedImplicitly] public string Id { get; init;}
            [UsedImplicitly] public object Value { get; init;}
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

            public bool IsHidden { get; init;}
            public bool IsArchived { get; init;}
            public string InstanceTypeAssociation { get; init;}
            public List<Binding> Categories { get; init;}
            public List<string> LabelIds { get; init;}
            public Binding Group { get; init;}

            public class Binding {
                [UsedImplicitly] public string BindingId { get; init;}
                [UsedImplicitly] public string Id { get; init;}
            }
        }
    }
}
}