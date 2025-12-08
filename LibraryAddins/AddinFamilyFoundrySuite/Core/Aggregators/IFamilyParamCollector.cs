namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Strategy interface for collecting parameter data from families.
///     Different implementations can use different approaches (temp instance, edit family, etc.)
/// </summary>
public interface IFamilyParamCollector
{
    /// <summary>
    ///     Collects parameter metadata from a family.
    /// </summary>
    /// <param name="doc">The Revit document containing the family</param>
    /// <param name="family">The family to collect parameters from</param>
    /// <returns>List of parameter collection results, or empty list if collection fails</returns>
    List<ParamCollectionResult> CollectParams(Document doc, Family family);
}

/// <summary>
///     Result of collecting a single parameter's metadata from a family.
/// </summary>
/// <param name="ParamName">The parameter's display name</param>
/// <param name="DataType">The ForgeTypeId representing the parameter's data type</param>
/// <param name="IsInstance">True if instance parameter, false if type parameter</param>
/// <param name="StorageType">The internal storage type (Double, Integer, String, ElementId)</param>
/// <param name="IsBuiltIn">True if this is a built-in Revit parameter</param>
/// <param name="SharedGuid">The GUID if this is a shared parameter, null otherwise</param>
public record ParamCollectionResult(
    string ParamName,
    ForgeTypeId DataType,
    bool IsInstance,
    StorageType StorageType,
    bool IsBuiltIn,
    Guid? SharedGuid
);
