using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

// TODO: this still needs alot of work!!!
public class DeleteUnusedReferencePlanes : DocOperation<DeleteUnusedReferencePlanesSettings> {
    public override string Description => "Deletes reference planes in the Family which are not used by anything important";

    public override OperationLog Execute(Document doc) {
        var logs = new List<LogEntry>();
        this.RecursiveDeleteUnusedReferencePlanes(doc, logs);
        return new OperationLog(this.Name, logs);
    }

    private void RecursiveDeleteUnusedReferencePlanes(Document doc, List<LogEntry> logs) {
        var deleteCount = 0;

        var referencePlanes = new FilteredElementCollector(doc)
            .OfClass(typeof(ReferencePlane))
            .Cast<ReferencePlane>()
            .ToList();

        foreach (var refPlane in referencePlanes) {
            var planeName = refPlane.Name ?? $"RefPlane_{refPlane.Id}";

            if (this.IsImportantPlane(refPlane)) continue;
            if (this.GetSketchedCurves(doc, refPlane).Count != 0) continue;
            if (this.Settings.SafeDelete && this.GetDependentElements(doc, refPlane).Count != 0) continue;

            try {
                _ = doc.Delete(refPlane.Id);
                logs.Add(new LogEntry { Item = planeName });
                deleteCount++;
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = planeName, Error = ex.Message });
            }
        }

        if (deleteCount > 0) this.RecursiveDeleteUnusedReferencePlanes(doc, logs);
    }


    private bool IsImportantPlane(ReferencePlane refPlane) =>
        refPlane.Pinned || refPlane.GetOrderedParameters()
            .Where(p => p.Definition.Name.Equals("Is Reference"))
            .Any(p => !new[] { "Not a Reference", "Weak Reference" }.Contains(p.AsValueString()));


    private List<Element> GetDependentElements(Document doc, ReferencePlane refPlane) {
        var dependentElements = refPlane.GetDependentElements(null)?
            .Where(id => id != refPlane.Id);

        // Apply dimension filters when safe mode is enabled
        if (dependentElements != null) {
            dependentElements = dependentElements.Where(id => {
                var element = doc.GetElement(id);
                if (element is not Dimension dimension) return true;

                return !this.DimensionIsDeletable(dimension);
            });
        }

        if (dependentElements?.Any() == true) return [.. dependentElements.Select(doc.GetElement)];
        return [];
    }

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
    [Description(
        "If false, the check for unusedness is relaxed: unused means that an RP does not have a dimension with a parameter associated to it.")]
    [Required]
    public bool SafeDelete { get; init; } = false;

    public bool Enabled { get; init; } = true;
}