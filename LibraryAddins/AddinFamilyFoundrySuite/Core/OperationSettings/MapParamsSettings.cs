using PeExtensions.FamDocument.SetValue;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;
public class MapParamsSettings : IOperationSettings {
    [Description("List of parameter remapping rules")]
    [Required]
    public List<MappingDataRecord> MappingData { get; init; } = [];

    public bool Enabled { get; init; } = true;

    public record MappingDataRecord {
        [Description("Current parameter name to map from")]
        [Required]
        public string CurrName { get; init; }

        [Description("New parameter name to map to")]
        [Required]
        public string NewName { get; init; }

        [Description(
            "Coercion strategy to use for the remapping. CoerceByStorageType will be used when none is specified.")]
        public ParamCoercionStrategy MappingStrategy { get; init; } = ParamCoercionStrategy.CoerceByStorageType;

        [JsonIgnore] public bool isProcessed { get; set; } = false;
    }
}