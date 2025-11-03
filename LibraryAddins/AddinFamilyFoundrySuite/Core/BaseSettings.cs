using PeServices.Storage.Core;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public class BaseSettings<TProfile> where TProfile : BaseProfileSettings, new() {
    [Description(
        "Current profile to use for the command. This determines which profile is used in the next launch of a command.")]
    [Required]
    public string CurrentProfile { get; set; } = "Default";

    [Required] public OnProcessingFinishSettings OnProcessingFinish { get; set; } = new();
}

public class OnProcessingFinishSettings : LoadAndSaveOptions {

}