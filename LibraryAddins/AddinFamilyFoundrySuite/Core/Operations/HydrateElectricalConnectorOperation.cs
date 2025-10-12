using Autodesk.Revit.DB.Electrical;

namespace AddinFamilyFoundrySuite.Core.Operations;

public static class HydrateElectricalConnectorOperation {
    public static void HydrateElectricalConnector(this Document doc) {
        var numberOfPoles = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .FirstOrDefault(fp => fp.Definition.Name == "PE_E___NumberOfPoles");
        _ = doc.FamilyManager.SetValueStrict(numberOfPoles, 2);

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

        if (!connectorElements.Any()) connectorElements.Add(MakeElectricalConnector(doc));

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

            var polesParam = connectorElement.get_Parameter(BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES);
            if (polesParam != null && !polesParam.IsReadOnly) {
                var targetFamilyParam = doc.FamilyManager.Parameters
                    .Cast<FamilyParameter>()
                    .FirstOrDefault(fp => fp.Definition.Name == "PE_E___NumberOfPoles");
                try {
                    if (targetFamilyParam != null)
                        doc.FamilyManager.AssociateElementParameterToFamilyParameter(polesParam, targetFamilyParam);
                } catch (Exception ex) {
                    Debug.WriteLine($"{polesParam.Definition.Name} can't be assigned to " +
                                    $"{targetFamilyParam?.Definition.Name} because {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    ///     Make an electrical connector on the family at the origin
    /// </summary>
    private static ConnectorElement MakeElectricalConnector(this Document doc) {
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