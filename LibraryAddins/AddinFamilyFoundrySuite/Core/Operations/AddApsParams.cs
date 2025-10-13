using PeServices.Storage;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;
using System.ComponentModel.DataAnnotations;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddApsParams : IOperation<AddApsParamsSettings> {
    public AddApsParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Add APS Parameters";
    public string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public void Execute(Document doc) =>
        _ = this.AddParams(doc);

    public List<Result<SharedParameterElement>> AddParams(
       Document famDoc
   ) {
        if (famDoc is null) throw new ArgumentNullException(nameof(famDoc));
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var fm = famDoc.FamilyManager;

        var finalDownloadResults = new List<Result<SharedParameterElement>>();

        var filteredResults = this.Settings.Filter != null
            ? AddApsParamsSettings.GetAPSParams().Results.Where(this.Settings.Filter).ToList()
            : AddApsParamsSettings.GetAPSParams().Results;

        var defFile = MakeTempSharedParamTxt(famDoc);
        var group = defFile.Groups.get_Item("Parameters") ?? defFile.Groups.Create("Parameters");

        foreach (var psParamInfo in filteredResults) {
            if (psParamInfo.TypedMetadata.IsArchived) continue;

            var (sharedParam, sharedParamErr) = famDoc.AddApsParameter(fm, group, psParamInfo);
            if (sharedParamErr is not null) {
                finalDownloadResults.Add(famDoc.AddApsParameterSlow(psParamInfo));
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

    private static DefinitionFile MakeTempSharedParamTxt(Document famDoc) {
        var app = famDoc.Application;
        var tempSharedParamFile = Path.GetTempFileName() + ".txt";
        using (File.Create(tempSharedParamFile)) { } // Create empty file

        app.SharedParametersFilename = tempSharedParamFile;
        try {
            return app.OpenSharedParameterFile();
        } catch (Exception ex) {
            throw new Exception($"Failed to create temp shared parameter file: {ex.Message}");
        }
    }
}

public class AddApsParamsSettings {
    [Required] public List<string> IncludeNamesEqualing { get; init; } = [];
    [Required] public List<string> ExcludeNamesEqualing { get; init; } = [];
    [Required] public List<string> IncludeNamesContaining { get; init; } = [];
    [Required] public List<string> ExcludeNamesContaining { get; init; } = [];
    [Required] public List<string> IncludeNamesStartingWith { get; init; } = [];
    [Required] public List<string> ExcludeNamesStartingWith { get; init; } = [];

    public PsRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();

    public bool Filter(ParamModelRes p) =>
        Include(this.IncludeNamesEqualing, p.Name.Equals)
        && Exclude(this.ExcludeNamesEqualing, p.Name.Equals)
        && Include(this.IncludeNamesContaining, p.Name.Contains)
        && Exclude(this.ExcludeNamesContaining, p.Name.Contains)
        && Include(this.IncludeNamesStartingWith, p.Name.StartsWith)
        && Exclude(this.ExcludeNamesStartingWith, p.Name.StartsWith);

    private static bool Include<T>(List<T> list, Func<T, bool> predicate) =>
        list.Count == 0 || list.Any(predicate); // Pass if empty OR condition met

    private static bool Exclude<T>(List<T> list, Func<T, bool> predicate) =>
        list.Count == 0 || !list.Any(predicate); // Pass if empty OR condition NOT met

    public static ParamModel GetAPSParams() {
        var apsParams = Storage.GlobalState("parameters-service-cache.json").Json<ParamModel>().Read();
        if (apsParams.Results != null) return apsParams;

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }

    public class PsRecoverFromErrorSettings {
        public bool ReplaceParameterWithMatchingName { get; init; } = true;
    }
}

