using Nice3point.Revit.Extensions;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace PeRevit.Families;

public static class AddParams {
    public static List<Result<FamilyParameter>> Family(
        Document famDoc,
        FamilyParamInfo[] parameters
    ) {
        ArgumentNullException.ThrowIfNull(famDoc);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
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
        ArgumentNullException.ThrowIfNull(famDoc);
        ArgumentNullException.ThrowIfNull(sharedParams);
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
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
        ParamModel psParamInfos,
        Func<ParamModelRes, bool> filter = null
    ) {
        ArgumentNullException.ThrowIfNull(famDoc);
        ArgumentNullException.ThrowIfNull(psParamInfos);
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var finalDownloadResults = new List<Result<SharedParameterElement>>();

        var filteredResults = filter != null
            ? psParamInfos.Results.Where(filter).ToList()
            : psParamInfos.Results;

        foreach (var psParamInfo in filteredResults) {
            if (psParamInfo.TypedMetadata.IsArchived) continue;

            var parameterTypeId = psParamInfo.DownloadOptions.ParameterTypeId;
            var dlOpts = new ParameterDownloadOptions(
                new HashSet<ElementId>(),
                psParamInfo.DownloadOptions.IsInstance,
                psParamInfo.DownloadOptions.Visible,
                GroupTypeId.General);

            try {
                finalDownloadResults.Add(ParameterUtils.DownloadParameter(famDoc, dlOpts, parameterTypeId));
            } catch (Exception ex) {
                finalDownloadResults.Add(HandleDownloadError(famDoc, psParamInfo, dlOpts, ex, true));
            }
        }

        return finalDownloadResults;
    }

    // TODO: this needs some major love and testing
    private static Result<SharedParameterElement> HandleDownloadError(
        Document famDoc,
        ParamModelRes psParamInfo,
        ParameterDownloadOptions dlOpts,
        Exception downloadErr,
        bool ReplaceParameterWithMatchingName
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var parameterTypeId = psParamInfo.DownloadOptions.ParameterTypeId;
        var paramMsg = $"\n{psParamInfo.Name} ({parameterTypeId})";

        switch (downloadErr.Message) {
        case { } msg when msg.Contains("Parameter with a matching name"):
            try {
                var fm = famDoc.FamilyManager;
                if (!ReplaceParameterWithMatchingName) return downloadErr;
                var currentParam = fm.FindParameter(psParamInfo.Name);
                fm.RemoveParameter(currentParam);
                return ParameterUtils.DownloadParameter(famDoc, dlOpts, parameterTypeId);
            } catch (Exception ex) {
                return new Exception($"Recovery failed for \"matching name\" error with parameter: {paramMsg}", ex);
            }
        case { } msg when msg.Contains("Parameter with a matching GUID"):
            try {
                return famDoc.FindParameter(parameterTypeId);
            } catch (Exception ex) {
                return new Exception($"Recovery failed for \"matching GUID\" error with parameter: {paramMsg}", ex);
            }
        default:
            return new Exception($"Recovery skipped for unknown error: {downloadErr.Message} ", downloadErr);
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