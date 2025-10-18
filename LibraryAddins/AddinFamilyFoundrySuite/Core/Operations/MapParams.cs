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

    public OperationLog Execute(Document doc) {
        var log = new OperationLog();

        foreach (var p in this.Settings.MappingData) {
            var mappingDesc = $"{p.CurrNameOrId} → {p.NewNameOrId}";

            try {
                var targetParam = doc.FamilyManager.FindParameter(p.NewNameOrId);
                var sourceParam = doc.FamilyManager.FindParameter(p.CurrNameOrId);

                if (targetParam is null || sourceParam is null) {
                    log.Entries.Add(new LogEntry {
                        Item = mappingDesc,
                        Error = "Parameter not found"
                    });
                    continue;
                }

                _ = doc.SetValue(targetParam, sourceParam, p.MappingStrategy);
                log.Entries.Add(new LogEntry {
                    Item = mappingDesc,
                });
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry {
                    Item = mappingDesc,
                    Error = ex.Message
                });
            }
        }

        return log;
    }
}

public class MapParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
    [Description("List of parameter remapping rules")]
    [Required]
    public List<MappingDataRecord> MappingData { get; init; } = [];

    public record MappingDataRecord {
        public string CurrNameOrId { get; set; }
        public string NewNameOrId { get; set; }

        [Description(
            "Coercion strategy to use for the remapping. CoerceByStorageType will be used when none is specified.")]
        public ParamCoercionStrategy MappingStrategy { get; set; } = ParamCoercionStrategy.CoerceByStorageType;
    }
}