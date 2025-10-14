using PeExtensions.FamDocument;
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamManager;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams : IOperation<MapParamsSettings> {
    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Type;
    public string Name => "Remap Parameters";
    public string Description => "Remap parameter values between parameters for each family type";

    public void Execute(Document doc) {
        foreach (var p in this.Settings.MappingData) {
            try {
                var targetParam = doc.FamilyManager.FindParameter(p.NewNameOrId);
                var sourceParam = doc.FamilyManager.FindParameter(p.CurrNameOrId);

                _ = doc.SetValue(targetParam, sourceParam, p.MappingStrategy);
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}

public class MapParamsSettings {
    [Description("List of parameter remapping rules")]
    [Required]
    public List<MappingDataRecord> MappingData { get; set; } = [];

    public record MappingDataRecord {
        public string CurrNameOrId { get; set; }
        public string NewNameOrId { get; set; }

        [Description(
            "Coercion strategy to use for the remapping. CoerceByStorageType will be used when none is specified.")]
        public ParamCoercionStrategy MappingStrategy { get; set; } = ParamCoercionStrategy.CoerceByStorageType;
    }
}