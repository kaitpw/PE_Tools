/// <summary>
///     Exception thrown when an element has intersections with other elements
///     that prevent an operation from completing successfully.
/// </summary>
public class ElementIntersectException : Exception {
    /// <summary>
    ///     Creates a new instance of IntersectingElementsException
    /// </summary>
    /// <param name="reference">The ID of the reference element</param>
    /// <param name="intersections">The IDs of intersecting elements</param>
    public ElementIntersectException(ElementId reference, ElementId[] intersections)
        : base(FormatDefaultMessage(reference, intersections)) {
        this.ReferenceElement = reference;
        this.IntersectionElements = intersections;
    }

    public ElementId? ReferenceElement { get; }
    public ElementId[] IntersectionElements { get; }

    private static string FormatDefaultMessage(ElementId reference, ElementId[] intersections) =>
        $"Element {reference} has {intersections.Length} intersection{(intersections.Length != 1 ? "s" : "")}";
}

public class JsonValidationException : Exception {
    public JsonValidationException(string message) : base(message) { }
    public JsonValidationException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    ///     Creates a JsonValidationException with formatted validation errors
    /// </summary>
    /// <param name="validationErrors">List of validation error messages</param>
    public JsonValidationException(IEnumerable<string> validationErrors)
        : base(FormatValidationErrors(validationErrors)) {
    }

    private static string FormatValidationErrors(IEnumerable<string> errors) {
        var errorList = errors.ToList();
        return $"JSON validation failed with {errorList.Count} error{(errorList.Count != 1 ? "s" : "")}:\n" +
               string.Join("\n", errorList.Select((error, index) => $"  {index + 1}. {error}"));
    }
}