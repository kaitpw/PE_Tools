using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Contract resolver that skips serializing properties when they equal their default values.
///     Default values are determined by comparing the actual property value against a provided default instance.
/// </summary>
public class DefaultValueSkippingContractResolver : DefaultContractResolver {
    private readonly object _defaultInstance;

    public DefaultValueSkippingContractResolver(object defaultInstance) =>
        _defaultInstance = defaultInstance ?? throw new ArgumentNullException(nameof(defaultInstance));

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);

        if (property.PropertyType == null || member is not PropertyInfo propInfo) {
            return property;
        }

        // Skip default value checking for required properties (always serialize them)
        if (this.IsRequiredProperty(propInfo)) {
            return property;
        }

        // Get the default value for this property from the default instance
        var defaultValue = this.GetDefaultValue(propInfo, this._defaultInstance);

        // Set ShouldSerialize to skip when value equals default
        property.ShouldSerialize = instance => {
            var actualValue = propInfo.GetValue(instance);
            return !this.AreValuesEqual(actualValue, defaultValue, propInfo.PropertyType);
        };

        return property;
    }

    /// <summary>
    ///     Checks if a property is required (either via C# 'required' keyword or [Required] attribute).
    /// </summary>
    private bool IsRequiredProperty(PropertyInfo propertyInfo) {
        // Check for [Required] attribute from System.ComponentModel.DataAnnotations
        if (propertyInfo.GetCustomAttribute<RequiredAttribute>() != null) {
            return true;
        }

        // Check for RequiredMemberAttribute (C# 'required' keyword)
        if (propertyInfo.GetCustomAttribute<RequiredMemberAttribute>() != null) {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets the default value for a property from the default instance.
    /// </summary>
    private object GetDefaultValue(PropertyInfo propertyInfo, object defaultInstance) {
        try {
            return propertyInfo.GetValue(defaultInstance);
        } catch {
            // If we can't get the value, return null (property will be serialized)
            return null;
        }
    }

    /// <summary>
    ///     Compares two values for equality, handling null and value types properly.
    /// </summary>
    private bool AreValuesEqual(object value1, object value2, Type propertyType) {
        // Handle null cases
        if (value1 == null && value2 == null) {
            return true;
        }

        if (value1 == null || value2 == null) {
            return false;
        }

        // Use EqualityComparer for proper comparison
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