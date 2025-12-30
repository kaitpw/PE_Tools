using AddinFamilyFoundrySuite.Core.Snapshots;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class MakeRefPlaneAndDimsSettings : IOperationSettings {
    [Required] public List<RefPlaneSpec> Specs { get; init; } = [];

    public bool Enabled { get; init; } = true;
}