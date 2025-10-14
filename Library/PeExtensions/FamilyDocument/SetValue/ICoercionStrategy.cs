#nullable enable
namespace PeExtensions.FamDocument.SetValue;

/// <summary>
///     Interface for parameter coercion strategies.
///     Each strategy decides if it can handle a coercion scenario and executes the mapping.
/// </summary>
public interface ICoercionStrategy {
    /// <summary>
    ///     Determines if this strategy can handle the given coercion scenario.
    /// </summary>
    bool CanMap(CoercionContext context);

    /// <summary>
    ///     Executes the coercion. Should only be called if CanMap returns true.
    /// </summary>
    Result<FamilyParameter> Map(CoercionContext context);
}