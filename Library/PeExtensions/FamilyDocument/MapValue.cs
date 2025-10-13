using AddinFamilyFoundrySuite.Core.MapValue;

namespace PeExtensions;

public static class FamilyManagerMapValue {
    /// <summary>
    ///     Maps a value from source parameter to target parameter using the specified policy.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    public static Result<FamilyParameter> MapValue(this Document famDoc,
        string sourceName,
        string targetName,
        string policy = null
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");

        var sourceParam = famDoc.FamilyManager.FindParameter(sourceName);
        var targetParam = famDoc.FamilyManager.FindParameter(targetName);
        if (sourceParam == null) return new Exception($"Source parameter {sourceName} not found");
        if (targetParam == null) return new Exception($"Target parameter {targetName} not found");
        var strategy = MappingPolicyRegistry.GetStrategy(policy, famDoc, sourceParam, targetParam);

        if (!strategy.CanMap()) {
            var sourceDataType = sourceParam?.Definition.GetDataType();
            var targetDataType = targetParam?.Definition.GetDataType();
            return new Exception(
                $"Cannot map value from {sourceName} ({sourceDataType}) to {targetName} ({targetDataType}) using policy '{policy ?? "default"}'");
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
    public static Result<FamilyParameter> MapValue(this Document famDoc,
        object sourceValue,
        string targetName,
        string policy = null) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");

        var targetParam = famDoc.FamilyManager.FindParameter(targetName);
        if (targetParam == null) return new Exception($"Target parameter {targetName} not found");
        var strategy = MappingPolicyRegistry.GetStrategy(policy, famDoc, sourceValue, targetParam);

        if (!strategy.CanMap()) {
            var targetDataType = targetParam?.Definition.GetDataType();
            return new Exception(
                $"Cannot map value '{sourceValue}' to {targetName} ({targetDataType}) using policy '{policy ?? "default"}'");
        }

        return strategy.Map();
    }
}