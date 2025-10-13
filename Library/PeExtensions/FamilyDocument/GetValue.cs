namespace PeExtensions.FamDocument;

public static class FamilyManagerGetValue {
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
    public static object GetValue(this Document famDoc, FamilyParameter familyParameter) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");
        var familyManager = famDoc.FamilyManager;
        if (familyParameter is null) throw new ArgumentNullException(nameof(familyParameter));

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
}