#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PeExtensions.FamDocument.SetValue;

/// <summary>
///     Coercion strategies for mapping from source parameter to target parameter.
///     These strategies have access to both the source parameter's value and metadata.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ParamCoercionStrategy {
    /// <summary>
    ///     Strict coercion - only allows mapping when source and target storage types match exactly.
    ///     This is the safest strategy.
    /// </summary>
    Strict,

    /// <summary>
    ///     Storage type coercion - handles cases where storage types differ but data types are compatible.
    ///     Implements comprehensive storage type conversions based on Revit's parameter system.
    /// </summary>
    CoerceByStorageType,

    /// <summary>
    ///     Electrical coercion - converts numeric/string values to electrical parameters with unit conversion
    ///     and special handling for voltage values.
    /// </summary>
    CoerceElectrical
}

/// <summary>
///     Coercion strategies for mapping from a direct value to target parameter.
///     These strategies only have access to the value itself, not parameter metadata.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ValueCoercionStrategy {
    /// <summary>
    ///     Strict coercion - only allows mapping when value type and target storage type match exactly.
    ///     This is the safest strategy.
    /// </summary>
    Strict,

    /// <summary>
    ///     Coerced mapping - performs reasonable type coercions between compatible storage types
    ///     (e.g., bool to int, int to double, string parsing).
    /// </summary>
    CoerceSimple
}