using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations.Doc;

public static class AddApsParamsOperation {
    public static List<Result<SharedParameterElement>> AddApsParams(
        this Document famDoc,
        ParamModel psParamInfos,
        Func<ParamModelRes, bool> filter = null
    ) {
        if (famDoc is null) throw new ArgumentNullException(nameof(famDoc));
        if (psParamInfos is null) throw new ArgumentNullException(nameof(psParamInfos));
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var fm = famDoc.FamilyManager;

        var finalDownloadResults = new List<Result<SharedParameterElement>>();

        var filteredResults = filter != null
            ? psParamInfos.Results.Where(filter).ToList()
            : psParamInfos.Results;

        var (defFile, _) = MakeTempSharedParamTxt(famDoc);
        var group = defFile.Groups.get_Item("Parameters") ?? defFile.Groups.Create("Parameters");

        foreach (var psParamInfo in filteredResults) {
            if (psParamInfo.TypedMetadata.IsArchived) continue;

            var (sharedParam, sharedParamErr) = DownloadParameterFast(famDoc, fm, group, psParamInfo);
            if (sharedParamErr is not null) {
                finalDownloadResults.Add(DownloadParameterNative(famDoc, psParamInfo));
                continue;
            }

            finalDownloadResults.Add(sharedParam);
        }

        try {
            if (File.Exists(defFile.Filename)) File.Delete(defFile.Filename);
        } catch {
            Debug.WriteLine("Failed to delete temporary shared param file.");
        }

        return finalDownloadResults;
    }

    private static Result<SharedParameterElement> DownloadParameterNative(Document famDoc, ParamModelRes psParamInfo) {
        var parameterTypeId = psParamInfo.DownloadOptions.ParameterTypeId;
        var dlOpts = new ParameterDownloadOptions(
            new HashSet<ElementId>(),
            psParamInfo.DownloadOptions.IsInstance,
            psParamInfo.DownloadOptions.Visible,
            GroupTypeId.General);

        try {
            return ParameterUtils.DownloadParameter(famDoc, dlOpts, parameterTypeId);
        } catch (Exception downloadErr) {
            if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
            var paramMsg = $"\n{psParamInfo.Name} ({parameterTypeId})";

            switch (downloadErr.Message) {
            case { } msg when msg.Contains("Parameter with a matching name"):
                try {
                    var fm = famDoc.FamilyManager;
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
    }

    private static Result<SharedParameterElement> DownloadParameterFast(Document famDoc,
        FamilyManager fm,
        DefinitionGroup group,
        ParamModelRes psParamInfo) {
        try {
            if (group is null) throw new ArgumentNullException(nameof(group));

            // Extract the actual GUID from Parameters Service ID (similar to your FindParameter method)
            var parameterTypeId = psParamInfo.DownloadOptions.ParameterTypeId;
            var typeId = parameterTypeId.TypeId;
            var typeIdParts = typeId?.Split(':');
            if (typeIdParts == null || typeIdParts.Length < 2)
                throw new ArgumentException($"ParameterTypeId is not of the Parameters Service format: {typeId}");

            var parameterPart = typeIdParts[1];
            var dashIndex = parameterPart.IndexOf('-');
            var guidText = dashIndex > 0 ? parameterPart[..dashIndex] : parameterPart;

            if (!Guid.TryParse(guidText, out var guid))
                throw new ArgumentException($"Could not extract GUID from parameterTypeId: {typeId}");

            // Use the correct data type from Parameters Service
            var dataTypeId = new ForgeTypeId(psParamInfo.SpecId);
            var options = new ExternalDefinitionCreationOptions(psParamInfo.Name, dataTypeId) {
                GUID = guid, // Use the actual Parameters Service GUID
                Visible = psParamInfo.DownloadOptions.Visible,
                UserModifiable = !psParamInfo.ReadOnly,
                Description = psParamInfo.Description ?? ""
            };

            var externalDef = group.Definitions.Create(options) as ExternalDefinition;

            // Add parameter to family using FamilyManager
            var familyParam = fm.AddParameter(externalDef,
                psParamInfo.DownloadOptions.GroupTypeId,
                psParamInfo.DownloadOptions.IsInstance);

            // Find the SharedParameterElement that was created using the original Parameters Service ID
            var sharedParamElement = famDoc.FindParameter(parameterTypeId);
            if (sharedParamElement != null) return sharedParamElement;

            // If we can't find the SharedParameterElement, the FamilyParameter creation succeeded
            // which is what we care about for the family, so this isn't necessarily an error
            return new Exception($"Parameter {psParamInfo.Name} added to family but SharedParameterElement not found");
        } catch (Exception ex) {
            return ex;
        }
    }

    private static Result<DefinitionFile> MakeTempSharedParamTxt(Document famDoc) {
        var app = famDoc.Application;
        var tempSharedParamFile = Path.GetTempFileName() + ".txt";
        using (File.Create(tempSharedParamFile)) { } // Create empty file

        app.SharedParametersFilename = tempSharedParamFile;
        try {
            return app.OpenSharedParameterFile();
        } catch (Exception ex) {
            return new Exception($"Failed to create temp shared parameter file: {ex.Message}");
        }
    }
}