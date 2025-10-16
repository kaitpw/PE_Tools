using System.ComponentModel;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedReferencePlanes : IOperation<DeleteUnusedReferencePlanesSettings> {
    public DeleteUnusedReferencePlanesSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Delete Unused Reference Planes";
    public string Description => "Deletes reference planes in the Family which are not used by anything important";

    public OperationLog Execute(Document doc, FamilyType typeContext = null) {
        var log = new OperationLog { OperationName = nameof(DeleteUnusedReferencePlanes) };
        this.RecursiveDeleteUnusedReferencePlanes(doc, log);
        return log;
    }

    private void RecursiveDeleteUnusedReferencePlanes(Document doc, OperationLog log) {
        var deleteCount = 0;

        var referencePlanes = new FilteredElementCollector(doc)
            .OfClass(typeof(ReferencePlane))
            .Cast<ReferencePlane>()
            .ToList();

        foreach (var refPlane in referencePlanes) {
            var planeName = refPlane.Name ?? $"RefPlane_{refPlane.Id}";

            if (this.IsImportantPlane(refPlane)) continue;
            if (this.GetDependentElements(doc, refPlane, this.Settings.SafeDelete).Count != 0) continue;
            if (this.GetSketchedCurves(doc, refPlane).Count != 0) continue;

            try {
                _ = doc.Delete(refPlane.Id);
                log.Entries.Add(new LogEntry { Item = planeName });
                deleteCount++;
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = planeName, Error = ex.Message });
            }
        }

        if (deleteCount > 0) {
            this.RecursiveDeleteUnusedReferencePlanes(doc, log);
        }
    }


    private bool IsImportantPlane(ReferencePlane refPlane) =>
        refPlane.Pinned || refPlane.GetOrderedParameters()
            .Where(p => p.Definition.Name.Equals("Is Reference"))
            .Any(p => !new[] { "Not a Reference", "Weak Reference" }.Contains(p.AsValueString()));


    private List<Element> GetDependentElements(Document doc, ReferencePlane refPlane, bool safe = false) =>
        refPlane.GetDependentElements(null)?
            .Where(id => id != refPlane.Id)
            .Select(doc.GetElement)
            .Where(e => {
                if (safe) return true;
                if (e is not Dimension dimension) return true;
                if (this.DimensionIsDeletable(dimension)) return false;
                return true;
            }).ToList() ?? [];

    private bool DimensionIsDeletable(Dimension dimension) {
        try {
            return dimension.FamilyLabel != null && !dimension.AreSegmentsEqual;
        } catch {
            return false;
        }
    }

    private List<CurveElement> GetSketchedCurves(Document doc, ReferencePlane refPlane) {
        var planeOrigin = refPlane.GetPlane().Origin;
        var planeNormal = refPlane.Normal;

        return new FilteredElementCollector(doc)
            .OfClass(typeof(CurveElement))
            .Cast<CurveElement>()
            .Where(ce => ce.SketchPlane != null)
            .Where(ce => {
                try {
                    var sketchPlane = ce.SketchPlane?.GetPlane();
                    return sketchPlane?.Normal.IsAlmostEqualTo(planeNormal) == true &&
                           sketchPlane.Origin.IsAlmostEqualTo(planeOrigin, 0.01);
                } catch {
                    return false;
                }
            })
            .ToList();
    }
}

public class DeleteUnusedReferencePlanesSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
    [Description("If false, the check for unusedness is relaxed: unused means that an RP does not have a dimension with a parameter associated to it.")]
    public bool SafeDelete { get; init; } = false;
}