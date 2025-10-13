namespace AddinFamilyFoundrySuite.Core.Operations;

public class HydrateElectricalConnectorSettings {
    // Currently no configurable settings for this operation
}

public class HydrateElectricalConnectorOperationTyped : IOperation<HydrateElectricalConnectorSettings> {
    public HydrateElectricalConnectorSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Hydrate Electrical Connector";
    public string Description => "Configure electrical connector parameters and associate them with family parameters";

    public void Execute(Document doc) =>
        doc.HydrateElectricalConnector();
}