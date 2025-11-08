namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class Include
{
    public List<string> Equaling { get; init; } = [];
    public List<string> Containing { get; init; } = [];
    public List<string> StartingWith { get; init; } = [];
}

public class Exclude
{
    public List<string> Equaling { get; init; } = [];
    public List<string> Containing { get; init; } = [];
    public List<string> StartingWith { get; init; } = [];
}