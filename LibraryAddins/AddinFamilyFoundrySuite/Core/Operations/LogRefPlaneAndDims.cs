using System.Text.Json;
using System.Text.Json.Serialization;

namespace AddinFamilyFoundrySuite.Core.Operations;

// TODO: THIS IS NOT WORKING!!!
/// <summary>
///     Ad-hoc operation that logs existing reference planes and dimensions in a format
///     compatible with MakeRefPlaneAndDimsSettings for copying into profile JSON.
///     <para>
///         Usage example:
///         <code>
///     queue.Add(new LogRefPlaneAndDims(storage.Output().GetFolderPath()), new LogRefPlaneAndDimsSettings());
///     </code>
///     </para>
/// </summary>
public class LogRefPlaneAndDims : DocOperation<LogRefPlaneAndDimsSettings> {
    public LogRefPlaneAndDims(LogRefPlaneAndDimsSettings settings, string outputDir) : base(settings) =>
        this.OutputPath = outputDir;

    public string OutputPath { get; }

    public override string Description => "Log existing reference planes and dimensions in profile JSON format";

    public override OperationLog Execute(Document doc) {
        var specs = new List<RefPlaneSpec>();

        // Get all reference planes
        var refPlanes = new FilteredElementCollector(doc)
            .OfClass(typeof(ReferencePlane))
            .Cast<ReferencePlane>()
            .Where(rp => !string.IsNullOrEmpty(rp.Name))
            .ToList();

        // Get all dimensions
        var dimensions = new FilteredElementCollector(doc)
            .OfClass(typeof(Dimension))
            .Cast<Dimension>()
            .Where(d => d is not SpotDimension)
            .ToList();

        // Try to match dimensions to reference planes
        foreach (var dimension in dimensions) {
            var spec = this.TryCreateSpec(dimension, doc);
            if (spec != null)
                specs.Add(spec);
        }

        // Output JSON format
        var jsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var json = JsonSerializer.Serialize(specs, jsonOptions);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"ref-planes-dims_{timestamp}.json";
        var filePath = Path.Combine(this.OutputPath, filename);
        File.WriteAllText(filePath, json);

        var log = new LogEntry { Item = $"Wrote {specs.Count} reference plane specs to {filename}" };
        return new OperationLog(this.Name, [log]);
    }

    private RefPlaneSpec TryCreateSpec(Dimension dim, Document doc) {
        if (dim.References.Size < 2) return null;

        // Get the reference planes from the dimension
        var refPlanes = new List<ReferencePlane>();
        for (var i = 0; i < dim.References.Size; i++) {
            var reference = dim.References.get_Item(i);
            var elem = doc.GetElement(reference);
            if (elem is ReferencePlane rp && !string.IsNullOrEmpty(rp.Name))
                refPlanes.Add(rp);
        }

        if (refPlanes.Count < 2) return null;

        // Try to identify the pattern
        return this.IdentifySpec(refPlanes, dim);
    }

    private RefPlaneSpec IdentifySpec(List<ReferencePlane> refPlanes, Dimension dim) {
        // Check if this is a mirror pattern (3 planes with center)
        if (refPlanes.Count == 3) {
            var centerPlane = refPlanes[1];
            var leftPlane = refPlanes[0];
            var rightPlane = refPlanes[2];

            if (this.IsCenterPlane(centerPlane.Name) &&
                this.AreMirrorPair(leftPlane.Name, rightPlane.Name, centerPlane)) {
                var baseName = this.ExtractBaseName(leftPlane.Name);
                var parameter = this.GetDimensionParameter(dim);

                return new RefPlaneSpec {
                    Name = baseName,
                    AnchorName = centerPlane.Name,
                    Placement = Placement.Mirror,
                    HasEqualPair = dim.AreSegmentsEqual,
                    Parameter = parameter,
                    Strength = this.GetStrength(leftPlane)
                };
            }
        }

        // Check if this is a 2-plane mirror pattern (equal pair dimension)
        if (refPlanes.Count == 2) {
            var plane1 = refPlanes[0];
            var plane2 = refPlanes[1];

            // Try to detect if these are mirror pair planes by checking naming patterns
            var centerAnchor = this.FindCenterAnchor(plane1, plane2);
            if (centerAnchor != null) {
                var baseName = this.ExtractBaseName(plane1.Name);
                var parameter = this.GetDimensionParameter(dim);

                return new RefPlaneSpec {
                    Name = baseName,
                    AnchorName = centerAnchor,
                    Placement = Placement.Mirror,
                    HasEqualPair = true, // 2-plane dimensions that are mirror pairs are equal pair dimensions
                    Parameter = parameter,
                    Strength = this.GetStrength(plane1)
                };
            }

            // Otherwise, treat as positive/negative pattern
            var placement = this.DeterminePlacement(plane1, plane2);
            var parameter2 = this.GetDimensionParameter(dim);

            return new RefPlaneSpec {
                Name = plane2.Name,
                AnchorName = plane1.Name,
                Placement = placement,
                Parameter = parameter2,
                Strength = this.GetStrength(plane2)
            };
        }

        return null;
    }

