using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class MakeRefPlaneAndDimsSettings : IOperationSettings {
    [Required] public List<RefPlaneSpec> Specs { get; init; } = [];

    public bool Enabled { get; init; } = true;
}

public class RefPlaneSpec {
    public required string Name { get; set; }
    public required string AnchorName { get; set; }
    public Placement Placement { get; set; } = Placement.Mirror;
    public string Parameter { get; set; }
    public RpStrength Strength { get; set; } = RpStrength.NotARef;
}

[JsonConverter(typeof(StringEnumConverter))]
public enum Placement { Positive, Mirror, Negative }

[JsonConverter(typeof(StringEnumConverter))]
public enum RpStrength {
    Left = 0,
    CenterLR = 1,
    Right = 2,
    Front = 3,
    CenterFB = 4,
    Back = 5,
    Bottom = 6,
    CenterElev = 7,
    Top = 8,
    NotARef = 12,
    StrongRef = 13,
    WeakRef = 14
}