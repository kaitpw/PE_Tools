using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PeServices.Storage;


namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundryRemap : FamilyFoundryBase<SettingsRemap, ProfileRemap>, IExternalCommand {
    protected ParametersApi.Parameters _apsParams;

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            this.Init(() => {
                // test if the cache exists, if not throw error to prompt user to run command to generate cache
                var tmpParams = this._settings.GetAPSParams();
                if (tmpParams.Results == null) {
                    throw new InvalidOperationException(
                        $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
                        $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
                }

                this._apsParams = tmpParams;
            });

            var queue = new OperationEnqueuer(doc)
                .DocOperation(famDoc => {
                    var familyName = famDoc.OwnerFamily?.Name ?? "Unknown";
                    Debug.WriteLine($"\nProcessing family: {familyName}");
                    Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                    Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                })
                .DocOperation(famDoc =>
                    AddParams.ParamService(famDoc, this._apsParams, this._profile.ParamsAddPS.Filter))
                .DocOperation(this.HydrateElectricalConnector)
                .TypeOperation(famDoc => this.RemapParameters(famDoc, this._profile.ParamsRemap.RemapData));

            this.ProcessQueue(queue);

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }


    /// <summary>
    ///     Per-type remap method for use with the new fluent API
    /// </summary>
    private void RemapParameters(Document famDoc, List<ParamsRemap.RemapDataRecord> paramRemaps) {
        foreach (var p in paramRemaps) {
            try {
                _ = famDoc.MapValue(p.CurrNameOrId, p.NewNameOrId, p.MappingPolicy);
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }
    }

    public void HydrateElectricalConnector(Document doc) {
        var apparentPower = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .FirstOrDefault(fp => fp.Definition.Name == "PE_E___ApparentPower");

        doc.FamilyManager.SetFormula(apparentPower, "PE_E___Voltage * PE_E___MCA");

        // Get all connector elements in the family
        var connectorElements = new FilteredElementCollector(doc)
            .OfClass(typeof(ConnectorElement))
            .Cast<ConnectorElement>()
            .Where(ce => ce.Domain == Domain.DomainElectrical)
            .ToList();

        if (!connectorElements.Any()) {
            Console.WriteLine("No electrical connector elements found in family");
            return;
        }

        foreach (var connectorElement in connectorElements) {
            var voltageParam = connectorElement.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE);
            if (voltageParam != null) {
                // Find the PE_E___Voltage family parameter
                var targetFamilyParam = doc.FamilyManager.Parameters
                    .Cast<FamilyParameter>()
                    .FirstOrDefault(fp => fp.Definition.Name == "PE_E___Voltage");
                try {
                    if (targetFamilyParam != null) {
                        // Associate the connector voltage parameter with the family parameter
                        doc.FamilyManager.AssociateElementParameterToFamilyParameter(voltageParam, targetFamilyParam);
                    }
                } catch (Exception ex) {
                    Debug.WriteLine($"{voltageParam.Definition.Name} can't be assigned to " +
                                    $"{targetFamilyParam?.Definition.Name} because {ex.Message}");
                }
            }

            // Try to set apparent power parameter (5000VA)
            var powerParam = connectorElement.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD);
            if (powerParam != null && !powerParam.IsReadOnly) {
                var targetFamilyParam = doc.FamilyManager.Parameters
                    .Cast<FamilyParameter>()
                    .FirstOrDefault(fp => fp.Definition.Name == "PE_E___ApparentPower");
                try {
                    if (targetFamilyParam != null)
                        doc.FamilyManager.AssociateElementParameterToFamilyParameter(powerParam, targetFamilyParam);
                } catch (Exception ex) {
                    Debug.WriteLine($"{powerParam.Definition.Name} can't be assigned to " +
                                    $"{targetFamilyParam?.Definition.Name} because {ex.Message}");
                }
            }
        }
    }
}

public class SettingsRemap : BaseSettings<ProfileRemap> {
    public ParametersApi.Parameters GetAPSParams() =>
        Storage.GlobalState("parameters-service-cache.json").Json<ParametersApi.Parameters>().Read();
}

public class ProfileRemap : BaseProfileSettings {
    [Description("Parameters adding settings")]
    [Required]
    public ParamsAddPS ParamsAddPS { get; init; } = new();

    [Description("Parameters remap settings")]
    [Required]
    public ParamsRemap ParamsRemap { get; init; } = new();
}