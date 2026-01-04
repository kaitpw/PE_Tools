using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PeServices.Storage.Core.Json.Converters;
using PeServices.Storage.Core.Json.RevitTypes;

namespace PeServices.Storage.Core.Json.ContractResolvers;

/// <summary>
///     Contract resolver that applies discriminator-based converters to properties.
///     For ForgeTypeId properties with [ForgeKind] attributes, this applies the
///     appropriate converter (SpecTypeConverter or GroupTypeConverter) based on the discriminator.
/// </summary>
internal class RevitTypeContractResolver : OrderedContractResolver {
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);

        if (member is PropertyInfo propInfo) {
            // Check if property type is registered in RevitTypeRegistry
            if (RevitTypeRegistry.TryGet(propInfo.PropertyType, out var registration) && registration != null) {
                Type converterType = null;

                // If type has discriminator, check for attribute and select converter
                if (registration.DiscriminatorType != null && registration.ConverterSelector != null) {
                    var discriminatorAttr = propInfo.GetCustomAttribute(registration.DiscriminatorType);
                    if (discriminatorAttr != null) {
                        converterType = registration.ConverterSelector(discriminatorAttr);
                    }
                }

                // Fall back to default converter if no discriminator or no match
                converterType ??= registration.DefaultConverter;

                // Apply converter to property
                if (converterType != null) {
                    property.Converter = (JsonConverter)Activator.CreateInstance(converterType);
                }
            }
        }

        return property;
    }
}
