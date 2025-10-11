using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Settings;

public class BaseSettings<T> : ILoadAndSaveOptions where T : BaseProfileSettings, new() {
    [Description(
        "Use cached Parameters Service data instead of downloading from APS on every run. " +
        "Only set to true if you are sure no one has changed the param definitions since the last time you opened Revit " +
        "and/or you are running this command in quick succession.")]
    [Required]
    public bool UseCachedParametersServiceData { get; set; } = true;

    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

    [Description("Load processed family(ies) into the main model document (if the command is run on a main model document)")]
    public bool LoadFamily { get; set; } = true;

    [Description("Save processed family(ies) to the internal path of the family document on your computer")]
    public bool SaveFamilyToInternalPath { get; set; } = false;

    [Description("Save processed family(ies) to the output directory of the command")]
    public bool SaveFamilyToOutputDir { get; set; } = false;

    [Description(
        "Current profile to use for the command. This determines which profile is used in the next launch of a command.")]
    [Required]
    public string CurrentProfile { get; set; } = "Default";

    [Description(
        "Profiles for the command. The profile that a command uses is determined by the `CurrentProfile` property.")]
    [Required]
    public Dictionary<string, T> Profiles { get; set; } = new() { { "Default", new T() } };

    public T GetProfile() => this.Profiles[this.CurrentProfile];
}
