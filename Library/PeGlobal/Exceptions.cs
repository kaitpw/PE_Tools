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

    /// <summary>Creates a JsonValidationException with a formatted list of validation errors</summary>
    /// <param name="validationErrors">List of validation error messages</param>
    public JsonValidationException(string path,IEnumerable<string> validationErrors)
        : base(FormatValidationErrors(path, validationErrors)) {
    }

    private static string FormatValidationErrors(string path,IEnumerable<string> errors) {
        var errorList = errors.ToList();
        return $"JSON validation failed at {path} with {errorList.Count} error{(errorList.Count != 1 ? "s" : "")}:\n" +
               string.Join("\n", errorList.Select((error, index) => $"  {index + 1}. {error}"));
    }
}

public class CrashProgramException : Exception {
    private static readonly string _prefix = "The program was intentionally crashed because";
    public CrashProgramException(string message) : base(_prefix + FormatMessage(message)) { }

    public CrashProgramException(Exception exception) : base(_prefix + " an unrecoverable error occurred:" +
                                                             FormatError(exception)) {
    }

    private static string FormatMessage(string message) =>
        message.Trim().Length > 0
            ? " " + char.ToLower(message[0]) + message[1..]
            : " " + message.Trim();

    private static string FormatError(Exception exception) =>
        $"\n\n{exception.Message}\n{exception.StackTrace}";
}