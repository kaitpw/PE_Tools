using AddinFamilyFoundrySuite.Core;
using System.Linq;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Placement { Positive, Mirror, Negative }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RpStrength {
    Left = 0,
    CenterLR = 1,
    Right = 2,
    Front = 3,
    CenterFB = 4,
    Back = 5,
    Bottom = 6,
    CenterElev = 7,
    Top = 8,
    NotARef = 12,
    StrongRef = 13,
    WeakRef = 14
}

public class RefPlaneSpec {
    public required string Name { get; set; }
    public required string AnchorName { get; set; }
    public Placement Placement { get; set; } = Placement.Mirror;
    public string Parameter { get; set; } = null;
    public RpStrength Strength { get; set; } = RpStrength.NotARef;
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
        ["Center (Left/Right)"] = 0,
        ["Center (Front/Back)"] = 0,
        ["Ref. Level"] = 0
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

    public static string GetPlaneName(RefPlaneSpec spec, XYZ normal, int side) =>
        (spec.Placement, side) switch {
            (Placement.Mirror, -1) => $"{spec.Name} ({GetOrientationLabel(normal, -1)})",
            (Placement.Mirror, 1) => $"{spec.Name} ({GetOrientationLabel(normal, 1)})",
            (Placement.Mirror, 0) => GetCenterPlaneName(normal),
            _ => spec.Name
        };

    public static string GetOrientationLabel(XYZ normal, double sign) =>
        Math.Abs(normal.X) == 1.0 ? sign < 0 ? "Left" : "Right" :
        Math.Abs(normal.Y) == 1.0 ? sign < 0 ? "Back" : "Front" :
        Math.Abs(normal.Z) == 1.0 ? sign < 0 ? "Bottom" : "Top" :
        throw new ArgumentException($"Invalid normal: ({normal.X:F3}, {normal.Y:F3}, {normal.Z:F3})");

    public static string GetCenterPlaneName(XYZ normal) =>
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

    public static RefPlaneSpec SerializeDimensionToSpec(Dimension dim, Document doc) {
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

        // Check if this is a mirror pattern (3 planes with center)
        if (refPlanes.Count == 3) {
            // Find center plane geometrically by finding which plane is between the other two
            var centerPlane = FindCenterPlaneGeometrically(refPlanes);
            if (centerPlane != null) {
                var sidePlanes = refPlanes.Where(p => p != centerPlane).ToList();
                var normal = centerPlane.Normal;

                // Determine which side plane is on which side geometrically
                var (side1, side2) = DetermineSidePlanes(sidePlanes[0], sidePlanes[1], normal);

                // Extract base name from dimension parameter or derive from plane names
                var baseName = GetBaseNameFromDimension(dim, side1, side2);

                return new RefPlaneSpec {
                    Name = baseName,
                    AnchorName = centerPlane.Name,
                    Placement = Placement.Mirror,
                    Parameter = GetDimensionParameter(dim),
                    Strength = GetStrength(side1)
                };
            }
        }

        // Check if this is a 2-plane dimension (positive/negative pattern)
        if (refPlanes.Count == 2) {
            var plane1 = refPlanes[0];
            var plane2 = refPlanes[1];

            var placement = DeterminePlacement(plane1, plane2);
            var anchor = placement == Placement.Positive ? plane1 : plane2;
            var target = placement == Placement.Positive ? plane2 : plane1;

            return new RefPlaneSpec {
                Name = target.Name,
                AnchorName = anchor.Name,
                Placement = placement,
                Parameter = GetDimensionParameter(dim),
                Strength = GetStrength(target)
            };
        }

        return null;
    }

