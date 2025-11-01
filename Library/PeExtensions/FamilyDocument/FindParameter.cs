namespace PeExtensions.FamDocument;

public static class FamilyDocumentFindParameter {
    /// <summary>
    ///     Find a shared parameter by ParameterTypeId identifier recieved from Parameters Service
    /// </summary>
    /// <param name="famDoc">The family document</param>
    /// <param name="parameterTypeId">The ForgeTypeId identifier of the parameter</param>
    /// <returns>The shared parameter element, or null if the parameter is not found</returns>
    public static SharedParameterElement? FindParameter(this FamilyDocument famDoc, ForgeTypeId parameterTypeId) {
        var typeId = parameterTypeId.TypeId;
        var typeIdParts = typeId?.Split(':');
        if (typeIdParts == null || typeIdParts.Length < 2)
            throw new ArgumentException($"ParameterTypeId is not of the Parameters Service format: {typeId}");

        var parameterPart = typeIdParts[1];
        var dashIndex = parameterPart.IndexOf('-');
        var guidText = dashIndex > 0 ? parameterPart[..dashIndex] : parameterPart;

        return !Guid.TryParse(guidText, out var guid)
            ? throw new ArgumentException($"Could not extract GUID from parameterTypeId: {typeId}")
            : new FilteredElementCollector(famDoc)
                .OfClass(typeof(SharedParameterElement))
                .OfType<SharedParameterElement>()
                .First(p => p.GuidValue == guid);
    }
}