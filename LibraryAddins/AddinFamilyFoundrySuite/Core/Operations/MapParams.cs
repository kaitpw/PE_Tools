using AddinFamilyFoundrySuite.Core.Operations.Settings;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamManager;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams : IOperation<MapParamsSettings> {
    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Type;

    public string Description => "Map an old parameter's value to a new parameter for each family type";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog(this.GetType().Name);

        foreach (var p in this.Settings.MappingData.Where(m => !m.isProcessed)) {
            var mappingDesc = $"{p.CurrName} → {p.NewName}";

            try {
                var sourceParam = doc.FamilyManager.FindParameter(p.CurrName);
                var targetParam = doc.FamilyManager.FindParameter(p.NewName);

                if (sourceParam is null || targetParam is null) {
                    var notFoundParam = sourceParam is null ? p.CurrName : p.NewName;
                    log.Entries.Add(new LogEntry {
                        Item = mappingDesc,
                        Error = $"{notFoundParam} not found in the family"
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
        [Description("Current parameter name to map from")]
        [Required] public string CurrName { get; init; }

        [Description("New parameter name to map to")]
        [Required] public string NewName { get; init; }

        [Description("Coercion strategy to use for the remapping. CoerceByStorageType will be used when none is specified.")]
        public ParamCoercionStrategy MappingStrategy { get; init; } = ParamCoercionStrategy.CoerceByStorageType;

        [Newtonsoft.Json.JsonIgnore]
        public bool isProcessed { get; set; } = false;
    }
}