    private bool IsCenterPlane(string name) =>
        name is "Center (Left/Right)" or "Center (Front/Back)" or "Ref. Level";

    private string FindCenterAnchor(ReferencePlane plane1, ReferencePlane plane2) {
        var name1 = plane1.Name.ToLower();
        var name2 = plane2.Name.ToLower();

        // Check if the names suggest mirror pairs (left/right or front/back or top/bottom)
        var isLeftRight = (name1.Contains("left") && name2.Contains("right")) ||
                          (name1.Contains("right") && name2.Contains("left"));
        var isFrontBack = (name1.Contains("front") && name2.Contains("back")) ||
                          (name1.Contains("back") && name2.Contains("front"));
        var isTopBottom = (name1.Contains("top") && name2.Contains("bottom")) ||
                          (name1.Contains("bottom") && name2.Contains("top"));

        if (isLeftRight) return "Center (Left/Right)";
        if (isFrontBack) return "Center (Front/Back)";
        if (isTopBottom) return "Ref. Level";

        return null;
    }

    private bool AreMirrorPair(string leftName, string rightName, ReferencePlane center) {
        var normal = center.Normal;

        // Check if names follow the pattern "Name (Left)" and "Name (Right)"
        var leftLabel = this.GetOrientationLabel(normal, -1);
        var rightLabel = this.GetOrientationLabel(normal, 1);

        if (leftName.EndsWith($"({leftLabel})") && rightName.EndsWith($"({rightLabel})")) {
            var leftBase = leftName[..leftName.LastIndexOf("(")].Trim();
            var rightBase = rightName[..rightName.LastIndexOf("(")].Trim();
            return leftBase == rightBase;
        }

        return false;
    }

    private string ExtractBaseName(string sideName) {
        // Extract base name from "Name (Side)" format
        var lastParen = sideName.LastIndexOf("(");
        if (lastParen > 0) return sideName[..lastParen].Trim();

        return sideName;
    }

    private string GetOrientationLabel(XYZ normal, double sign) =>
        Math.Abs(normal.X) == 1.0 ? sign < 0 ? "Left" : "Right" :
        Math.Abs(normal.Y) == 1.0 ? sign < 0 ? "Back" : "Front" :
        Math.Abs(normal.Z) == 1.0 ? sign < 0 ? "Bottom" : "Top" :
        "Unknown";

    private Placement DeterminePlacement(ReferencePlane anchor, ReferencePlane target) {
        var anchorMid = (anchor.BubbleEnd + anchor.FreeEnd) * 0.5;
        var targetMid = (target.BubbleEnd + target.FreeEnd) * 0.5;
        var diff = targetMid - anchorMid;
        var dot = diff.DotProduct(anchor.Normal);

        return dot > 0 ? Placement.Positive : Placement.Negative;
    }

    private string GetDimensionParameter(Dimension dim) {
        try {
            var label = dim.FamilyLabel;
            return label?.Definition?.Name;
        } catch {
            return null;
        }
    }

    private int GetStrength(ReferencePlane rp) {
        try {
            var param = rp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME);
            return param?.AsInteger() ?? 3;
        } catch {
            return 3;
        }
    }
}

public class LogRefPlaneAndDimsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}