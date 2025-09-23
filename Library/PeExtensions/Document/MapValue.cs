using System.Globalization;
using System.Text.RegularExpressions;
using AddinFamilyFoundrySuite.Core.MapValue;

namespace PeExtensions;

public static class FamilyManagerMapValue
{
    /// <summary>
    ///     Maps a value from source parameter to target parameter using the specified policy.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    public static Result<FamilyParameter> MapValue(this Document doc, FamilyParameter sourceParam, FamilyParameter targetParam, string policy = null)
    {
        var strategy = MappingPolicyRegistry.GetStrategy(policy, doc, sourceParam, targetParam);

        if (!strategy.CanMap())
        {
            var sourceName = sourceParam?.Definition.Name ?? "Unknown";
            var targetName = targetParam?.Definition.Name ?? "Unknown";
            var sourceDataType = sourceParam?.Definition.GetDataType();
            var targetDataType = targetParam?.Definition.GetDataType();
            return new Exception($"Cannot map value from {sourceName} ({sourceDataType}) to {targetName} ({targetDataType}) using policy '{policy ?? "default"}'");
        }

        return strategy.Map();
    }

    /// <summary>
    ///     Maps a source value to target parameter using the specified policy.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    public static Result<FamilyParameter> MapValue(this Document doc, object sourceValue, FamilyParameter targetParam, string policy = null)
    {
        var strategy = MappingPolicyRegistry.GetStrategy(policy, doc, sourceValue, targetParam);

        if (!strategy.CanMap())
        {
            var targetName = targetParam?.Definition.Name ?? "Unknown";
            var targetDataType = targetParam?.Definition.GetDataType();
            return new Exception($"Cannot map value '{sourceValue}' to {targetName} ({targetDataType}) using policy '{policy ?? "default"}'");
        }

        return strategy.Map();
    }
}