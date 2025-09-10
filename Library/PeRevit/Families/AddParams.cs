using Json.Schema.Generation;
using Nice3point.Revit.Extensions;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace PeRevit.Families;

public static class AddParams {
    public static List<Result<FamilyParameter>> Family(
        Document famDoc,
        FamilyParamInfo[] parameters
    ) {
        var fm = famDoc.FamilyManager;
        var results = new List<Result<FamilyParameter>>();
        foreach (FamilyType type in fm.Types) {
            fm.CurrentType = type;
            foreach (var param in parameters) {
                try {
                    var parameter = fm.FindParameter(param.Name);
                    parameter ??= fm.AddParameter(param.Name, param.Group, param.Category, param.IsInstance);
                    results.Add(parameter);
                } catch (Exception ex) {
                    results.Add(ex);
                }
            }
        }

        return results;
    }

    public static List<Result<FamilyParameter>> SharedParam(
        Document famDoc,
        List<SharedParameterElement> sharedParams
    ) {
        var fm = famDoc.FamilyManager;
        var results = new List<Result<FamilyParameter>>();

        foreach (var sharedParam in sharedParams) {
            try {
                var externalDefinition = famDoc.Application.OpenSharedParameterFile()?.Groups?
                    .SelectMany(g => g.Definitions)
                    .OfType<ExternalDefinition>()
                    .FirstOrDefault(def => def.GUID == sharedParam.GuidValue);

                if (externalDefinition != null)
                    results.Add(fm.AddParameter(externalDefinition, GroupTypeId.General, true));
            } catch (Exception ex) {
                throw new Exception($"Failed to add parameter service parameter {sharedParam.Name}: {ex.Message}");
            }
        }

        return results;
    }

    public static List<Result<SharedParameterElement>> ParamService(
        Document famDoc,
        PsRecoverFromErrorSettings settings,
        ParamModel psParamInfos
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var finalDownloadResults = new List<Result<SharedParameterElement>>();

        foreach (var psParamInfo in psParamInfos.Results) {
            if (psParamInfo.TypedMetadata.IsArchived) continue;
            var dlOpts = new ParameterDownloadOptions(
                new HashSet<ElementId>(),
                psParamInfo.DownloadOptions.IsInstance,
                psParamInfo.DownloadOptions.Visible,
                GroupTypeId.General);
            var parameterTypeId = psParamInfo.DownloadOptions.ParameterTypeId;
            try {
                finalDownloadResults.Add(ParameterUtils.DownloadParameter(famDoc, dlOpts, parameterTypeId));
            } catch (Exception ex) {
                finalDownloadResults.Add(HandleDownloadError(famDoc, psParamInfo, ex, settings));
            }
        }

        return finalDownloadResults;
    }

    // TODO: this needs some major love and testing
    private static Result<SharedParameterElement> HandleDownloadError(
        Document famDoc,
        ParamModelRes psParamInfo,
        Exception downloadErr,
        PsRecoverFromErrorSettings settings
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var fm = famDoc.FamilyManager;
        // var balloon = new Balloon();
        var parameterTypeId = psParamInfo.DownloadOptions.ParameterTypeId;
        var paramMsg = $"\n({psParamInfo.Name}: {parameterTypeId})";
        var downloadOptions = new ParameterDownloadOptions();
        try {
            downloadOptions = ParameterUtils.DownloadParameterOptions(parameterTypeId);
        } catch (Exception ex) {
            downloadErr = new Exception(downloadErr.Message, ex);
        }

        switch (downloadErr.Message) {
        case { } msg when msg.Contains("Parameter with a matching name"):
            try {
                if (settings.ReplaceParameterWithMatchingName) {
                    var currentParam = fm.FindParameter(psParamInfo.Name);
                    fm.RemoveParameter(currentParam);
                    return ParameterUtils.DownloadParameter(famDoc, downloadOptions, parameterTypeId);
                }

                return downloadErr;
            } catch (Exception ex) {
                return new Exception($"Failed to recover from a \"matching name\" error {paramMsg}", ex);
            }
        case { } msg when msg.Contains("Parameter with a matching GUID"):
            // return fm.FindParameter(new ForgeTypeId(originalParamInfo.Id)); // TODO: Figure this out!!!!!!!!!!!!
            return new Exception("TODO: recover from \"param with matching GUID\" error");
        default:
            return new Exception($"Skipped recovery for unknown error {downloadErr.Message} ", downloadErr);
        }
    }


    public record FamilyParamInfo {
        public string Name { get; init; }
        public ForgeTypeId Group { get; init; } // must find how to default to other
        public ForgeTypeId Category { get; init; }
        public bool IsInstance { get; init; } = true;
        public object Value { get; init; }
    }
}

public class ParameterAdditionSettings {
    public ParametersServiceSettings ParametersService { get; init; } = new();
    public SharedParameterSettings SharedParameter { get; init; } = new();
    public FamilyParameterSettings FamilyParameter { get; init; } = new();
}

public class ParametersServiceSettings {
    public PsRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();
}

public class PsRecoverFromErrorSettings {
    public bool ReplaceParameterWithMatchingName { get; init; } = true;
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