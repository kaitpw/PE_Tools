namespace PeRevit.Families;

public static class MutateParam {

    /// <summary>
    ///     Take the value from one parameter and set it to another. returns the new parameter 
    ///     Throws if an arg is null, or if the param get set fails
    /// </summary>
    /// <param name="famDoc"></param>
    /// <param name="oldParam"></param>
    /// <param name="newParam"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static FamilyParameter Remap(
       Document famDoc,
       FamilyParameter oldParam,
       FamilyParameter newParam,
       object defaultValue = null
    ) {
        ArgumentNullException.ThrowIfNull(famDoc);
        ArgumentNullException.ThrowIfNull(oldParam);
        ArgumentNullException.ThrowIfNull(newParam);
        if (!famDoc.IsFamilyDocument)
            throw new Exception("Family document is null or not a family document");

        var fm = famDoc.FamilyManager;
        foreach (FamilyType famType in fm.Types) {
            var currentValue = oldParam.GetValue(famType);
            if (currentValue != null)
                _ = newParam.SetValueCoerced(fm, famType, currentValue);
            else if (defaultValue != null)
                _ = newParam.SetValueCoerced(fm, famType, defaultValue);
        }
        return newParam;
    }

    /// <summary>
    ///     Take the value from one parameter and set it to another. 
    /// </summary>
    /// <param name="famDoc"></param>
    /// <param name="oldParam"></param>
    /// <param name="newParam"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static FamilyParameter RemapCoalesce(
       Document famDoc,
       FamilyParameter oldParam,
       FamilyParameter newParam,
       object defaultValue
    ) {
        ArgumentNullException.ThrowIfNull(famDoc);
        ArgumentNullException.ThrowIfNull(oldParam);
        ArgumentNullException.ThrowIfNull(newParam);
        if (!famDoc.IsFamilyDocument)
            throw new Exception("Family document is null or not a family document");

        var fm = famDoc.FamilyManager;
        foreach (FamilyType famType in fm.Types) {
            var currentValue = oldParam.GetValue(famType);
            _ = currentValue is not null and not ""
                ? newParam.SetValue(fm, famType, currentValue)
                : newParam.SetValue(fm, famType, defaultValue);
        }
        return newParam;
    }
}