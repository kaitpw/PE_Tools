using Autodesk.Revit.DB.Electrical;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.SetValue;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class HydrateElectricalConnector : IOperation<HydrateElectricalConnectorSettings> {
    public HydrateElectricalConnectorSettings Settings { get; set; } 
    public OperationType Type => OperationType.Doc;

    public string Description => "Configure electrical connector parameters and associate them with family parameters";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog(this.GetType().Name);

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
                    doc.FamilyManager.SetFormula(apparentPower, $"{voltageParamName} * {mcaParamName} * 0.8 * if({polesParamName} = 3, sqrt(3), 1)");
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
                log.Entries.Add(new LogEntry { Item = "Create connector" });
            }

            foreach (var (source, target, action) in mappings) {
                if (string.IsNullOrEmpty(source)) continue;

                try {
                    var sourceParam = doc.FamilyManager.Parameters
                                          .OfType<FamilyParameter>()
                                          .FirstOrDefault(fp => fp.Definition.Name == source);

                    if (sourceParam == null) {
                        log.Entries.Add(new LogEntry {
                            Item = $"Map {source}",
                            Error = "Parameter not found"
                        });
                        continue;
                    }

                    foreach (var connectorElement in connectorElements) {
                        var targetParam = connectorElement.get_Parameter(target);
                        if (targetParam != null) {
                            doc.FamilyManager.AssociateElementParameterToFamilyParameter(targetParam, sourceParam);
                        }
                    }

                    action?.Invoke(doc, sourceParam);
                    log.Entries.Add(new LogEntry { Item = $"Map {source}" });
                } catch (Exception ex) {
                    log.Entries.Add(new LogEntry {
                        Item = $"Map {source}",
                        Error = ex.Message
                    });
                }
            }
        } catch (Exception ex) {
            log.Entries.Add(new LogEntry {
                Item = "Hydrate connector",
                Error = ex.Message
            });
        }

        return log;
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
    public bool Enabled { get; init; } = true;
    [Required] public Parameters SourceParameterNames { get; init; } = new();

    public class Parameters {
        [Required] public string NumberOfPoles { get; init; } = "";
        [Required] public string ApparentPower { get; init; } = "";
        [Required] public string MinimumCurrentAmpacity { get; init; } = "";
        [Required] public string Voltage { get; init; } = "";
    }
}