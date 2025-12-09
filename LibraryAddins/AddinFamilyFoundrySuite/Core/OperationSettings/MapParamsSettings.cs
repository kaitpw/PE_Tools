using PeExtensions.FamDocument.SetValue;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class MapParamsSettings : IOperationSettings {
    [Description("List of parameter remapping rules")]
    [Required]
    public IEnumerable<MappingData> MappingData { get; init; } = [];

    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Runtime wrapper for MapParamsSettings that provides mutable state tracking.
/// This class is NOT serialized - it's created at runtime from MapParamsSettings.
/// </summary>
public class RuntimeMapParamsSettings : IOperationSettings {
#pragma warning disable PE001 // Settings classes should not have fields
    private readonly IEnumerable<MappingData> _sourceMappings;
    private List<MappingData> _materializedMappings;
#pragma warning restore PE001

    public RuntimeMapParamsSettings() : this([]) { }

    public RuntimeMapParamsSettings(IEnumerable<MappingData> mappingData) {
        this._sourceMappings = mappingData;
        this.Reset();
    }

    public void Reset() =>
        this._materializedMappings = this._sourceMappings.Select(m => new MappingData {
            CurrName = m.CurrName,
            NewName = m.NewName,
            MappingStrategy = m.MappingStrategy,
            IsProcessed = false
        }).ToList();

    public IEnumerable<MappingData> UnProcessedMappingData =>
        this._materializedMappings.Where(m => !m.IsProcessed);

    public IEnumerable<MappingData> ProcessedMappingData =>
        this._materializedMappings.Where(m => m.IsProcessed);

    public void MarkNewNameAsProcessed(string newName) {
        foreach (var mapping in this._materializedMappings.Where(m => m.NewName == newName)) {
            mapping.IsProcessed = true;
        }
    }

    public bool Enabled { get; init; } = true;
}

public class MappingData {
    [Description("Current parameter name to map from")]
    [Required]
    public string CurrName { get; init; }

    [Description("New parameter name to map to")]
    [Required]
    public string NewName { get; init; }

    [Description(
        "Coercion strategy to use for the remapping. CoerceByStorageType will be used when none is specified.")]
    public ParamCoercionStrategy MappingStrategy { get; init; } = ParamCoercionStrategy.CoerceByStorageType;

    /// <summary>
    /// Whether to skip the param in the operation. Use this to dictate upfront which params to skip.
    /// And/or to specify that a parameter has been sufficiently processed by a previous operation/operation iteration.
    /// </summary>
    [JsonIgnore] public bool IsProcessed { get; set; } = false;

}