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

    public OperationLog Execute(Document doc) {
        var log = new OperationLog();

        var fm = doc.FamilyManager;
        var filteredResults = this.Settings.Filter != null
            ? AddApsParamsSettings.GetAPSParams().Results.Where(this.Settings.Filter).ToList()
            : AddApsParamsSettings.GetAPSParams().Results;

        var defFile = MakeTempSharedParamTxt(doc);
        var group = defFile.Groups.get_Item("Parameters") ?? defFile.Groups.Create("Parameters");

        foreach (var psParamInfo in filteredResults) {
            if (psParamInfo.TypedMetadata.IsArchived) continue;

            try {
                var (sharedParam, sharedParamErr) = doc.AddApsParameter(fm, group, psParamInfo);
                if (sharedParamErr is not null) {
                    var (slowParam, slowErr) = doc.AddApsParameterSlow(psParamInfo);
                    if (slowErr != null) {
                        log.Entries.Add(new LogEntry {
                            Item = psParamInfo.Name,
                            Error = slowErr.Message
                        });
                    } else {
                        log.Entries.Add(new LogEntry { Item = slowParam.Name });
                    }
                } else {
                    log.Entries.Add(new LogEntry { Item = sharedParam.Name });
                }
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry {
                    Item = psParamInfo.Name,
                    Error = ex.Message
                });
            }
        }

        try {
            if (File.Exists(defFile.Filename)) File.Delete(defFile.Filename);
        } catch {
            Debug.WriteLine("Failed to delete temporary shared param file.");
        }

        return log;
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

public class AddApsParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
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
