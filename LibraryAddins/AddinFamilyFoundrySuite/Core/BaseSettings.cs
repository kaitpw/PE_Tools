using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public class BaseSettings<TProfile> where TProfile : BaseProfileSettings, new() {
    [Required] public OnProcessingFinishSettings OnProcessingFinish { get; set; } = new();

    [Description(
        "Current profile to use for the command. This determines which profile is used in the next launch of a command.")]
    [Required]
    public string CurrentProfile { get; set; } = "Default";

    [Description(
        "Profiles for the command. The profile that a command uses is determined by the `CurrentProfile` property.")]
    [Required]
    public Dictionary<string, TProfile> Profiles { get; set; } = new() { { "Default", new TProfile() } };

    public TProfile GetProfile() => this.Profiles[this.CurrentProfile];
}

public class OnProcessingFinishSettings : ILoadAndSaveOptions {
    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

    [Description(
        "Load processed family(ies) into the main model document (if the command is run on a main model document)")]
    [Required]
    public bool LoadFamily { get; set; } = true;

    [Description("Save processed family(ies) to the internal path of the family document on your computer")]
    [Required]
    public bool SaveFamilyToInternalPath { get; set; } = false;

    [Description("Save processed family(ies) to the output directory of the command")]
    [Required]
    public bool SaveFamilyToOutputDir { get; set; } = false;
}