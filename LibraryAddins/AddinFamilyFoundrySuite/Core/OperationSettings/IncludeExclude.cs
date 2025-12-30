using AddinFamilyFoundrySuite.Core.SchemaProviders;
using PeServices.Storage.Core.Json.SchemaProcessors;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class IncludeFamilies {
    [SchemaExamples(typeof(FamilyNamesProvider))]
    public List<string> Equaling { get; init; } = [];

    [SchemaExamples(typeof(FamilyNamesProvider))]
    public List<string> Containing { get; init; } = [];

    [SchemaExamples(typeof(FamilyNamesProvider))]
    public List<string> StartingWith { get; init; } = [];
}

public class ExcludeFamilies {
    [SchemaExamples(typeof(FamilyNamesProvider))]
    public List<string> Equaling { get; init; } = [];

    [SchemaExamples(typeof(FamilyNamesProvider))]
    public List<string> Containing { get; init; } = [];

    [SchemaExamples(typeof(FamilyNamesProvider))]
    public List<string> StartingWith { get; init; } = [];
}

public class IncludeSharedParameter {
    [SchemaExamples(typeof(SharedParameterNamesProvider))]
    public List<string> Equaling { get; init; } = [];

    [SchemaExamples(typeof(SharedParameterNamesProvider))]
    public List<string> Containing { get; init; } = [];

    [SchemaExamples(typeof(SharedParameterNamesProvider))]
    public List<string> StartingWith { get; init; } = [];
}

public class ExcludeSharedParameter {
    [SchemaExamples(typeof(SharedParameterNamesProvider))]
    public List<string> Equaling { get; init; } = [];

    [SchemaExamples(typeof(SharedParameterNamesProvider))]
    public List<string> Containing { get; init; } = [];

    [SchemaExamples(typeof(SharedParameterNamesProvider))]
    public List<string> StartingWith { get; init; } = [];
}