using PeExtensions.FamDocument.SetValue;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class MapParamsSettings : IOperationSettings {
    [Description("List of parameter remapping rules")]
    [Required]
    public IEnumerable<MappingData> MappingData { get; init; } = [];

    public bool Enabled { get; init; } = true;
}

public class MappingData {
    [Description("Current parameter names to map from (ordered by priority)")]
    [Required]
    public List<string> CurrNames { get; set; } = [];

    [Description("New parameter name to map to")]
    [Required]
    public string NewName { get; init; }

    [Description(
        "Coercion strategy to use for the remapping. CoerceByStorageType will be used when none is specified.")]
    public ParamCoercionStrategy MappingStrategy { get; init; } = ParamCoercionStrategy.CoerceByStorageType;

    /// <summary>
    ///     Whether to skip the param in the operation. Use this to dictate upfront which params to skip.
    ///     And/or to specify that a parameter has been sufficiently processed by a previous operation/operation iteration.
    /// </summary>
    public bool IsProcessed { get; set; } = false;
}