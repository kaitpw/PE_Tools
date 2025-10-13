using Autodesk.Revit.DB.Electrical;
using System.ComponentModel.DataAnnotations;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class HydrateElectricalConnector : IOperation<HydrateElectricalConnectorSettings> {
    public HydrateElectricalConnectorSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Hydrate Electrical Connector";
    public string Description => "Configure electrical connector parameters and associate them with family parameters";

    public void Execute(Document doc) {
        var polesParamName = this.Settings.SourceParameterNames.NumberOfPoles;
        var appPowerParamName = this.Settings.SourceParameterNames.ApparentPower;
        var voltageParamName = this.Settings.SourceParameterNames.Voltage;
        var mcaParamName = this.Settings.SourceParameterNames.MinimumCurrentAmpacity;

        var mappings = new List<(string source, BuiltInParameter target, Action<Document, FamilyParameter> action)> {
            (
                polesParamName,
                BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES,
                (doc, numberOfPoles) => doc.FamilyManager.SetValueStrict(numberOfPoles, 2)
            ), (
                appPowerParamName,
                BuiltInParameter.RBS_ELEC_APPARENT_LOAD,
                (doc, apparentPower) => {
                    if (string.IsNullOrEmpty(voltageParamName) || string.IsNullOrEmpty(mcaParamName)) return;
                    doc.FamilyManager.SetFormula(apparentPower, $"{voltageParamName} * {mcaParamName}");
                    }
            ), (
                voltageParamName,
                BuiltInParameter.RBS_ELEC_VOLTAGE,
                null)
        };

        // Get all connector elements in the family
        var connectorElements = new FilteredElementCollector(doc)
            .OfClass(typeof(ConnectorElement))
            .Cast<ConnectorElement>()
            .Where(ce => ce.Domain == Domain.DomainElectrical)
            .ToList();

        if (!connectorElements.Any()) connectorElements.Add(MakeElectricalConnector(doc));

        foreach (var (source, target, action) in mappings) {
            if (string.IsNullOrEmpty(source)) continue;
            var sourceParam = doc.FamilyManager.Parameters
                .OfType<FamilyParameter>()
                .FirstOrDefault(fp => fp.Definition.Name == source)
                ?? throw new Exception($"Parameter {source} not found");

            foreach (var connectorElement in connectorElements) {
                var targetParam = connectorElement.get_Parameter(target);
                try {
                    if (source != null && targetParam != null)
                        doc.FamilyManager.AssociateElementParameterToFamilyParameter(targetParam, sourceParam);
                } catch (Exception ex) {
                    Debug.WriteLine($"{targetParam.Definition.Name} can't be assigned to " +
                                    $"{sourceParam?.Definition.Name} because {ex.Message}");
                }
            }
            action?.Invoke(doc, sourceParam);
        }
    }

    /// <summary>
    ///     Make an electrical connector on the family at the origin
    /// </summary>
    private static ConnectorElement MakeElectricalConnector(Document doc) {
        var referenceCollector = new FilteredElementCollector(doc)
            .OfClass(typeof(ReferencePlane))
            .Cast<ReferencePlane>()
            .FirstOrDefault(rp => rp.Name is "Center (Left/Right)" or "CenterLR");

        Reference faceReference = null;

        faceReference = new Reference(referenceCollector);

        if (faceReference == null) {
            throw new InvalidOperationException(
                "Could not find a suitable planar face or reference plane to place the electrical connector on.");
        }

        try {
            // Create the electrical connector using PowerCircuit system type
            return ConnectorElement.CreateElectricalConnector(
                doc,
                ElectricalSystemType.PowerBalanced,
                faceReference);
        } catch (Exception ex) {
            throw new InvalidOperationException($"Failed to create electrical connector: {ex.Message}", ex);
        }
    }
}

public class HydrateElectricalConnectorSettings {
    // Currently no configurable settings for this operation
    // [Required] public string TargetParameters = "Center (Left/Right)"; // do this later
    [Required] public Parameters SourceParameterNames = new();

    public class Parameters {
        [Required] public string NumberOfPoles { get; set; } = "";
        [Required] public string ApparentPower { get; set; } = "";
        [Required] public string MinimumCurrentAmpacity { get; set; } = "";
        [Required] public string Voltage { get; set; } = "";
    }
}