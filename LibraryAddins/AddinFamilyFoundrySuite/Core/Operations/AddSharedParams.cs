using AddinFamilyFoundrySuite.Core;
using PeExtensions.FamDocument;
using PeServices.Storage;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddSharedParams(
    IEnumerable<SharedParameterDefinition> sharedParams
) : DocOperation<DefaultOperationSettings>(new DefaultOperationSettings()) {
    private IEnumerable<SharedParameterDefinition> SharedParams {
        get;
    } = sharedParams;

    public override string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public override OperationLog Execute(FamilyDocument doc, FamilyProcessingContext processingContext, OperationContext groupContext) {
        var logs = new List<LogEntry>();

        // Create diagnostic logger
        var outputDir = new Storage("FF Migrator").OutputDir().DirectoryPath;
        var familyName = doc.Document.Title;
        using var diagnosticLogger = new DiagnosticLogger(outputDir, familyName);

        diagnosticLogger.LogSection("AddSharedParams.Execute Starting");
        diagnosticLogger.Log($"Family: {familyName}");
        diagnosticLogger.Log($"Total parameters to add: {this.SharedParams.Count()}");

        // Log SharedParametersFile state at execution time
        diagnosticLogger.LogSharedParamFileState(doc.Document.Application, "Before adding parameters");

        // Get family category for logging
        var familyCategory = doc.OwnerFamily?.FamilyCategory?.Name ?? "(unknown)";
        diagnosticLogger.Log($"Family Category: {familyCategory}");

        foreach (var sharedParam in this.SharedParams) {
            var name = sharedParam.ExternalDefinition.Name;
            var guid = Guid.Empty;
            var specTypeId = "(unknown)";
            var groupTypeId = sharedParam.GroupTypeId?.TypeId ?? "(unknown)";

            try {
                guid = sharedParam.ExternalDefinition.GUID;
                specTypeId = sharedParam.ExternalDefinition.GetDataType()?.TypeId ?? "(unknown)";
            } catch (Exception ex) {
                diagnosticLogger.Log($"WARNING: Could not read GUID or SpecTypeId for {name}: {ex.Message}");
            }

            diagnosticLogger.LogParameterAttempt(name, guid, specTypeId, groupTypeId, sharedParam.IsInstance, familyCategory);
            diagnosticLogger.LogExternalDefinitionState(sharedParam.ExternalDefinition);

            try {
                var addedParam = doc.AddSharedParameter(sharedParam);
                diagnosticLogger.Log($"SUCCESS: Parameter '{addedParam.Definition.Name}' added successfully");
                logs.Add(new LogEntry(addedParam.Definition.Name).Success("Added"));
            } catch (Exception ex) {
                diagnosticLogger.LogException($"Adding parameter '{name}'", ex);
                logs.Add(new LogEntry(name).Error(ex));
            }
        }

        diagnosticLogger.LogSection("AddSharedParams.Execute Completed");
        diagnosticLogger.Log($"Successful: {logs.Count(l => l.Status == LogStatus.Success)}");
        diagnosticLogger.Log($"Failed: {logs.Count(l => l.Status == LogStatus.Error)}");

        return new OperationLog(this.Name, logs);
    }
}