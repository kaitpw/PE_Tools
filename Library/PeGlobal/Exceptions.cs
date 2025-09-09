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
    public JsonValidationException(string path, IEnumerable<string> validationErrors)
        : base(FormatValidationErrors(path, validationErrors)) {
    }

    private static string FormatValidationErrors(string path, IEnumerable<string> errors) {
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


/// <summary>
///     Extension methods for Exception class
/// </summary>
public static class ExceptionExtensions {
    /// <summary>
    ///     Checks if an exception originated from a specific method.
    /// </summary>
    /// <example>
    ///     <code>
    /// catch (Exception ex) {
    /// if (ex.IsExceptionFromMethod(nameof(ParameterUtils.DownloadParameterOptions))) {
    ///     switch (ex.Message) {
    ///     case { } msg when msg.Contains("Some text 1."): // do something
    ///     case { } msg when msg.Contains("Some text 2."): // do something else
    ///     default: // do fallback default thing
    ///     }
    /// } 
    ///     </code>
    /// </example>
    /// <param name="exception">The exception this method is being called from</param>
    /// <param name="methodName">The method name</param>
    /// <param name="parameterTypes">Optional parameter types to match specific overloads</param>
    /// <returns>True if the exception originated from the specified method</returns>
    public static bool IsExceptionFromMethod(
        this Exception exception,
        string methodName,
        params Type[] parameterTypes
    ) {
        // TargetSite is the method the exception was thrown from
        var targetSite = exception.TargetSite;
        if (targetSite != null && targetSite.Name != methodName) return false;

        // If no parameter types specified, just match method name
        if (parameterTypes == null || parameterTypes.Length == 0) return true;

        // Check parameter types for specific overload matching
        if (targetSite == null) return true;
        var methodParams = targetSite.GetParameters();
        return methodParams.Length == parameterTypes.Length &&
               !parameterTypes.Where((t, i) => methodParams[i].ParameterType != t).Any();
    }
}