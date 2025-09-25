using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Families;
using PeRevit.Ui;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundryRemap : FamilyFoundryBase<SettingsRemap, ProfileRemap>, IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            // New fluent API - batches type operations into a single optimized loop
            var queue = this.EnqueueOperations(doc)
                .DocOperation(famDoc => {
                    var familyName = famDoc.OwnerFamily?.Name ?? "Unknown";
                    Debug.WriteLine($"\nProcessing family: {familyName}");
                    Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                    Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                })
                .DocOperation(famDoc => AddParams.ParamService(famDoc, this._apsParams, this._profile.ParamsAddPS.Filter))
                .DocOperation(this.HydrateElectricalConnector)
                .TypeOperation((famDoc) => this.RemapParameters(famDoc, this._profile.ParamsRemap.RemapData));

            this.ProcessQueue(queue);

            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), Log.ERR,
                    $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}")
                .Show();
            return Result.Cancelled;
        }
    }


    /// <summary>
    /// Per-type remap method for use with the new fluent API
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

                if (targetFamilyParam != null) {
                    // Associate the connector voltage parameter with the family parameter
                    doc.FamilyManager.AssociateElementParameterToFamilyParameter(voltageParam, targetFamilyParam);
                }
            }

            // Try to set apparent power parameter (5000VA)
            var powerParam = connectorElement.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD);
            if (powerParam != null && !powerParam.IsReadOnly) {
                var targetFamilyParam = doc.FamilyManager.Parameters
                    .Cast<FamilyParameter>()
                    .FirstOrDefault(fp => fp.Definition.Name == "PE_E___ApparentPower");

                if (targetFamilyParam != null) {
                    doc.FamilyManager.AssociateElementParameterToFamilyParameter(powerParam, targetFamilyParam);
                }
            }
        }

    }
}

public class SettingsRemap : BaseSettings<ProfileRemap> {
}

public class ProfileRemap : BaseProfileSettings {
    [Description("Parameters adding settings")]
    [Required]
    public ParamsAddPS ParamsAddPS { get; init; } = new();

    [Description("Parameters remap settings")]
    [Required]
    public ParamsRemap ParamsRemap { get; init; } = new();
}