namespace AddinFamilyFoundrySuite.Core.Operations;

public class HydrateElectricalConnectorOperationTyped : Operation<NoSettings> {
    public override OperationType Type => OperationType.Doc;
    public override string Name => "Hydrate Electrical Connector";
    public override string Description => "Configure electrical connector parameters and associate them with family parameters";

    protected override void ExecuteCore(Document doc, NoSettings settings) =>
        doc.HydrateElectricalConnector();
}
