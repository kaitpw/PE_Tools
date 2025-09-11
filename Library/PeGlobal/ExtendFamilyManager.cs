namespace Nice3point.Revit.Extensions;

/// <summary>
///     Revit FamilyManager Extensions
/// </summary>
public static class ExtendFamilyManager {
    /// <summary>
    ///     Find a parameter by ForgeTypeId identifier
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="parameter">Identifier of the built-in parameter</param>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     ForgeTypeId does not identify a built-in parameter.
    /// </exception>
    public static FamilyParameter? FindParameter(this FamilyManager familyManager, ForgeTypeId parameter) =>
        familyManager.GetParameter(parameter);

    /// <summary>
    ///     Find a parameter by built-in parameter identifier
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="parameter">The built-in parameter ID</param>
    public static FamilyParameter? FindParameter(this FamilyManager familyManager, BuiltInParameter parameter) =>
        familyManager.get_Parameter(parameter);

    /// <summary>
    ///     Find a parameter by definition
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="definition">The internal or external definition of the parameter</param>
    public static FamilyParameter? FindParameter(this FamilyManager familyManager, Definition definition) =>
        familyManager.get_Parameter(definition);

    /// <summary>
    ///     Find a shared parameter by GUID
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="guid">The unique id associated with the shared parameter</param>
    public static FamilyParameter? FindParameter(this FamilyManager familyManager, Guid guid) =>
        familyManager.get_Parameter(guid);

    /// <summary>
    ///     Find a parameter by name
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="name">The name of the parameter to be found</param>
    public static FamilyParameter? FindParameter(this FamilyManager familyManager, string name) =>
        familyManager.get_Parameter(name);

    /// <summary>
    ///     Set a <c>FamilyManager</c> parameter value
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="familyParameter">The parameter to be set</param>
    /// <param name="value">The value to be set</param>
    /// <exception cref="T:System.ArgumentException">Invalid value type</exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentNullException">
    ///     Thrown when the input argument-"familyParameter"-is <see langword="null" />.
    /// </exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     Thrown when the input argument-"familyParameter"-is an invalid family parameter.
    ///     --or-- When the storage type of family parameter is not ElementId
    ///     --or-- The input ElementId does not represent either a valid element in the document or InvalidElementId.
    /// </exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentOutOfRangeException">
    ///     Thrown when the input argument-"familyParameter"-is out of range.
    ///     --or-- Thrown when the input ElementId is not valid as a value for this FamilyParameter.
    /// </exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.InvalidOperationException">
    ///     Thrown when the family parameter is determined by formula,
    ///     or the current family type is invalid.
    /// </exception>
    public static void Set(this FamilyManager familyManager, FamilyParameter familyParameter, object value) {
        if (value != null) {
            switch (value) {
            case double doubleValue:
                familyManager.Set(familyParameter, doubleValue);
                break;
            case int intValue:
                familyManager.Set(familyParameter, intValue);
                break;
            case string stringValue:
                familyManager.Set(familyParameter, stringValue);
                break;
            case ElementId elementIdValue: // TODO: check if this works
                familyManager.Set(familyParameter, elementIdValue);
                break;
            default:
                throw new ArgumentException($"Invalid value type: {value.GetType().Name}");
            }
        }
    }
}