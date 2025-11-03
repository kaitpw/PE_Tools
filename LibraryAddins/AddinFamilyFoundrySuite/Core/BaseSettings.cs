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


    public TProfile GetProfile(SettingsManager settingsManager) {
        var profilePath = Path.Combine(
            settingsManager.GetProfilesFolderPath(), $"{this.CurrentProfile}.json");
        return settingsManager.Json<TProfile>(profilePath).Read();
    }
}

public class OnProcessingFinishSettings : LoadAndSaveOptions {
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