using Newtonsoft.Json.Linq;

namespace PeServices.Storage.Core.Json;

/// <summary>
///     Utility for composing JSON arrays from reusable fragment files.
///     Processes <c>$include</c> directives within arrays, replacing them with fragment contents.
/// </summary>
/// <remarks>
///     <para>Usage in JSON:</para>
///     <code>
///     {
///       "Fields": [
///         { "$include": "_fragments/header-fields" },
///         { "ParameterName": "CustomField" },
///         { "$include": "_fragments/footer-fields" }
///       ]
///     }
///     </code>
///     <para>
///         The <c>$include</c> objects are replaced with the array contents from the referenced fragment file.
///         Fragment paths are relative to the base directory (typically the profile's directory).
///     </para>
/// </remarks>
public static class JsonArrayComposer {
    private const string IncludeProperty = "$include";

    /// <summary>
    ///     Recursively processes a JObject, expanding <c>$include</c> directives in all arrays.
    /// </summary>
    /// <param name="obj">The JObject to process (modified in place)</param>
    /// <param name="baseDirectory">Base directory for resolving relative fragment paths</param>
    public static void ExpandIncludes(JObject obj, string baseDirectory) =>
        ExpandIncludes(obj, baseDirectory, []);

    /// <summary>
    ///     Recursively processes a JObject, expanding <c>$include</c> directives in all arrays.
    ///     Tracks visited fragments to detect circular includes.
    /// </summary>
    private static void ExpandIncludes(JObject obj, string baseDirectory, HashSet<string> visitedFragments) {
        foreach (var prop in obj.Properties().ToList()) {
            switch (prop.Value) {
            case JArray array:
                obj[prop.Name] = ExpandArrayIncludes(array, baseDirectory, visitedFragments);
                break;
            case JObject childObj:
                ExpandIncludes(childObj, baseDirectory, visitedFragments);
                break;
            }
        }
    }

    /// <summary>
    ///     Expands <c>$include</c> directives within an array, preserving order.
    /// </summary>
    private static JArray ExpandArrayIncludes(JArray array, string baseDirectory, HashSet<string> visitedFragments) {
        var result = new JArray();

        foreach (var item in array) {
            // Check if this item is an $include directive
            if (item is JObject obj && obj.TryGetValue(IncludeProperty, out var includeToken)) {
                // Validate include value
                if (includeToken.Type != JTokenType.String || string.IsNullOrWhiteSpace(includeToken.Value<string>())) {
                    throw JsonExtendsException.InvalidIncludeValue(
                        includeToken.Type == JTokenType.String ? "empty string" : includeToken.Type.ToString()
                    );
                }

                var includePath = includeToken.Value<string>()!;
                var fragmentPath = ResolveFragmentPath(includePath, baseDirectory);

                // Check for circular includes
                var normalizedPath = Path.GetFullPath(fragmentPath).ToLowerInvariant();
                if (visitedFragments.Contains(normalizedPath)) {
                    throw JsonExtendsException.CircularFragmentInclude(
                        fragmentPath,
                        visitedFragments.Append(normalizedPath).ToList()
                    );
                }

                // Load and expand the fragment
                var fragmentArray = LoadFragment(fragmentPath);

                // Track this fragment for circular detection
                var newVisited = new HashSet<string>(visitedFragments) { normalizedPath };

                // Recursively expand includes within the fragment
                var expandedFragment =
                    ExpandArrayIncludes(fragmentArray, Path.GetDirectoryName(fragmentPath)!, newVisited);

                // Add all fragment items to result
                foreach (var fragmentItem in expandedFragment) result.Add(fragmentItem.DeepClone());
            } else {
                // Regular item - just add it
                // If it's an object, recursively process it for nested arrays
                if (item is JObject itemObj) {
                    var cloned = (JObject)itemObj.DeepClone();
                    ExpandIncludes(cloned, baseDirectory, visitedFragments);
                    result.Add(cloned);
                } else
                    result.Add(item.DeepClone());
            }
        }

        return result;
    }

    /// <summary>
    ///     Resolves a fragment path relative to the base directory.
    /// </summary>
    private static string ResolveFragmentPath(string includePath, string baseDirectory) {
        // Add .json extension if not present
        var fragmentFile = includePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? includePath
            : $"{includePath}.json";

        // Resolve relative to base directory
        return Path.GetFullPath(Path.Combine(baseDirectory, fragmentFile));
    }

    /// <summary>
    ///     Loads a fragment file and returns it as a JArray.
    /// </summary>
    private static JArray LoadFragment(string fragmentPath) {
        if (!File.Exists(fragmentPath)) throw JsonExtendsException.FragmentNotFound(fragmentPath);

        try {
            var content = File.ReadAllText(fragmentPath);
            var token = JToken.Parse(content);

            if (token is not JArray array) {
                throw JsonExtendsException.InvalidFragmentFormat(
                    fragmentPath,
                    token.Type.ToString()
                );
            }

            return array;
        } catch (JsonExtendsException) {
            throw; // Re-throw our custom exceptions
        } catch (Exception ex) {
            throw JsonExtendsException.FragmentLoadFailed(fragmentPath, ex);
        }
    }
}