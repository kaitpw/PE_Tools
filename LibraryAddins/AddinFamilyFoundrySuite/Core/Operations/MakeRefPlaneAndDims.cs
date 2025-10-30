using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;


public class MakeRefPlaneAndDims : ICompoundOperation<MakeRefPlaneAndDimsSettings> {
    public MakeRefPlaneAndDims() {
        var sharedHelper = new SharedHelper();
        this.Operations = [
            new MakeRefPlanes(sharedHelper),
            new MakeDimensions(sharedHelper)
        ];
    }

    public List<IOperation<MakeRefPlaneAndDimsSettings>> Operations { get; set; }

    public OperationType Type => OperationType.Doc; // find a way to delete later
    public string Name { get; set; }
    public string Description => "Make reference planes and dimensions for the family";

    public OperationLog Execute(Document doc) => throw new NotImplementedException("Compound operations should not be executed directly");
}

public class SharedHelper {
    public PlaneQuery Query { get; set; }
    public RefPlaneAndDimHelper Helper { get; set; }
    public List<LogEntry> Logs { get; set; }
}


public class MakeRefPlaneAndDimsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
    [Required]
    public List<RefPlaneSpec> Specs { get; init; } = [];
}

public class MakeRefPlanes : IOperation<MakeRefPlaneAndDimsSettings> {
    private readonly SharedHelper _shared;
 
    public MakeRefPlanes(SharedHelper shared) => this._shared = shared;

    public MakeRefPlaneAndDimsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc; 
    public string Name { get; set; }
    public string Description => "Make reference planes for the family";

    public OperationLog Execute(Document doc) {
        this._shared.Logs = new List<LogEntry>();
        this._shared.Query = new PlaneQuery(doc);
        this._shared.Helper = new RefPlaneAndDimHelper(doc, this._shared.Query, this._shared.Logs);

        foreach (var spec in this.Settings.Specs) this._shared.Helper.CreatePlanes(spec);

        return new OperationLog(this.Name, this._shared.Logs);
    }
}

public class MakeDimensions : IOperation<MakeRefPlaneAndDimsSettings> {
    private readonly SharedHelper _shared;

    public MakeDimensions(SharedHelper shared) => this._shared = shared;

    public MakeRefPlaneAndDimsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name { get; set; }
    public string Description => "Make dimensions for the family";

    public OperationLog Execute(Document doc) {
        foreach (var spec in this.Settings.Specs) this._shared.Helper.CreateDimension(spec);

        return new OperationLog(this.Name, this._shared.Logs);
    }
}