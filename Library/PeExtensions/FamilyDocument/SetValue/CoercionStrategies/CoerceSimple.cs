#nullable enable
namespace PeExtensions.FamDocument.SetValue.CoercionStrategies;

/// <summary>
///     Coerced mapping strategy - performs reasonable type coercions between compatible storage types.
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
public class CoerceSimple : ICoercionStrategy {
    public bool CanMap(CoercionContext context) {
        // Can always map if storage types match
        if (context.SourceStorageType == context.TargetStorageType)
            return true;

        // Check if coercion is possible
        return (context.SourceValue, context.TargetStorageType) switch {
            (bool, StorageType.Integer) => true,
            (int, StorageType.Double) => true,
            (string str, StorageType.Double) => double.TryParse(str, out _),
            (string str, StorageType.Integer) => int.TryParse(str, out _),
            _ => false
        };
    }

    public Result<FamilyParameter> Map(CoercionContext context) {
        var fm = context.FamilyManager;
        var target = context.TargetParam;

        switch (context.SourceValue) {
        case bool boolValue when context.TargetStorageType == StorageType.Integer:
            fm.Set(target, boolValue ? 1 : 0);
            return target;
        case double doubleValue:
            fm.Set(target, doubleValue);
            return target;
        case int intValue:
            if (context.TargetStorageType == StorageType.Double)
                fm.Set(target, (double)intValue);
            else
                fm.Set(target, intValue);
            return target;
        case string stringValue:
            if (context.TargetStorageType == StorageType.Double)
                fm.Set(target, double.Parse(stringValue));
            else if (context.TargetStorageType == StorageType.Integer)
                fm.Set(target, int.Parse(stringValue));
            else
                fm.Set(target, stringValue);
            return target;
        case ElementId elementIdValue:
            fm.Set(target, elementIdValue);
            return target;
        default:
            return new ArgumentException($"Invalid type of value to set ({context.SourceValue.GetType().Name})");
        }
    }
}