using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams : IOperation<MapParamsSettings> {
    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Type;
    public string Name => "Remap Parameters";
    public string Description => "Remap parameter values between parameters for each family type";

    public void Execute(Document doc) {
        foreach (var p in this.Settings.RemapData) {
            try {
                _ = doc.MapValue(p.CurrNameOrId, p.NewNameOrId, p.MappingPolicy);
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}

public class MapParamsSettings {
    [Description("List of parameter remapping rules")]
    [Required]
    public List<RemapDataRecord> RemapData { get; set; } = [];

    public record RemapDataRecord {
        public string CurrNameOrId { get; set; }
        public string NewNameOrId { get; set; }

        [Description("Mapping policy to use for the remapping. Strict will be used when none is specified.")]
        [EnumConstraint("Strict", "AllowStorageTypeCoercion", "PeElectrical")]
        public string MappingPolicy { get; set; } = "AllowStorageTypeCoercion"; // Default policy
    }
}