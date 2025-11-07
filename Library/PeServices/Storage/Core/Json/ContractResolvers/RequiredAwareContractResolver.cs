using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace PeServices.Storage.Core.Json.ContractResolvers;

/// <summary>
/// Contract resolver that:
/// 1. Orders properties by declaration order (respecting inheritance)
/// 2. Always serializes properties marked with [Required] attribute
/// 3. Skips serializing non-required properties when they equal their type's default values
/// </summary>
internal class RequiredAwareContractResolver : DefaultContractResolver {
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        var properties = base.CreateProperties(type, memberSerialization);

        // Create a default instance to compare against
        var defaultInstance = TryCreateDefaultInstance(type);

        // Build inheritance chain from base to derived
        var typeHierarchy = new List<Type>();
        var currentType = type;
        while (currentType != null && currentType != typeof(object)) {
            typeHierarchy.Insert(0, currentType);
            currentType = currentType.BaseType;
        }

        // Create ordered list: base class properties first, then derived class properties
        var orderedProperties = new List<JsonProperty>();
        var seenProperties = new HashSet<JsonProperty>();

        foreach (var t in typeHierarchy) {
            var declaredProps = t.GetProperties(BindingFlags.Public |
                                                BindingFlags.Instance |
                                                BindingFlags.DeclaredOnly)
                .OrderBy(p => p.MetadataToken) // Order by metadata token to ensure declaration order
                .ToList();

            foreach (var declaredProp in declaredProps) {
                var jsonProp = properties.FirstOrDefault(p => p.UnderlyingName == declaredProp.Name);
                if (jsonProp != null && !seenProperties.Contains(jsonProp)) {
                    _ = seenProperties.Add(jsonProp);

                    // Check if property has [Required] attribute
                    var hasRequiredAttribute = declaredProp.GetCustomAttributes(typeof(RequiredAttribute), true).Any();

                    if (hasRequiredAttribute) {
                        // Always serialize required properties, even if they have default values
                        jsonProp.DefaultValueHandling = DefaultValueHandling.Include;
                    } else {
                        // For non-required properties, skip when they match the default value
                        if (defaultInstance != null) {
                            var defaultValue = GetDefaultValue(declaredProp, defaultInstance);
                            jsonProp.ShouldSerialize = instance => {
                                var actualValue = declaredProp.GetValue(instance);
                                return !AreValuesEqual(actualValue, defaultValue, declaredProp.PropertyType);
                            };
                        } else {
                            // Fallback to CLR default handling
                            jsonProp.DefaultValueHandling = DefaultValueHandling.Ignore;
                        }
                    }

                    orderedProperties.Add(jsonProp);
                }
            }
        }

        return orderedProperties;
    }

    /// <summary>
    /// Attempts to create a default instance of the type for comparison.
    /// Handles types with parameterless constructors and types with required properties.
    /// </summary>
    private static object TryCreateDefaultInstance(Type type) {
        try {
            // Try regular parameterless construction first
            return Activator.CreateInstance(type);
        } catch {
            try {
                // For types with required properties, try to find a constructor and pass default values
                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                // Try to find the simplest constructor (one that might be generated for records with required properties)
                var constructor = constructors.OrderBy(c => c.GetParameters().Length).FirstOrDefault();
                if (constructor != null) {
                    var parameters = constructor.GetParameters();
                    var paramValues = new object[parameters.Length];

                    for (var i = 0; i < parameters.Length; i++) {
                        var paramType = parameters[i].ParameterType;
                        // Try to create a default value for each parameter
                        paramValues[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                    }

                    return constructor.Invoke(paramValues);
                }
            } catch {
                // Ignore and fall through
            }

            // If all else fails, return null (will skip default value comparisons)
            return null;
        }
    }

    private static object GetDefaultValue(PropertyInfo propertyInfo, object defaultInstance) {
        try {
            return propertyInfo.GetValue(defaultInstance);
        } catch {
            return null;
        }
    }

    private static bool AreValuesEqual(object value1, object value2, Type propertyType) {
        if (value1 == null && value2 == null) {
            return true;
        }

        if (value1 == null || value2 == null) {
            return false;
        }

        // Special handling for collections - compare by content, not reference
        if (value1 is System.Collections.IEnumerable enum1 && value2 is System.Collections.IEnumerable enum2) {
            // Don't treat strings as collections
            if (value1 is string || value2 is string) {
                return Equals(value1, value2);
            }

            var list1 = enum1.Cast<object>().ToList();
            var list2 = enum2.Cast<object>().ToList();

            if (list1.Count != list2.Count) {
                return false;
            }

            // For empty collections, consider them equal
            if (list1.Count == 0 && list2.Count == 0) {
                return true;
            }

            // For non-empty collections, compare element by element
            return list1.SequenceEqual(list2);
        }

        var comparerType = typeof(EqualityComparer<>).MakeGenericType(propertyType);
        var defaultComparer = comparerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (defaultComparer == null) {
            return Equals(value1, value2);
        }

        var equalsMethod = comparerType.GetMethod("Equals", new[] { propertyType, propertyType });
        if (equalsMethod != null) {
            return (bool)(equalsMethod.Invoke(defaultComparer, new[] { value1, value2 }) ?? false);
        }

        return Equals(value1, value2);
    }
}

