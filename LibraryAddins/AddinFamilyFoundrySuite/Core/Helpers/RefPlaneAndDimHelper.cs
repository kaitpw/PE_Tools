using AddinFamilyFoundrySuite.Core;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Placement { Positive, Mirror, Negative }

public class RefPlaneSpec {
    public string Name { get; set; }
    public string AnchorName { get; set; }
    public Placement Placement { get; set; } = Placement.Mirror;
    public string Parameter { get; set; } = null;
    public bool IsEqual { get; set; } = false;
    public bool HasEqualPair { get; set; } = false;
    public int Strength { get; set; } = 3;
}

public class PlaneQuery {
    private readonly Dictionary<string, ReferencePlane> _cache = new();
    private readonly Document _doc;

    public PlaneQuery(Document doc) => this._doc = doc;

    public ReferencePlane Get(string name) {
        if (string.IsNullOrEmpty(name)) return null;
        if (!this._cache.ContainsKey(name)) {
            this._cache[name] = new FilteredElementCollector(this._doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>()
                .FirstOrDefault(rp => rp.Name == name);
        }

        return this._cache[name];
    }

    public ReferencePlane ReCache(string name) =>
        string.IsNullOrEmpty(name)
            ? null
            : this._cache[name] = new FilteredElementCollector(this._doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>()
                .FirstOrDefault(rp => rp.Name == name);
}

public class RefPlaneAndDimHelper {
    private readonly Dictionary<string, int> _depths = new() {
        ["Center (Left/Right)"] = 0, ["Center (Front/Back)"] = 0, ["Ref. Level"] = 0
    };

    private readonly Document _doc;
    private readonly List<LogEntry> _logs;
    private readonly Dictionary<string, (double plane, double dim)> _offsetCache = new();
    private readonly PlaneQuery _query;
    private readonly Dictionary<string, int> _siblingCounts = new();

    public RefPlaneAndDimHelper(Document doc, PlaneQuery query, List<LogEntry> logs) {
        this._doc = doc;
        this._query = query;
        this._logs = logs;
    }

    private static string GetPlaneName(RefPlaneSpec spec, XYZ normal, int side) =>
        (spec.Placement, side) switch {
            (Placement.Mirror, -1) => $"{spec.Name} ({GetOrientationLabel(normal, -1)})",
            (Placement.Mirror, 1) => $"{spec.Name} ({GetOrientationLabel(normal, 1)})",
            (Placement.Mirror, 0) => GetCenterPlaneName(normal),
            _ => spec.Name
        };

    private static string GetOrientationLabel(XYZ normal, double sign) =>
        Math.Abs(normal.X) == 1.0 ? sign < 0 ? "Left" : "Right" :
        Math.Abs(normal.Y) == 1.0 ? sign < 0 ? "Back" : "Front" :
        Math.Abs(normal.Z) == 1.0 ? sign < 0 ? "Bottom" : "Top" :
        throw new ArgumentException($"Invalid normal: ({normal.X:F3}, {normal.Y:F3}, {normal.Z:F3})");

    private static string GetCenterPlaneName(XYZ normal) =>
        Math.Abs(normal.X) == 1.0 ? "Center (Left/Right)" :
        Math.Abs(normal.Y) == 1.0 ? "Center (Front/Back)" :
        Math.Abs(normal.Z) == 1.0 ? "Ref. Level" :
        throw new ArgumentException("Invalid normal, only X/Y/Z supported");

    private (double plane, double dim) GetOffsets(RefPlaneSpec spec) {
        if (this._offsetCache.TryGetValue(spec.Name, out var cached)) return cached;

        var siblingIndex = this._siblingCounts.GetValueOrDefault(spec.AnchorName, 0);
        var depth = this._depths.GetValueOrDefault(spec.AnchorName, 0) + 1;

        this._siblingCounts[spec.AnchorName] = siblingIndex + 1;
        this._depths[spec.Name] = depth;

        var planeOffset = 0.5 + (siblingIndex * 2);
        var dimOffset = spec.Placement == Placement.Mirror ? siblingIndex : depth * 0.5;
        var offsets = (planeOffset, dimOffset);
        this._offsetCache[spec.Name] = offsets;
        return offsets;
    }

    public void CreatePlanes(RefPlaneSpec spec) {
        var anchor = this._query.Get(spec.AnchorName);
        if (anchor == null) {
            this._logs.Add(new LogEntry {
                Item = $"RefPlane: {spec.Name}", Error = $"Anchor plane '{spec.AnchorName}' not found"
            });
            return;
        }

        var (planeOffset, _) = this.GetOffsets(spec);
        var extent = 8.0;
        var midpoint = (anchor.BubbleEnd + anchor.FreeEnd) * 0.5;
        var normal = anchor.Normal;
        var direction = anchor.Direction;
        var cutVec = normal.CrossProduct(direction);
        var t = direction * extent;

        var planesToCreate = spec.Placement switch {
            Placement.Mirror => new[] {
                (GetPlaneName(spec, normal, -1), midpoint - (normal * planeOffset)),
                (GetPlaneName(spec, normal, 1), midpoint + (normal * planeOffset))
            },
            Placement.Positive => new[] { (spec.Name, midpoint + (normal * planeOffset)) },
            Placement.Negative => new[] { (spec.Name, midpoint - (normal * planeOffset)) },
            _ => throw new ArgumentException($"Unknown placement: {spec.Placement}")
        };

        foreach (var (name, origin) in planesToCreate) {
            try {
                if (this._query.Get(name) != null) continue;
                var rp = this._doc.FamilyCreate.NewReferencePlane(origin + t, origin - t, cutVec, this._doc.ActiveView);
                rp.Name = name;
                _ = rp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME).Set(spec.Strength);
                _ = this._query.ReCache(name);
                this._logs.Add(new LogEntry { Item = $"RefPlane: {name}" });
            } catch (Exception ex) {
                this._logs.Add(new LogEntry { Item = $"RefPlane: {name}", Error = ex.Message });
            }
        }
    }

    public void CreateDimension(RefPlaneSpec spec) {
        var anchor = this._query.Get(spec.AnchorName);
        if (anchor == null) return;

        var (_, dimOffset) = this.GetOffsets(spec);
        var planes = (spec.Placement switch {
            Placement.Mirror => new[] { -1, 0, 1 }.Select(i => this._query.Get(GetPlaneName(spec, anchor.Normal, i))),
            Placement.Positive => new[] { this._query.Get(spec.AnchorName), this._query.Get(spec.Name) },
            Placement.Negative => new[] { this._query.Get(spec.Name), this._query.Get(spec.AnchorName) },
            _ => throw new ArgumentException($"Unknown placement: {spec.Placement}")
        }).Where(p => p != null).ToArray();

        if (planes.Length < 2) {
            this._logs.Add(new LogEntry { Item = $"Dimension: {spec.Name}", Error = "Reference planes not found" });
            return;
        }

        try {
            var refArray = new ReferenceArray();
            foreach (var plane in planes) refArray.Append(plane.GetReference());

            var dimLine = CreateDimensionLine(planes[0], planes[^1], dimOffset);
            Dimension dim;

            if (spec.Placement == Placement.Mirror && spec.HasEqualPair) {
                var refArrayMirror = new ReferenceArray();
                refArrayMirror.Append(planes[0].GetReference());
                refArrayMirror.Append(planes[^1].GetReference());
                dim = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLine, refArrayMirror);

                var dimLineEq = CreateDimensionLine(planes[0], planes[^1], dimOffset - 0.5);
                var dimEq = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLineEq, refArray);
                dimEq.AreSegmentsEqual = true;
            } else
                dim = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLine, refArray);

            if (spec.IsEqual) {
                if (dim.References.Size != 3)
                    throw new Exception($"Equal dimension requires 3 reference planes. Spec: {spec.Name}");
                dim.AreSegmentsEqual = true;
            }

            if (!string.IsNullOrEmpty(spec.Parameter))
                dim.FamilyLabel = this._doc.FamilyManager.get_Parameter(spec.Parameter);

            this._logs.Add(new LogEntry { Item = $"Dimension: {spec.Name}" });
        } catch (Exception ex) {
            this._logs.Add(new LogEntry { Item = $"Dimension: {spec.Name}", Error = ex.Message });
        }
    }

    private static Line CreateDimensionLine(ReferencePlane rp1, ReferencePlane rp2, double offset) {
        var normal = rp1.Normal;
        var direction = rp1.Direction;
        var distanceAlongNormal =
            (((rp2.BubbleEnd + rp2.FreeEnd) * 0.5) - ((rp1.BubbleEnd + rp1.FreeEnd) * 0.5)).DotProduct(normal);

        var p1 = rp1.BubbleEnd + (direction * offset);
        var p2 = p1 + (normal * distanceAlongNormal);

        return Line.CreateBound(p1, p2);
    }
}