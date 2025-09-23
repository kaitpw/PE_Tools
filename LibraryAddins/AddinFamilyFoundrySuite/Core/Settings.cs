using PeServices.Aps;
using PeServices.Storage;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public class FamilyFoundryBaseSettings : Aps.IOAuthTokenProvider, Aps.IParametersTokenProvider {
    [Description(
        "Use cached Parameters Service data instead of downloading from APS on every run. " +
        "Only set to true if you are sure no one has changed the param definitions since the last time you opened Revit " +
        "and/or you are running this command in quick succession.")]
    [Required]
    public bool UseCachedParametersServiceData { get; set; } = true;

    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

    public ParameterAdditionSettings ParameterAdditionSettings { get; set; } = new();

    [Description(
        "Current profile to use for the command. This determines which profile is used in the next launch of a command.")]
    public string CurrentProfile { get; set; } = "";

    [Description(
        "Profiles for the command. The profile that a command uses is determined by the `CurrentProfile` property.")]
    public List<object> Profiles { get; set; } = [];


    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
    public string GetAccountId() => Storage.GlobalSettings().Json().Read().Bim360AccountId;
    public string GetGroupId() => Storage.GlobalSettings().Json().Read().ParamServiceGroupId;
    public string GetCollectionId() => Storage.GlobalSettings().Json().Read().ParamServiceCollectionId;
}

public class ParameterAdditionSettings {
    public ParametersServiceSettings ParametersService { get; init; } = new();
    public SharedParameterSettings SharedParameter { get; init; } = new();
    public FamilyParameterSettings FamilyParameter { get; init; } = new();

    public class ParametersServiceSettings {
        public PsRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();

        public class PsRecoverFromErrorSettings {
            public bool ReplaceParameterWithMatchingName { get; init; } = true;
        }
    }

    public class FamilyParameterSettings {
        [Description(
            "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
        [Required]
        public bool OverrideExistingValues { get; set; } = true;
        // public FpRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();
        //
        // public class FpRecoverFromErrorSettings {
        //     public bool DangerouslyReplaceParameterWithMatchingName;
        // }
    }


    public class SharedParameterSettings {
        //     public SpRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();
        //
        //     public class SpRecoverFromErrorSettings {
        //         public bool DangerouslyReplaceParameterWithMatchingName;
        //     }
    }
}