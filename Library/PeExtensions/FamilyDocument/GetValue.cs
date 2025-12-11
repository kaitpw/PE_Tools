using PeExtensions.FamManager;

namespace PeExtensions.FamDocument;

public static class FamilyManagerGetValue {
    /// <summary>
    ///     Get a parameter value using the current family type. Returns null if the familyParameter is null.
    /// </summary>
    /// <remarks>
    ///     Only use this when the type-safety of the parameter value is unimportant, like logging,
    ///     or for example when used in conjunction with the SetValue extension methods.
    /// </remarks>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     Thrown if the input argument-"familyParameter"-is invalid,
    /// </exception>
    public static object GetValue(this FamilyDocument famDoc, FamilyParameter familyParameter) {
        var famType = famDoc.FamilyManager.CurrentType;
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
    ///     Get a parameter value using the current family type. Returns null if the familyParameter is null.
    /// </summary>
    /// <remarks>
    ///     Only use this when the type-safety of the parameter value is unimportant, like logging,
    ///     or for example when used in conjunction with the SetValue extension methods.
    /// </remarks>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     Thrown if the input argument-"familyParameter"-is invalid,
    /// </exception>
    public static object GetValue(this FamilyDocument famDoc, string familyParameterName) {
        var fm = famDoc.FamilyManager;
        var famType = fm.CurrentType;
        var familyParameter = fm.FindParameter(familyParameterName);
        if (familyParameter == null || !famType.HasValue(familyParameter)) return null;

        return familyParameter.StorageType switch {
            StorageType.Double => famType.AsDouble(familyParameter),
            StorageType.String => famType.AsString(familyParameter),
            StorageType.Integer => famType.AsInteger(familyParameter),
            StorageType.ElementId => famType.AsElementId(familyParameter),
            _ => null
        };
    }

    /// <summary>
    ///     Get a double parameter value using the current family type.
    ///     Returns null if the parameter doesn't exist, has no value, or isn't a Double StorageType.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Values are returned in Revit's internal units:
    ///         <list type="bullet">
    ///             <item>Length: feet</item>
    ///             <item>Angle: radians</item>
    ///             <item>Voltage, Current, Power: base SI (Volts, Amps, Watts)</item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         For most electrical parameters (Voltage, Current, Power), internal units match
    ///         common display units, so 120V is stored as 120.0.
    ///     </para>
    /// </remarks>
    public static double? GetDouble(this FamilyDocument famDoc, string familyParameterName) {
        var fm = famDoc.FamilyManager;
        var familyParameter = fm.FindParameter(familyParameterName);
        return famDoc.GetDouble(familyParameter);
    }

    /// <summary>
    ///     Get a double parameter value using the current family type.
    ///     Returns null if the parameter is null, has no value, or isn't a Double StorageType.
    /// </summary>
    public static double? GetDouble(this FamilyDocument famDoc, FamilyParameter familyParameter) {
        if (familyParameter == null) return null;
        if (familyParameter.StorageType != StorageType.Double) return null;

        var famType = famDoc.FamilyManager.CurrentType;
        if (!famType.HasValue(familyParameter)) return null;

        return famType.AsDouble(familyParameter);
    }

    /// <summary>
    ///     Get an integer parameter value using the current family type.
    ///     Returns null if the parameter doesn't exist, has no value, or isn't an Integer StorageType.
    /// </summary>
    public static int? GetInt(this FamilyDocument famDoc, string familyParameterName) {
        var fm = famDoc.FamilyManager;
        var familyParameter = fm.FindParameter(familyParameterName);
        return famDoc.GetInt(familyParameter);
    }

    /// <summary>
    ///     Get an integer parameter value using the current family type.
    ///     Returns null if the parameter is null, has no value, or isn't an Integer StorageType.
    /// </summary>
    public static int? GetInt(this FamilyDocument famDoc, FamilyParameter familyParameter) {
        if (familyParameter == null) return null;
        if (familyParameter.StorageType != StorageType.Integer) return null;

        var famType = famDoc.FamilyManager.CurrentType;
        if (!famType.HasValue(familyParameter)) return null;

        return famType.AsInteger(familyParameter);
    }
}