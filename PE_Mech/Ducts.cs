using Autodesk.Revit.DB.Mechanical;
using PE_Lib;
using PE_MEP;

namespace PE_Mech;

internal class Ducts {
    /// <summary>
    ///     Create a proper takeoff fitting using Document.NewTakeoffFitting
    ///     This ensures the tap is connected to the duct system
    /// </summary>
    public static Result<FamilyInstance> MakeTakeoffWithBranch(
        Document doc,
        MEPCurve trunkDuct,
        XYZ location,
        XYZ direction,
        double tapSizeFeet,
        DuctType ductType,
        BalloonCollector balloon = null
    ) {
        try {
            var (level, _) = MepCurve.GetReferenceLevel(trunkDuct);
            if (level is null) return new InvalidOperationException("ReferenceLevel is null, nothing was found");
            balloon?.AddDebug(new StackFrame(), $"Using reference level: {level.Name}");

            var (systemType, _) = MepCurve.GetSystemType(trunkDuct);
            if (systemType is null) return new InvalidOperationException("SystemType is null, nothing was found");
            balloon?.AddDebug(new StackFrame(), $"Using system type from main duct: {systemType.Name}");

            // Check for existing elements at the tap location first
            var boundingBox = new BoundingBoxXYZ();
            boundingBox.Min = location - new XYZ(tapSizeFeet / 2, tapSizeFeet / 2, tapSizeFeet / 2);
            boundingBox.Max = location + new XYZ(tapSizeFeet / 2, tapSizeFeet / 2, tapSizeFeet / 2);

            var outline = new Outline(boundingBox.Min, boundingBox.Max);
            var bbIntersectsFilter = new BoundingBoxIntersectsFilter(outline);

            var existingElements = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DuctFitting)
                .WhereElementIsNotElementType()
                .WherePasses(bbIntersectsFilter)
                .ToElements()
                .Where(e => e.Id != trunkDuct.Id)
                .ToList();

            if (existingElements.Any()) {
                var existingIds = existingElements.Select(e => e.Id).ToArray();
                return new ElementIntersectException(existingIds[0], existingIds);
            }

            // If no intersections, proceed with creating the tap
            var branchStart = location;
            var branchEnd = location + (direction.Normalize() * 0.5); // 6 inch stub
            var branchDuct = Duct.Create(
                doc,
                systemType.Id,
                ductType.Id,
                level.Id,
                branchStart,
                branchEnd
            );
            if (branchDuct is null) return new InvalidOperationException("Branch duct is null, creation was faulty");
            balloon?.AddDebug(new StackFrame(), $"Created branch duct ID: {branchDuct.Id}");

            // Set the duct diameter to the correct tap size
            var setDiamSuccess = branchDuct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM).Set(tapSizeFeet);
            if (!setDiamSuccess) balloon?.Add(new StackFrame(), "Branch duct's diameter could not be set");

            // Get the connector from the branch duct closest to the main duct
            var (branchConns, _) = Connectors.GetClosestToPoint(branchDuct, location);
            if (branchConns is null)
                return new InvalidOperationException("Branch connectors are null, nothing was found");
            var branchConn = branchConns[0];

            var fitting = doc.Create.NewTakeoffFitting(branchConn, trunkDuct);
            if (fitting is null) {
                //_ = doc.Delete(branchDuct.Id); // TODO: figure out why a pipe still exists even if we delete the branchDuct here
                return new InvalidOperationException("Failed to create takeoff fitting");
            }

            return fitting != null && !branchConn.IsConnected
                ? new InvalidOperationException("Tap not properly connected to branch duct.")
                : fitting;
        } catch (Exception ex) {
            balloon?.AddException(new StackFrame(), ex);
            return ex;
        }
    }
}