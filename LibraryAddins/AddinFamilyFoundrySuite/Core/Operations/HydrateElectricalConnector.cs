using Autodesk.Revit.DB.Electrical;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class HydrateElectricalConnector : IOperation<HydrateElectricalConnectorSettings> {
    public HydrateElectricalConnectorSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name { get; set; }

    public string Description => "Configure electrical connector parameters and associate them with family parameters";

    public OperationLog Execute(Document doc) {
        var logs = new List<LogEntry>();

        var polesParamName = this.Settings.SourceParameterNames.NumberOfPoles;
        var appPowerParamName = this.Settings.SourceParameterNames.ApparentPower;
        var voltageParamName = this.Settings.SourceParameterNames.Voltage;
        var mcaParamName = this.Settings.SourceParameterNames.MinimumCurrentAmpacity;

        var mappings = new List<(string source, BuiltInParameter target, Action<Document, FamilyParameter> action)> {
            (
                polesParamName,
                BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES,
                (doc, numberOfPoles) => doc.FamilyManager.SetFormula(numberOfPoles, "2")
            ),
            (
                appPowerParamName,
                BuiltInParameter.RBS_ELEC_APPARENT_LOAD,
                (doc, apparentPower) => {
                    if (string.IsNullOrEmpty(voltageParamName) || string.IsNullOrEmpty(mcaParamName)) return;
                    doc.FamilyManager.SetFormula(apparentPower,
                        $"{voltageParamName} * {mcaParamName} * 0.8 * if({polesParamName} = 3, sqrt(3), 1)");
                }
            ),
            (
                voltageParamName,
                BuiltInParameter.RBS_ELEC_VOLTAGE,
                null)
        };

        try {
            var connectorElements = new FilteredElementCollector(doc)
                .OfClass(typeof(ConnectorElement))
                .Cast<ConnectorElement>()
                .Where(ce => ce.Domain == Domain.DomainElectrical)
                .ToList();

            if (!connectorElements.Any()) {
                connectorElements.Add(MakeElectricalConnector(doc));
                logs.Add(new LogEntry { Item = "Create connector" });
            }

            var targetMappings = mappings
                .Where(m => !string.IsNullOrEmpty(m.source))
                .ToDictionary(
                    m => m.target,
                    m => (
                        doc.FamilyManager.Parameters
                            .OfType<FamilyParameter>()
                            .FirstOrDefault(fp => fp.Definition.Name == m.source),
                        m.action
                    )
                );

            foreach (var connectorElement in connectorElements) {
                foreach (Parameter connectorParam in connectorElement.Parameters) {
                    try {
                        var bip = (BuiltInParameter)connectorParam.Id.Value;
                        var currentAssociation = doc.FamilyManager.GetAssociatedFamilyParameter(connectorParam);

                        if (targetMappings.TryGetValue(bip, out var mapping)) {
                            var (sourceParam, _) = mapping;
                            if (sourceParam == null) {
                                logs.Add(new LogEntry { Item = $"Map {bip}", Error = "Parameter not found" });
                                continue;
                            }

                            if (currentAssociation?.Id != sourceParam.Id) {
                                doc.FamilyManager.AssociateElementParameterToFamilyParameter(connectorParam,
                                    sourceParam);
                                logs.Add(new LogEntry { Item = $"Map {sourceParam.Definition.Name}" });
                            }
                        } else if (currentAssociation != null) {
                            doc.FamilyManager.AssociateElementParameterToFamilyParameter(connectorParam, null);
                            logs.Add(new LogEntry { Item = $"Disassociate {currentAssociation.Definition.Name}" });
                        }
                    } catch (Exception ex) {
                        logs.Add(
                            new LogEntry { Item = $"Process {connectorParam.Definition.Name}", Error = ex.Message });
                    }
                }
            }

            foreach (var kvp in targetMappings) {
                var (sourceParam, action) = kvp.Value;
                if (sourceParam == null) continue;

                try {
                    action?.Invoke(doc, sourceParam);
                } catch (Exception ex) {
                    logs.Add(new LogEntry { Item = $"Action for {sourceParam.Definition.Name}", Error = ex.Message });
                }
            }
        } catch (Exception ex) {
            logs.Add(new LogEntry { Item = "Hydrate connector", Error = ex.Message });
        }

        return new OperationLog(this.Name, logs);
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

public class HydrateElectricalConnectorSettings : IOperationSettings {
    [Required] public Parameters SourceParameterNames { get; init; } = new();
    public bool Enabled { get; init; } = true;

    public class Parameters {
        [Required] public string NumberOfPoles { get; init; } = "";
        [Required] public string ApparentPower { get; init; } = "";
        [Required] public string MinimumCurrentAmpacity { get; init; } = "";
        [Required] public string Voltage { get; init; } = "";
    }
}