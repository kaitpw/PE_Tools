using PeExtensions.FamManager;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace PeExtensions.FamDocument;

public static class FamilyDocumentAddParameter {
    /// <summary>
    ///     Add a family parameter. PropertiesGroup must be a <c>GroupTypeId</c> and DataType must be a <c>SpecTypeId</c>.
    /// </summary>
    public static FamilyParameter AddFamilyParameter(
        this Document famDoc,
        string name,
        ForgeTypeId propertiesGroup,
        ForgeTypeId dataType,
        bool isInstance
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");

        var fm = famDoc.FamilyManager;

        var parameter = fm.FindParameter(name);
        parameter ??= fm.AddParameter(name, propertiesGroup, dataType, isInstance);
        return parameter;
    }


    public static Result<SharedParameterElement> AddApsParameterSlow(
        this Document famDoc,
        ParamModelRes apsParamModel
    ) {
        var parameterTypeId = apsParamModel.DownloadOptions.ParameterTypeId;
        var dlOpts = new ParameterDownloadOptions(
            new HashSet<ElementId>(),
            apsParamModel.DownloadOptions.IsInstance,
            apsParamModel.DownloadOptions.Visible,
            GroupTypeId.General);

        try {
            return ParameterUtils.DownloadParameter(famDoc, dlOpts, parameterTypeId);
        } catch (Exception downloadErr) {
            if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
            var paramMsg = $"\n{apsParamModel.Name} ({parameterTypeId})";

            switch (downloadErr.Message) {
            case { } msg when msg.Contains("Parameter with a matching name"):
                try {
                    var fm = famDoc.FamilyManager;
                    var currentParam = fm.FindParameter(apsParamModel.Name);
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

    public static Result<SharedParameterElement> AddApsParameter(this Document famDoc,
        FamilyManager fm,
        DefinitionGroup group,
        ParamModelRes apsParamModel
    ) {
        try {
            var externalDef = apsParamModel.GetExternalDefinition(group);

            // Add parameter to family using FamilyManager
            var familyParam = fm.AddParameter(externalDef,
                apsParamModel.DownloadOptions.GroupTypeId,
                apsParamModel.DownloadOptions.IsInstance);

            // Find the SharedParameterElement that was created using the original Parameters Service ID
            var sharedParamElement = famDoc.FindParameter(apsParamModel.DownloadOptions.ParameterTypeId);
            if (sharedParamElement != null) return sharedParamElement;

            // If we can't find the SharedParameterElement, the FamilyParameter creation succeeded
            // which is what we care about for the family, so this isn't necessarily an error
            return new Exception(
                $"Parameter {apsParamModel.Name} added to family but SharedParameterElement not found");
        } catch (Exception ex) {
            return ex;
        }
    }
}