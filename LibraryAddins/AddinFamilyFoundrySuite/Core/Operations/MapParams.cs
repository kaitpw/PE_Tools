using Newtonsoft.Json;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamManager;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams(MapParamsSettings settings) : TypeOperation<MapParamsSettings>(settings) {
    public override string Description => "Map an old parameter's value to a new parameter for each family type";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        foreach (var p in this.Settings.MappingData.Where(m => !m.isProcessed)) {
            var mappingDesc = $"{p.CurrName} → {p.NewName}";

            try {
                var sourceParam = doc.FamilyManager.FindParameter(p.CurrName);
                var targetParam = doc.FamilyManager.FindParameter(p.NewName);

                if (sourceParam is null || targetParam is null) {
                    var notFoundParam = sourceParam is null ? p.CurrName : p.NewName;
                    logs.Add(new LogEntry { Item = mappingDesc, Error = $"{notFoundParam} not found in the family" });
                    continue;
                }

                _ = doc.SetValue(targetParam, sourceParam, p.MappingStrategy);
                logs.Add(new LogEntry { Item = mappingDesc });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = mappingDesc, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}

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