using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamParameter;
using PeExtensions.FamParameter.Formula;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class PurgeParams : DocOperation<PurgeParamsSettings> {
    public PurgeParams(PurgeParamsSettings settings, IEnumerable<string> ExcludeNamesEqualing) :
        base(settings) =>
        this.ExternalExcludeNamesEqualing = ExcludeNamesEqualing;

    public override string Description => "Recursively delete unused parameters from the family";

    public IEnumerable<string> ExternalExcludeNamesEqualing { get; set; } = [];

    public bool IsParameterEmpty(FamilyParameter param, FamilyProcessingContext processingContext) {
        if (processingContext == null) return false;

        foreach (var value in processingContext.GetTypesWithValue(param.Definition.Name)) {
            if (value == null) return true;
            if (this.Settings.ConsiderZeroValueAsEmpty
                && int.TryParse(value, out var intValue)
                && intValue == 0) return true;
            if (this.Settings.ConsiderEmptyStringAsEmpty
                && string.IsNullOrWhiteSpace(value)) return true;
        }

        return false;
    }

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        var logs = new List<LogEntry>();
        this.RecursiveDelete(doc, logs, processingContext);
        return new OperationLog(this.Name, logs);
    }

    private void RecursiveDelete(FamilyDocument doc, List<LogEntry> logs, FamilyProcessingContext processingContext) {
        var deleteCount = 0;
        var excludeSet = this.ExternalExcludeNamesEqualing.ToHashSet();

        var allParams = doc.FamilyManager.Parameters;

        var parameters = allParams
            .OfType<FamilyParameter>()
            .Where(p => !excludeSet.Contains(p.Definition.Name))
            .Where(this.Settings.Filter)
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .Where(p => !this.IsParameterEmpty(p, processingContext))
            .OrderByDescending(p => p.Formula?.Length ?? 0)
            .ToList();

        foreach (var param in parameters) {
            if (!this.Settings.DirectDeleteEmptyParameters) {
                if (param.GetDependents(allParams).Any(p => p.HasDirectAssociation(doc))) continue;
                if (param.HasDirectAssociation(doc)) continue;
            }

            var log = new LogEntry(param.Definition.Name);
            try {
                doc.FamilyManager.RemoveParameter(param);
                _ = log.Success("Deleted");
                deleteCount++;
            } catch (Exception ex) {
                _ = log.Error(ex);
            }

            logs.Add(log);
        }

        if (deleteCount > 0) this.RecursiveDelete(doc, logs, processingContext);
    }
}

public class PurgeParamsSettings : IOperationSettings {
    [Description(
        "Whether to delete parameters that have no value for every family type, regardless of whether they are used in the family. This is rare but possible. This setting is useful for properties like url variations where there are often multiple url parameters with no value.")]
    public bool DirectDeleteEmptyParameters { get; init; } = true;

    [Description("Whether to consider zero value as \"empty\" when deleting empty parameters.")]
    public bool ConsiderZeroValueAsEmpty { get; init; } = true;

    [Description("Whether to consider empty string as \"empty\" when deleting empty parameters.")]
    public bool ConsiderEmptyStringAsEmpty { get; init; } = true;

    [Description(
        "Exclude parameters from the deletion list. Parameters matching any exclude filter (Equaling, Containing, or StartingWith) will be protected from deletion.")]
    [Required]
    public ExcludeSharedParameter ExcludeNames { get; init; } = new();

    public bool Enabled { get; init; } = true;

    public bool Filter(FamilyParameter p) => !this.IsExcluded(p);

    private bool IsExcluded(FamilyParameter p) =>
        this.ExcludeNames.Equaling.Any(p.Definition.Name.Equals) ||
        this.ExcludeNames.Containing.Any(p.Definition.Name.Contains) ||
        this.ExcludeNames.StartingWith.Any(p.Definition.Name.StartsWith);
}