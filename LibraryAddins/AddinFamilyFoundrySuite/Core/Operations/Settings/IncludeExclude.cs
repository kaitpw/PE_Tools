namespace AddinFamilyFoundrySuite.Core.Operations.Settings;

public class Include {
    public List<string> Equaling { get; init; } = [];
    public List<string> Containing { get; init; } = [];
    public List<string> StartingWith { get; init; } = [];
}

public class Exclude {
    public List<string> Equaling { get; init; } = [];
    public List<string> Containing { get; init; } = [];
    public List<string> StartingWith { get; init; } = [];
}