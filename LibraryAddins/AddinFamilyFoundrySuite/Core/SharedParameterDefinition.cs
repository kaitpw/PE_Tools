namespace AddinFamilyFoundrySuite.Core;

public record SharedParameterDefinition(
    ExternalDefinition ExternalDefinition,
    ForgeTypeId GroupTypeId,
    bool IsInstance
);

