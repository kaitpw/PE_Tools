using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public class BaseSettings<TProfile> where TProfile : BaseProfileSettings, new() {
    [Required] public OnProcessingFinishSettings OnProcessingFinish { get; set; } = new();
}

public class OnProcessingFinishSettings : LoadAndSaveOptions {
}