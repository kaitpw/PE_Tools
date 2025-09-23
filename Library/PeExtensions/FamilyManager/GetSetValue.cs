namespace PeExtensions;

public static class FamilyManagerGetSetValue {
    /// <summary>
    ///     Get a parameter value using the current family type. Returns null if the familyParameter is null.
    /// </summary>
    /// <remarks>
    ///     Only use this when the type-safety of the parameter value is unimportant, like logging,
    ///     or for example when used in conjunction with the SetValue... extension methods.
    /// </remarks>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     Thrown if the input argument-"familyParameter"-is invalid,
    /// </exception>
    public static object GetValue(this FamilyManager familyManager, FamilyParameter familyParameter) {
        ArgumentNullException.ThrowIfNull(familyManager);
        ArgumentNullException.ThrowIfNull(familyParameter);
        var famType = familyManager.CurrentType;
        if (!famType.HasValue(familyParameter)) return null;

        return familyParameter.StorageType switch {
            StorageType.Double => famType.AsDouble(familyParameter),
            StorageType.String => famType.AsString(familyParameter),
            StorageType.Integer => famType.AsInteger(familyParameter),
            StorageType.ElementId => famType.AsElementId(familyParameter),
            _ => null
        };
    }

    /// <summary>
    ///     Set a <c>FamilyManager</c> parameter value on the <c>FamilyManager.CurrentType</c>
    ///     if the <c>value</c> is the same as the <c>targetParam</c> storage type.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">Invalid value type</exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     Thrown when the input argument-"targetParam"-is an invalid family parameter.
    ///     --or-- When the storage type of family parameter is not ElementId
    ///     --or-- The input ElementId does not represent either a valid element in the document or InvalidElementId.
    /// </exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentOutOfRangeException">
    ///     Thrown when the input argument-"targetParam"-is out of range.
    ///     --or-- Thrown when the input ElementId is not valid as a value for this FamilyParameter.
    /// </exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.InvalidOperationException">
    ///     Thrown when the family parameter is determined by formula,
    ///     or the current family type is invalid.
    /// </exception>
    public static FamilyParameter SetValueStrict(this FamilyManager familyManager,
        FamilyParameter targetParam,
        object value) {
        ArgumentNullException.ThrowIfNull(familyManager);
        ArgumentNullException.ThrowIfNull(targetParam);
        ArgumentNullException.ThrowIfNull(value);

        switch (value) {
        case double doubleValue:
            familyManager.Set(targetParam, doubleValue);
            return targetParam;
        case int intValue:
            familyManager.Set(targetParam, intValue);
            return targetParam;
        case string stringValue:
            familyManager.Set(targetParam, stringValue);
            return targetParam;
        case ElementId elementIdValue:
            familyManager.Set(targetParam, elementIdValue);
            return targetParam;
        default:
            throw new ArgumentException($"Invalid type of value to set ({value.GetType().Name})");
        }
    }

    /// <summary>
    ///     Set a <c>FamilyManager</c> parameter value on the <c>FamilyManager.CurrentType</c>
    ///     if the <c>value</c> is reasonably coercible to the <c>targetParam</c> storage type.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">Invalid value type</exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     Thrown when the input argument-"targetParam"-is an invalid family parameter.
    ///     --or-- When the storage type of family parameter is not ElementId
    ///     --or-- The input ElementId does not represent either a valid element in the document or InvalidElementId.
    /// </exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentOutOfRangeException">
    ///     Thrown when the input argument-"targetParam"-is out of range.
    ///     --or-- Thrown when the input ElementId is not valid as a value for this FamilyParameter.
    /// </exception>
    /// <exception cref="T:Autodesk.Revit.Exceptions.InvalidOperationException">
    ///     Thrown when the family parameter is determined by formula,
    ///     or the current family type is invalid.
    /// </exception>
    public static object SetValueCoerced(this FamilyManager familyManager, FamilyParameter targetParam, object value) {
        ArgumentNullException.ThrowIfNull(familyManager);
        ArgumentNullException.ThrowIfNull(targetParam);
        ArgumentNullException.ThrowIfNull(value);

        switch (value) {
        case bool boolValue when targetParam.StorageType == StorageType.Integer:
            familyManager.Set(targetParam, boolValue ? 1 : 0);
            return boolValue;
        case double doubleValue:
            familyManager.Set(targetParam, doubleValue);
            return doubleValue;
        case int intValue:
            if (targetParam.StorageType == StorageType.Double)
                familyManager.Set(targetParam, (double)intValue);
            else
                familyManager.Set(targetParam, intValue);
            return intValue;
        case string stringValue:
            if (targetParam.StorageType == StorageType.Double)
                familyManager.Set(targetParam, double.Parse(stringValue));
            else if (targetParam.StorageType == StorageType.Integer)
                familyManager.Set(targetParam, int.Parse(stringValue));
            else
                familyManager.Set(targetParam, stringValue);
            return stringValue;
        case ElementId elementIdValue:
            familyManager.Set(targetParam, elementIdValue);
            return elementIdValue;
        default:
            throw new ArgumentException($"Invalid type of value to set ({value.GetType().Name})");
        }
    }
}