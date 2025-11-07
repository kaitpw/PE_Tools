using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PeServices.Storage.Core.Json.ContractResolvers;
/// <summary> Contract resolver that orders properties by declaration order, respecting inheritance hierarchy </summary>
class OrderedContractResolver : DefaultContractResolver {
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        var properties = base.CreateProperties(type, memberSerialization);

        // Build inheritance chain from base to derived
        var typeHierarchy = new List<Type>();
        var currentType = type;
        while (currentType != null && currentType != typeof(object)) {
            typeHierarchy.Insert(0, currentType);
            currentType = currentType.BaseType;
        }

        // Create ordered list: base class properties first, then derived class properties
        var orderedProperties = new List<JsonProperty>();
        foreach (var t in typeHierarchy) {
            var declaredProps = t.GetProperties(BindingFlags.Public |
                                                BindingFlags.Instance |
                                                BindingFlags.DeclaredOnly);

            foreach (var declaredProp in declaredProps) {
                var jsonProp = properties.FirstOrDefault(p => p.UnderlyingName == declaredProp.Name);
                if (jsonProp != null && !orderedProperties.Contains(jsonProp)) orderedProperties.Add(jsonProp);
            }
        }

        return orderedProperties;
    }
}