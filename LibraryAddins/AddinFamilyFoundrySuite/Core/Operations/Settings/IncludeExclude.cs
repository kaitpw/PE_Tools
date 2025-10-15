using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations.Settings;

public class Include {
    [Required] public List<string> Equaling { get; init; } = [];
    [Required] public List<string> Containing { get; init; } = [];
    [Required] public List<string> StartingWith { get; init; } = [];
}

public class Exclude {
    [Required] public List<string> Equaling { get; init; } = [];
    [Required] public List<string> Containing { get; init; } = [];
    [Required] public List<string> StartingWith { get; init; } = [];
}