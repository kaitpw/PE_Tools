using PeExtensions.FamDocument;
using PeServices.Storage;
using System.ComponentModel.DataAnnotations;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
using AddinFamilyFoundrySuite.Core.Operations.Settings;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddApsParams : IOperation<AddApsParamsSettings> {
    public AddApsParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Add APS Parameters";
    public string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public void Execute(Document doc) =>
        _ = this.AddParams(doc);

    private List<Result<SharedParameterElement>> AddParams(
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
    [Required] public Include IncludeNames { get; init; } = new();
    [Required] public Exclude ExcludeNames { get; init; } = new();

    public bool Filter(ParamModelRes p) => this.IsIncluded(p) || !this.IsExcluded(p);

    private bool IsIncluded(ParamModelRes p) =>
        this.IncludeNames.Equaling.Any(p.Name.Equals) ||
        this.IncludeNames.Containing.Any(p.Name.Contains) ||
        this.IncludeNames.StartingWith.Any(p.Name.StartsWith);

    private bool IsExcluded(ParamModelRes p) =>
        this.ExcludeNames.Equaling.Any(p.Name.Equals) ||
        this.ExcludeNames.Containing.Any(p.Name.Contains) ||
        this.ExcludeNames.StartingWith.Any(p.Name.StartsWith);

    public static ParamModel GetAPSParams() {
        var apsParams = Storage.GlobalState("parameters-service-cache.json").Json<ParamModel>().Read();
        if (apsParams.Results != null) return apsParams;

        throw new InvalidOperationException(
            $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
            $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
    }
}
