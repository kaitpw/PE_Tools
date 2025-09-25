using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Settings;

public class ParamsRemap {
    [Required] public List<RemapDataRecord> RemapData { get; set; } = [];
    public record RemapDataRecord {
        public string CurrNameOrId { get; set; }
        public string NewNameOrId { get; set; }

        [Description("Mapping policy to use for the remapping. Strict will be used when none is specified.")]
        [EnumConstraint("Strict", "AllowStorageTypeCoercion", "PeElectrical")]
        public string MappingPolicy { get; set; } = "AllowStorageTypeCoercion"; // Default policy
    }
}