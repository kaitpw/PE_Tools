using ApsParamModel = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace PeExtensions;

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


    public static Result<SharedParameterElement> AddApsParameterSlow(this Document famDoc, ApsParamModel apsParamModel) {
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
        ApsParamModel apsParamModel) {
        try {
            if (group is null) throw new ArgumentNullException(nameof(group));

            // Extract the actual GUID from Parameters Service ID (similar to your FindParameter method)
            var parameterTypeId = apsParamModel.DownloadOptions.ParameterTypeId;
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
            var dataTypeId = new ForgeTypeId(apsParamModel.SpecId);
            var options = new ExternalDefinitionCreationOptions(apsParamModel.Name, dataTypeId) {
                GUID = guid, // Use the actual Parameters Service GUID
                Visible = apsParamModel.DownloadOptions.Visible,
                UserModifiable = !apsParamModel.ReadOnly,
                Description = apsParamModel.Description ?? ""
            };

            var externalDef = group.Definitions.Create(options) as ExternalDefinition;

            // Add parameter to family using FamilyManager
            var familyParam = fm.AddParameter(externalDef,
                apsParamModel.DownloadOptions.GroupTypeId,
                apsParamModel.DownloadOptions.IsInstance);

            // Find the SharedParameterElement that was created using the original Parameters Service ID
            var sharedParamElement = famDoc.FindParameter(parameterTypeId);
            if (sharedParamElement != null) return sharedParamElement;

            // If we can't find the SharedParameterElement, the FamilyParameter creation succeeded
            // which is what we care about for the family, so this isn't necessarily an error
            return new Exception($"Parameter {apsParamModel.Name} added to family but SharedParameterElement not found");
        } catch (Exception ex) {
            return ex;
        }
    }
}