    public static ReferencePlane FindCenterPlaneGeometrically(List<ReferencePlane> planes) {
        if (planes.Count != 3) return null;

        // All planes should have the same normal for a valid dimension
        var normal = planes[0].Normal;

        // Calculate midpoints and their positions along the normal
        var midpoints = planes.Select(p => (p, mid: (p.BubbleEnd + p.FreeEnd) * 0.5)).ToList();

        // Project midpoints onto a line along the normal (use first plane's midpoint as origin)
        var origin = midpoints[0].mid;
        var positions = midpoints.Select(m => (m.p, pos: (m.mid - origin).DotProduct(normal))).ToList();

        // Sort by position along normal
        positions.Sort((a, b) => a.pos.CompareTo(b.pos));

        // The middle plane is the center
        return positions[1].p;
    }

    private static (ReferencePlane negativeSide, ReferencePlane positiveSide) DetermineSidePlanes(
        ReferencePlane plane1, ReferencePlane plane2, XYZ normal) {
        var mid1 = (plane1.BubbleEnd + plane1.FreeEnd) * 0.5;
        var mid2 = (plane2.BubbleEnd + plane2.FreeEnd) * 0.5;

        // Determine which is negative/positive relative to their midpoint
        var midpoint = (mid1 + mid2) * 0.5;
        var pos1 = (mid1 - midpoint).DotProduct(normal);
        var pos2 = (mid2 - midpoint).DotProduct(normal);

        return pos1 < pos2 ? (plane1, plane2) : (plane2, plane1);
    }

    private static string GetBaseNameFromDimension(Dimension dim, ReferencePlane side1,
        ReferencePlane side2) {
        // Try to get base name from dimension parameter first
        var paramName = GetDimensionParameter(dim);
        if (!string.IsNullOrEmpty(paramName)) {
            // If parameter name looks like it has a suffix, try to extract base
            return paramName;
        }

        // Try to find common base name from side plane names
        var name1 = side1.Name;
        var name2 = side2.Name;

        // Find longest common prefix
        var commonPrefix = "";
        var minLength = Math.Min(name1.Length, name2.Length);
        for (var i = 0; i < minLength; i++) {
            if (name1[i] == name2[i]) {
                commonPrefix += name1[i];
            } else {
                break;
            }
        }

        // If we have a reasonable common prefix, use it
        if (commonPrefix.Length > 3) {
            return commonPrefix.Trim();
        }

        // Otherwise, use the shorter name (likely the base name)
        return name1.Length <= name2.Length ? name1 : name2;
    }

    public static string GetDimensionParameter(Dimension dim) {
        try {
            var label = dim.FamilyLabel;
            return label?.Definition?.Name;
        } catch {
            return null;
        }
    }

    public static RpStrength GetStrength(ReferencePlane rp) {
        try {
            var strength = rp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME).AsInteger();
            return (RpStrength)strength;
        } catch {
            return RpStrength.NotARef;
        }
    }

    private static Placement DeterminePlacement(ReferencePlane anchor, ReferencePlane target) {
        var anchorMid = (anchor.BubbleEnd + anchor.FreeEnd) * 0.5;
        var targetMid = (target.BubbleEnd + target.FreeEnd) * 0.5;
        var diff = targetMid - anchorMid;
        var dot = diff.DotProduct(anchor.Normal);

        return dot > 0 ? Placement.Positive : Placement.Negative;
    }

    public void CreatePlanes(RefPlaneSpec spec) {
        var anchor = this._query.Get(spec.AnchorName);
        if (anchor == null) {
            this._logs.Add(new LogEntry {
                Item = $"RefPlane: {spec.Name}",
                Error = $"Anchor plane '{spec.AnchorName}' not found"
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
                _ = rp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME).Set((int)spec.Strength);
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

            if (spec.Placement == Placement.Mirror) {
                var refArrayMirror = new ReferenceArray();
                refArrayMirror.Append(planes[0].GetReference());
                refArrayMirror.Append(planes[^1].GetReference());
                dim = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLine, refArrayMirror);

                var dimLineEq = CreateDimensionLine(planes[0], planes[^1], dimOffset - 0.5);
                var dimEq = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLineEq, refArray);
                dimEq.AreSegmentsEqual = true;

            } else
                dim = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLine, refArray);

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