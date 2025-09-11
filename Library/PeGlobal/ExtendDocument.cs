namespace Nice3point.Revit.Extensions;

public static class ExtendDocument {
    public static SharedParameterElement? FindParameter(this Document famDoc, ForgeTypeId parameterTypeId) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");
        var typeIdParts = parameterTypeId.TypeId?.Split(':');
        if (typeIdParts == null || typeIdParts.Length < 2) throw new ArgumentException("Invalid parameterTypeId");

        var parameterPart = typeIdParts[1];
        var dashIndex = parameterPart.IndexOf('-');
        var guidText = dashIndex > 0 ? parameterPart[..dashIndex] : parameterPart;

        return !Guid.TryParse(guidText, out var guid)
            ? throw new ArgumentException("Invalid parameterTypeId")
            : new FilteredElementCollector(famDoc)
                .OfClass(typeof(SharedParameterElement))
                .OfType<SharedParameterElement>()
                .First(p => p.GuidValue == guid);
    }
}