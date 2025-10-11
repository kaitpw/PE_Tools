namespace AddinFamilyFoundrySuite.Core.Operations.Doc;

public static class HydrateElectricalConnectorOperation {
    public static void HydrateElectricalConnector(this Document doc) {
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