namespace PeServices.Storage.Core.Json;

/// <summary>
///     Exception thrown when JSON profile inheritance resolution fails.
/// </summary>
public class JsonExtendsException : Exception {
    public JsonExtendsException(string message) : base(message) { }
    public JsonExtendsException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>The file path of the child profile being loaded.</summary>
    public string ChildFilePath { get; init; }

    /// <summary>The file path of the base profile (if applicable).</summary>
    public string? BaseFilePath { get; init; }

    /// <summary>The inheritance chain that was resolved (for debugging).</summary>
    public List<string> InheritanceChain { get; init; } = [];

    /// <summary>Creates an exception for when the base profile file is not found.</summary>
    public static JsonExtendsException BaseNotFound(
        string childPath,
        string extendsName,
        string expectedBasePath
    ) => new($"""
        Profile '{Path.GetFileName(childPath)}' extends '{extendsName}', but base profile was not found.
          Expected: {expectedBasePath}
          Hint: Ensure the base profile exists and the name is spelled correctly (case-sensitive).
        """) {
        ChildFilePath = childPath,
        BaseFilePath = expectedBasePath
    };

    /// <summary>Creates an exception for circular inheritance.</summary>
    public static JsonExtendsException CircularInheritance(
        string childPath,
        List<string> inheritanceChain
    ) => new($"""
        Circular inheritance detected in profile chain:
          {string.Join(" → ", inheritanceChain)}
          
        Profile inheritance must form a tree, not a cycle.
        """) {
        ChildFilePath = childPath,
        InheritanceChain = inheritanceChain
    };

    /// <summary>Creates an exception for invalid $extends value.</summary>
    public static JsonExtendsException InvalidExtendsValue(
        string childPath,
        string foundType
    ) => new($"""
        Invalid '$extends' value in '{Path.GetFileName(childPath)}'.
          Expected: a non-empty string (e.g., "MechEquip")
          Found: {foundType}
        """) {
        ChildFilePath = childPath
    };

    /// <summary>Creates an exception when base profile validation fails.</summary>
    public static JsonExtendsException BaseValidationFailed(
        string childPath,
        string basePath,
        Exception innerException
    ) => new($"""
        Base profile '{Path.GetFileName(basePath)}' failed validation (required by '{Path.GetFileName(childPath)}'):
          {innerException.Message}
          
        Fix the base profile before loading profiles that extend it.
        """, innerException) {
        ChildFilePath = childPath,
        BaseFilePath = basePath
    };

    /// <summary>Creates an exception when merged result validation fails.</summary>
    public static JsonExtendsException MergedValidationFailed(
        string childPath,
        string basePath,
        string validationErrors
    ) => new($"""
        Profile '{Path.GetFileName(childPath)}' (extending '{Path.GetFileName(basePath)}') failed schema validation:
          {validationErrors}
          
        This error is likely in the child profile's override. Check '{Path.GetFileName(childPath)}'.
        """) {
        ChildFilePath = childPath,
        BaseFilePath = basePath
    };
}
