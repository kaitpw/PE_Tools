namespace PE_Lib;

public class Levels
{
    /// <summary>
    ///     Retrieves the associated Level of the active View in Revit.
    /// </summary>
    /// <param name="view">The View object for which to find the associated Level.</param>
    /// <returns>The Level object associated with the view, or null if no level is associated or found.</returns>
    public static Level LevelOfActiveView(View view)
    {
        var doc = view.Document;
        var levelId = view.GenLevel.Id;

        if (levelId != ElementId.InvalidElementId && levelId != null)
            return doc.GetElement(levelId) as Level;
        return null;
    }

    /// <summary>
    ///     Get the level of an MEPCurve. First attempt is curve.ReverenceLevel, second is curve's level param.
    /// </summary>
    public static Result<Level> LevelOfMepCurve(MEPCurve mepCurve)
    {
        try
        {
            // Try getting level from the duct's reference level
            var levelId = mepCurve.ReferenceLevel?.Id;
            if (levelId != null && levelId != ElementId.InvalidElementId)
                return mepCurve.Document.GetElement(levelId) as Level;

            // Fallback: use the duct's level parameter
            var levelParam = mepCurve.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            if (levelParam != null && levelParam.AsElementId() != ElementId.InvalidElementId)
                return mepCurve.Document.GetElement(levelParam.AsElementId()) as Level;

            return new InvalidOperationException($"No level could be found for the MEPCurve {mepCurve.Id}");
        } catch (Exception e)
        {
            return e;
        }
    }
}