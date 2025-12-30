using Autodesk.Revit.DB.Electrical;
using Nice3point.Revit.Extensions;
using PeExtensions.FamDocument;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MakeElecConnector(MakeElecConnectorSettings settings) : DocOperation<MakeElecConnectorSettings>(settings) {
    /// <summary>Attempting to associate these will throw a "This parameter cannot be associated" exception.</summary>
    public List<string> UnassociableConnParams =
        ["Category", "System Type", "Power Factor State", "Design Option", "Family Name", "Type Name"];

    public override string Description =>
        "Configure electrical connector parameters and associate them with family parameters";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        var logs = new List<LogEntry>();

        var polesName = this.Settings.SourceParameterNames.NumberOfPoles;
        var appPowerParamName = this.Settings.SourceParameterNames.ApparentPower;
        var voltageName = this.Settings.SourceParameterNames.Voltage;
        var mcaName = this.Settings.SourceParameterNames.MinimumCurrentAmpacity;

        FamilyParameter GetSourceParameter(string name) {
            return doc.FamilyManager.Parameters
                .OfType<FamilyParameter>()
                .FirstOrDefault(fp => fp.Definition.Name == name);
        }

        // TODO: Figure out PE_E___LoadClassification migration!!!!!!!!!
        // Note: Load Classification (RBS_ELEC_LOAD_CLASSIFICATION) is intentionally NOT mapped here.
        // Load Classification is a Reference type (SpecTypeId.Reference.LoadClassification) that requires
        // an ElementId pointing to an ElectricalLoadClassification element. These elements only exist
        // in project documents, not family documents, so we cannot set a default value in the family.
        // Load Classification must be set at the project level when family instances are placed.
        var targetMappings =
            new Dictionary<BuiltInParameter, FamilyParameter> {
                { BuiltInParameter.RBS_ELEC_VOLTAGE, GetSourceParameter(voltageName) },
                { BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES, GetSourceParameter(polesName) },
                { BuiltInParameter.RBS_ELEC_APPARENT_LOAD, GetSourceParameter(appPowerParamName) }
            };

        var connectorElements = new FilteredElementCollector(doc)
            .OfClass(typeof(ConnectorElement))
            .Cast<ConnectorElement>()
            .Where(ce => ce.Domain == Domain.DomainElectrical)
            .ToList();

        if (!connectorElements.Any()) {
            connectorElements.Add(MakeElectricalConnector(doc));
            logs.Add(new LogEntry("Create connector").Success());
        }

        foreach (var connectorElement in connectorElements)
            logs.AddRange(this.HandleConnectorParameters(doc, connectorElement.Parameters, targetMappings));

        return new OperationLog(this.Name, logs);
    }

    private List<LogEntry> HandleConnectorParameters(FamilyDocument doc,
        ParameterSet connectorParameters,
        Dictionary<BuiltInParameter, FamilyParameter> targetMappings
    ) {
        List<LogEntry> logs = [];
        foreach (Parameter targetParam in connectorParameters) {
            if (this.UnassociableConnParams.Contains(targetParam.Definition.Name)) continue;
            try {
                var bip = targetParam.Definition.Cast<InternalDefinition>().BuiltInParameter;
                var tgtAssociations = doc.FamilyManager.GetAssociatedFamilyParameter(targetParam);
                _ = targetMappings.TryGetValue(bip, out var sourceParam);
                if (sourceParam == null || tgtAssociations?.Id == sourceParam.Id) continue;

                // Dissociate everything and it explicitly
                if (tgtAssociations != null) {
                    logs.Add(new LogEntry($"Unassociate {tgtAssociations.Definition.Name}").Success());
                    doc.FamilyManager.AssociateElementParameterToFamilyParameter(targetParam, null);
                }

                // Associate only if we can
                if (targetParam.Definition.GetDataType() == sourceParam.Definition.GetDataType()) {
                    logs.Add(new LogEntry($"Associate {sourceParam.Definition.Name}").Success());
                    doc.FamilyManager.AssociateElementParameterToFamilyParameter(targetParam, sourceParam);
                }
            } catch (Exception ex) {
                logs.Add(new LogEntry(targetParam.Definition.Name).Error(ex));
            }
        }

        return logs;
    }

    /// <summary>
    ///     Make an electrical connector on the family at the origin
    /// </summary>
    private static ConnectorElement MakeElectricalConnector(FamilyDocument doc) {
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

public class MakeElecConnectorSettings : IOperationSettings {
    [Required] public Parameters SourceParameterNames { get; init; } = new();
    public bool Enabled { get; init; } = true;

    public class Parameters {
        [Required] public string NumberOfPoles { get; init; } = "PE_E___NumberOfPoles";
        [Required] public string ApparentPower { get; init; } = "PE_E___ApparentPower";
        [Required] public string MinimumCurrentAmpacity { get; init; } = "PE_E___MCA";
        [Required] public string Voltage { get; init; } = "PE_E___Voltage";
    }
}