using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class RemapParamsSettings : IOperationSettings {
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

public class RemapParamsOperationTyped : Operation<RemapParamsSettings> {
    public override OperationType Type => OperationType.Type;
    public override string Name => "Remap Parameters";
    public override string Description => "Remap parameter values between parameters for each family type";

    protected override void ExecuteCore(Document doc, RemapParamsSettings settings) {
        foreach (var p in settings.RemapData) {
            try {
                _ = doc.MapValue(p.CurrNameOrId, p.NewNameOrId, p.MappingPolicy);
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}