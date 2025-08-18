namespace PE_Lib;

public class Faces {
    public static UV GetCenter(Face face) {
        var bbox = face.GetBoundingBox();
        return new UV((bbox.Min.U + bbox.Max.U) / 2.0, (bbox.Min.V + bbox.Max.V) / 2.0);
    }

    public static double GetMinSizePlanar(PlanarFace face) {
        var bb = face.GetBoundingBox();
        var p1 = face.Evaluate(new UV(bb.Min.U, bb.Min.V));
        var p2 = face.Evaluate(new UV(bb.Max.U, bb.Min.V));
        var p3 = face.Evaluate(new UV(bb.Min.U, bb.Max.V));
        var width = p1.DistanceTo(p2);
        var height = p1.DistanceTo(p3);
        var widthInches = UnitUtils.ConvertFromInternalUnits(width, UnitTypeId.Inches);
        var heightInches = UnitUtils.ConvertFromInternalUnits(height, UnitTypeId.Inches);
        return Math.Min(widthInches, heightInches);
    }

    public static double GetMinSizeCylindrical(CylindricalFace face) {
        var radius = face.get_Radius(0); // version agnostic, unlike `face.Radius`
        var radiusValue = radius.GetLength(); // Get the magnitude of the radius vector
        var diameter = 2 * radiusValue;
        return UnitUtils.ConvertFromInternalUnits(diameter, UnitTypeId.Inches);
    }

    public static (double width, double height) GetFaceDimensionsInFeet(Face face) {
        var bb = face.GetBoundingBox();
        var p1 = face.Evaluate(new UV(bb.Min.U, bb.Min.V));
        var p2 = face.Evaluate(new UV(bb.Max.U, bb.Min.V));
        var p3 = face.Evaluate(new UV(bb.Min.U, bb.Max.V));

        var width = p1.DistanceTo(p2); // Width in feet
        var height = p1.DistanceTo(p3); // Height in feet
        return (width, height);
    }

    public static bool IsPointInside(Face face, UV point, double marginFeet) {
        var bb = face.GetBoundingBox();
        var p = face.Evaluate(point);

        return p.DistanceTo(face.Evaluate(new UV(bb.Min.U, point.V))) >= marginFeet && // left
               p.DistanceTo(face.Evaluate(new UV(bb.Max.U, point.V))) >= marginFeet && // right
               p.DistanceTo(face.Evaluate(new UV(point.U, bb.Min.V))) >= marginFeet && // btm
               p.DistanceTo(face.Evaluate(new UV(point.U, bb.Max.V))) >= marginFeet; // top
    }

    public static UV AdjustPointToFaceBounds(Face face, UV point, double margin) {
        var bb = face.GetBoundingBox();
        var (faceWidth, faceHeight) = GetFaceDimensionsInFeet(face);
        var tapDiameter = margin * 2;

        // If face is smaller than or close to tap size, center the tap
        if (faceWidth <= tapDiameter * 1.2 || faceHeight <= tapDiameter * 1.2) return GetCenter(face);

        // For larger faces, try to keep the tap as close to click point while ensuring it fits
        var uRatio = (point.U - bb.Min.U) / (bb.Max.U - bb.Min.U);
        var vRatio = (point.V - bb.Min.V) / (bb.Max.V - bb.Min.V);
        uRatio = Math.Max(margin / faceWidth, Math.Min(1 - (margin / faceWidth), uRatio));
        vRatio = Math.Max(margin / faceHeight, Math.Min(1 - (margin / faceHeight), vRatio));

        return new UV(
            bb.Min.U + (uRatio * (bb.Max.U - bb.Min.U)),
            bb.Min.V + (vRatio * (bb.Max.V - bb.Min.V))
        );
    }

    ///<summary>Using a length and an angle, get a new point relative to a reference point</summary>
    public static UV GetPointAtAngle(UV referencePoint, double lengthFeet, double angleInDegrees) {
        var angleInRadians = angleInDegrees * (Math.PI / 180.0);
        var newX = referencePoint.U + (lengthFeet * Math.Cos(angleInRadians));
        var newY = referencePoint.V + (lengthFeet * Math.Sin(angleInRadians));
        return new UV(newX, newY);
    }
}