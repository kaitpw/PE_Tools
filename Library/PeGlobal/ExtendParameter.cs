public static class ExtendParameter {
    /// <summary>
    ///     Get a parameter value using the current family type. Returns null if the familyParameter is null.
    /// </summary>
    /// <remarks>
    ///     Only use this when the type-safety of the parameter value is unimportant (logging).
    ///     Use code like below in all other cases
    ///     <code>
    /// familyParameter.StorageType switch {
    ///     StorageType.Double => famType.AsDouble(familyParameter),
    ///     StorageType.String => famType.AsString(familyParameter),
    ///     StorageType.Integer => famType.AsInteger(familyParameter),
    ///     StorageType.ElementId => famType.AsElementId(familyParameter),
    ///     _ => null
    /// </code>
    /// </remarks>
    /// <param name="famType">The family type</param>
    /// <param name="familyParameter">The parameter to get the value from</param>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     Thrown if the input argument-"familyParameter"-is invalid,
    /// </exception>
    public static object GetValue(this FamilyParameter familyParameter, FamilyType famType) {
        ArgumentNullException.ThrowIfNull(famType);
        ArgumentNullException.ThrowIfNull(familyParameter);
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
    ///     Set a parameter value
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType 
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    /// <param name="familyParameter">The parameter to set</param>
    /// <param name="familyManager">The family manager</param>
    /// <param name="value">The value to set</param>
    /// <returns>The value that was set (not the parameter that was set)</returns>
    public static object SetValue(this FamilyParameter familyParameter, FamilyManager familyManager, object value) {
        ArgumentNullException.ThrowIfNull(familyManager);
        ArgumentNullException.ThrowIfNull(value);

        switch (value) {
        case double doubleValue:
            familyManager.Set(familyParameter, doubleValue);
            return doubleValue;
        case int intValue:
            familyManager.Set(familyParameter, intValue);
            return intValue;
        case string stringValue:
            familyManager.Set(familyParameter, stringValue);
            return stringValue;
        case ElementId elementIdValue:
            familyManager.Set(familyParameter, elementIdValue);
            return elementIdValue;
        default:
            throw new ArgumentException($"Invalid value type: {value.GetType().Name}");
        }
    }

    /// <summary>
    ///     Set a parameter value and coerce the value to the correct type if it is reasonably coercable.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType 
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    /// <param name="familyParameter">The parameter to set</param>
    /// <param name="familyManager">The family manager</param>
    /// <param name="value">The value to set</param>
    /// <returns>The value that was set (not the parameter that was set)</returns>
    public static object SetValueCoerced(this FamilyParameter familyParameter, FamilyManager familyManager, object value) {
        ArgumentNullException.ThrowIfNull(familyManager);
        ArgumentNullException.ThrowIfNull(value);

        switch (value) {
        case bool boolValue when familyParameter.StorageType == StorageType.Integer:
            familyManager.Set(familyParameter, boolValue ? 1 : 0);
            return boolValue;
        case double doubleValue:
            familyManager.Set(familyParameter, doubleValue);
            return doubleValue;
        case int intValue:
            if (familyParameter.StorageType == StorageType.Double)
                familyManager.Set(familyParameter, (double)intValue);
            else
                familyManager.Set(familyParameter, intValue);
            return intValue;
        case string stringValue:
            if (familyParameter.StorageType == StorageType.Double)
                familyManager.Set(familyParameter, double.Parse(stringValue));
            else if (familyParameter.StorageType == StorageType.Integer)
                familyManager.Set(familyParameter, int.Parse(stringValue));
            else
                familyManager.Set(familyParameter, stringValue);
            return stringValue;
        case ElementId elementIdValue:
            familyManager.Set(familyParameter, elementIdValue);
            return elementIdValue;
        default:
            throw new ArgumentException($"Invalid value type: {value.GetType().Name}");
        }
    }
}
