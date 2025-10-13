using PeExtensions.FamDocument.SetValue;

namespace PeExtensions.FamDocument;

public static class FamilyDocumentSetValue {
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
    public static FamilyParameter SetValueStrict(this Document famDoc,
        FamilyParameter targetParam,
        object value
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");
        if (targetParam is null) throw new ArgumentNullException(nameof(targetParam));
        if (value is null) throw new ArgumentNullException(nameof(value));

        var familyManager = famDoc.FamilyManager;

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
    ///     Set a family's parameter value on the <c>FamilyManager.CurrentType</c> using the specified strategy.
    ///     If no strategy is specified, the value will be set using the <c>SetValueStrict</c> method.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    public static FamilyParameter SetValue(this Document famDoc,
        FamilyParameter targetParam,
        FamilyParameter sourceParam,
        string strategyName = null
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");
        if (strategyName is null) return SetValueStrict(famDoc, targetParam, famDoc.GetValue(sourceParam));
        var strategy = ParamCoercionStrategyRegistry.GetStrategy(strategyName, famDoc, sourceParam, targetParam);
        if (!strategy.CanMap()) {
            var targetDataType = targetParam?.Definition.GetDataType();
            throw new Exception(
                $"Cannot map '{sourceParam.Definition.Name}' to '{targetParam.Definition.Name}' ({targetDataType}) using policy '{strategyName ?? "default"}'");
        }

        var (param, err) = strategy.Map();
        if (err is not null) throw err;
        return param;
    }

    // add support for value mapping strategies later
    // /// <summary>
    // ///     Set a family's parameter value on the <c>FamilyManager.CurrentType</c> using the specified strategy.
    // ///     If no strategy is specified, the value will be set using the <c>SetValueStrict</c> method.
    // /// </summary>
    // /// <remarks>
    // ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType
    // ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    // ///     in your loop/s.
    // /// </remarks>
    // public static FamilyParameter SetValue(this Document famDoc,
    //     FamilyParameter targetParam,
    //     object sourceValue,
    //     string strategyName = null
    // ) {
    //     if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");
    //     if (strategyName is null) return SetValueStrict(famDoc, targetParam, sourceValue);
    //     var strategy = ParamCoercionStrategyRegistry.GetStrategy(strategyName, famDoc, sourceValue, targetParam);
    //     if (!strategy.CanMap()) {
    //         var targetDataType = targetParam?.Definition.GetDataType();
    //         throw new Exception(
    //             $"Cannot map value '{sourceValue}' to {targetParam.Definition.Name} ({targetDataType}) using policy '{strategyName ?? "default"}'");
    //     }

    //     var (param, err) = strategy.Map();
    //     if (err is not null) throw err;
    //     return param;
    // }
}