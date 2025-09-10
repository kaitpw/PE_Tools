using Json.Schema.Generation;
using Nice3point.Revit.Extensions;
using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps;
using PeServices.Aps.Core;
using PeServices.Aps.Models;
using PeServices.Storage;
#if !REVIT2023 && !REVIT2024
#endif

namespace PE_Tools;

// TODO: 
// - add support for all param types (both in creating and verifying)
// - add support for shared parameters/parameters service
// - add support for formulas
// - add support for getting the value from an existing parameter
// - add support for cleaning family
//     - purge unused nested families
//     - delete unused reference lines
//     - delete unused params with no value (may need to discriminate more specifically)
//     - maybe delete certain reference lines
//     - delete linear dimensions (maybe?)
// - retroactively change the group of the params

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundry : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;


        // Get the first editable family in the project
        var families = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.IsEditable)
            .Where(f => f.Name.Contains("Price LBP15A Exhaust")) // Price LBP15A Exhaust, Fantech RC Series, 
            .ToList();

        // // TODO: remove this after testing family parameter additions
        // var famParamInfos = new[] {
        //     new AddParams.FamilyParamInfo {
        //         Name = "TEST5_Instance",
        //         Group = GroupTypeId.General,
        //         Category = SpecTypeId.String.Text,
        //         IsInstance = true,
        //         Value = "TEST1"
        //     },
        //     new AddParams.FamilyParamInfo {
        //         Name = "TEST5_Type",
        //         Group = GroupTypeId.General,
        //         Category = SpecTypeId.String.Text,
        //         IsInstance = false,
        //         Value = "TEST1"
        //     }
        // };


        var balloon = new Balloon();

        try {
            var storage = new Storage("FamilyFoundry");
            var settings = storage.Settings().Json<FamilyFoundrySettings>().Read();
            var svcAps = new Aps(settings);
            var svcApsParams = svcAps.Parameters(settings);
            var psParamInfos = GetParamSvcParamInfo(storage, svcApsParams);
            var psParamDlOpts = GetParameterDownloadOptions(doc, psParamInfos);
            List<Result<SharedParameterElement>> psParamsDownloadResults = [];
            List<Result<FamilyParameter>> psParamAdditionResults = [];

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");
                var fam = FamUtils.EditAndLoad(doc, family,
                    // (famDoc, results) => {
                    //     var result = AddParams.Family(famDoc, famParamInfos,
                    //         settings.ParameterAdditionSettings.FamilyParameter.OverrideExistingValues);
                    //     results.Add(nameof(AddParams.Family), result);
                    // },
                    famDoc => {
                        var recoverFromErrorSettings = settings.ParameterAdditionSettings.ParametersService
                            .RecoverFromErrorSettings;
                        var validPsParamDlOpts = psParamDlOpts.Where(o => o.PsParamDlOptsResult.AsTuple().value != null)
                            .ToList();
                        psParamsDownloadResults = DownloadParamSvcParams(famDoc, svcApsParams,
                            recoverFromErrorSettings,
                            validPsParamDlOpts);
                    },
                    famDoc => {
                        var psParams = psParamsDownloadResults
                            .Where(p => p.AsTuple().value != null)
                            .Select(p => p.AsTuple().value)
                            .ToList();
                        psParamAdditionResults = AddParams.ParamSvc(famDoc, psParams);
                    },
                    famDoc => SortParams(famDoc, ParametersOrder.Ascending)
                );
            }

            // tODO: write to output somehow

            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            _ = TaskDialog.Show("Error",
                $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}");
            return Result.Cancelled;
        }
    }

    private static ParametersApi.Parameters GetParamSvcParamInfo(Storage storage, Parameters svcApsParams) {
        const string cacheFileName = "parameters-service-cache.json";
        // var cache = storage.State().Json<ParametersApi.Parameters>(cacheFileName); // TODO, figure this out later
        var tcsParams = new TaskCompletionSource<Result<ParametersApi.Parameters>>();
        _ = Task.Run(async () => {
            try {
                tcsParams.SetResult(await svcApsParams.GetParameters());
            } catch (Exception ex) {
                tcsParams.SetResult(ex);
            }
        });
        tcsParams.Task.Wait();

        var (parameters, paramsResult) = tcsParams.Task.Result;
        return paramsResult != null ? throw paramsResult : parameters;
    }

    public static List<DlOptsConstructionResult> GetParameterDownloadOptions(
        Document doc,
        ParametersApi.Parameters psParamInfos
    ) {
        // if (!doc.IsFamilyDocument) throw new ArgumentException("Document is not a family document");
        var dlOptsList = new List<DlOptsConstructionResult>();
        if (psParamInfos is { Results: null }) return dlOptsList.ToList();
        foreach (var psParamInfo in psParamInfos.Results) {
            try {
                if (psParamInfo.TypedMetadata.IsArchived) continue;
                var downloadOpts = new ParameterDownloadOptions(
                    new HashSet<ElementId>(), // TODO: come back to
                    psParamInfo.DownloadOptions.IsInstance,
                    psParamInfo.DownloadOptions.Visible,
                    GroupTypeId.General);
                dlOptsList.Add(new DlOptsConstructionResult(
                    psParamInfo, downloadOpts));
            } catch (Exception ex) {
                if (ex.IsFromMethod(nameof(ParameterUtils.DownloadParameterOptions))) {
                    var msg = "Parameter cannot have an empty instance/type association in parameters service";
                    dlOptsList.Add(new DlOptsConstructionResult(
                        psParamInfo, new InvalidOperationException(msg, ex)));
                }
            }
        }

        return dlOptsList;
    }

    private static List<Result<SharedParameterElement>> DownloadParamSvcParams(
        Document famDoc,
        Parameters apsParams,
        PsRecoverFromErrorSettings settings,
        List<DlOptsConstructionResult> psParamDlOpts
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var downloadResults = apsParams.DownloadParameters(famDoc, psParamDlOpts);
        var finalDownloadResults = new List<Result<SharedParameterElement>>();

        // Handle Download Errors
        foreach (var downloadResult in downloadResults) {
            var originalPsParamInfo = downloadResult.OriginalParameter;
            var (sharedParam, downloadErr) = downloadResult.DownloadResult;
            if (downloadErr is not null) {
                finalDownloadResults.Add(RecoverDownloadError(famDoc, originalPsParamInfo, downloadErr, settings));
                continue;
            }

            finalDownloadResults.Add(sharedParam);
        }

        return finalDownloadResults;
    }

    private static Result<SharedParameterElement> RecoverDownloadError(
        Document famDoc,
        ParametersApi.Parameters.ParametersResult originalParamInfo,
        Exception downloadErr,
        PsRecoverFromErrorSettings settings
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var fm = famDoc.FamilyManager;
        // var balloon = new Balloon();
        var parameterTypeId = originalParamInfo.DownloadOptions.ParameterTypeId;
        var paramMsg = $"\n({originalParamInfo.Name}: {parameterTypeId})";
        var downloadOptions = new ParameterDownloadOptions();
        try {
            downloadOptions = ParameterUtils.DownloadParameterOptions(parameterTypeId);
        } catch (Exception ex) {
            downloadErr = new Exception(downloadErr.Message, ex);
        }

        var familyCategorySet = new HashSet<ElementId> { famDoc.OwnerFamily.FamilyCategoryId };

        switch (downloadErr.Message) {
        case { } msg when msg.Contains("empty category set"):
            try {
                downloadOptions.SetCategories(familyCategorySet);
                return ParameterUtils.DownloadParameter(famDoc, downloadOptions, parameterTypeId);
            } catch (Exception ex) {
                return new Exception($"Failed to recover from an empty category set {paramMsg}", ex);
            }
        case { } msg when msg.Contains("empty instance/type association"):
            try {
                downloadOptions.Visible = true;
                downloadOptions.IsInstance = true;
                downloadOptions.SetGroupTypeId(GroupTypeId.General); // TODO: come back to this default
                downloadOptions.SetCategories(familyCategorySet);
                return ParameterUtils.DownloadParameter(famDoc, downloadOptions, parameterTypeId);
            } catch (Exception ex) {
                return new Exception($"Failed to recover from an empty instance/type association {paramMsg}", ex);
            }
        case { } msg when msg.Contains("Parameter with a matching name"):
            try {
                if (settings.ReplaceParameterWithMatchingName) {
                    var currentParam = fm.FindParameter(originalParamInfo.Name);
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

    private static void SortParams(Document famDoc, ParametersOrder order) =>
        famDoc.FamilyManager.SortParameters(order);
}

public class DlOptsConstructionResult(
    ParametersApi.Parameters.ParametersResult psParamInfo,
    Result<ParameterDownloadOptions> psParamDlOptsResult) {
    public ParametersApi.Parameters.ParametersResult PsParamInfo { get; } = psParamInfo;
    public Result<ParameterDownloadOptions> PsParamDlOptsResult { get; } = psParamDlOptsResult;
}

public class FamilyFoundrySettings : Storage.BaseSettings, Aps.IOAuthTokenProvider, Aps.IParametersTokenProvider {
    [Description(
        "Use cached Parameters Service data instead of downloading from APS on every run. " +
        "Only set to true if you are sure no one has changed the param definitions since the last time you opened Revit " +
        "and/or you are running this command in quick succession.")]
    [Required]
    public bool UseCachedParametersServiceData { get; set; } = true;

    [Description("Remove parameters that have no values during family cleanup operations")]
    [Required]
    public bool DeleteEmptyParameters { get; set; } = true; // unused right now

    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

    public ParameterAdditionSettings ParameterAdditionSettings { get; set; } = new();


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