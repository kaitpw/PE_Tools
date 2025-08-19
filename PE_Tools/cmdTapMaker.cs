using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;
using PE_Init;
using PE_Lib;
using PE_Mech;
using PE_Tools.Properties;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class cmdTapMaker : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        try {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc?.Document;

            if (doc == null) {
                message = "No active document found";
                return Result.Failed;
            }

            bool success = false;

            while (true) {
                var (selection, selectionError) = Pickers.FacePosition(
                    uiapp,
                    new DuctFaceSelectionFilter(),
                    "where you want to place the tap"
                );

                if (selectionError is not null)
                    break;

                if (selection.element is not null
                    && selection.elementFace is not null
                    && selection.clickPosition is not null) {
                    success = CreateTapOnFace(
                        uiapp,
                        selection.element,
                        selection.elementFace,
                        selection.clickPosition
                    );
                }
            }

            return success ? Result.Succeeded : Result.Cancelled;
        } catch (Exception ex) {
            message = $"Error placing tap: {ex.Message}";
            return Result.Failed;
        }
    }

    internal static PushButtonData GetButtonData() {
        var buttonInternalName = "CmdBtnTapMaker";
        var buttonTitle = "Tap Maker";

        var myButtonData = new ButtonDataClass(
            buttonInternalName,
            buttonTitle,
            MethodBase.GetCurrentMethod().DeclaringType?.FullName,
            Resources.Green_32,
            Resources.Green_16,
            "Add 6\" taps to duct faces. Click on any duct face to place a tap at that exact location."
        );

        return myButtonData.Data;
    }

    public static bool CreateTapOnFace(
        UIApplication uiApplication,
        Element trunkDuct,
        Face face,
        UV clickPosition,
        double tapSizeInches = 6.0
    ) {
        var balloon = new BalloonCollector();

        try {
            var uidoc = uiApplication.ActiveUIDocument;
            var doc = uidoc.Document;

            tapSizeInches = TapSizer(face, tapSizeInches);
            var tapSizeFeet = tapSizeInches / 12.0;
            var tapRadiusFeet = tapSizeInches / 2 / 12.0; // Convert to feet for Revit internal units

            var locationAdjusted = (!Faces.IsPointInside(face, clickPosition, tapRadiusFeet))
                ? Faces.ConstrainUVPointWithMargin(face, clickPosition, tapRadiusFeet)
                : clickPosition;
            var location = face.Evaluate(locationAdjusted);
            var normal = face.ComputeNormal(locationAdjusted);

            var tapDuctType =
                new[] { ConnectorProfileType.Round, ConnectorProfileType.Rectangular, ConnectorProfileType.Oval }
                    .Select(shape => Filters.DuctType(doc, shape, JunctionType.Tap))
                    .FirstOrDefault(result => result is not null);
            if (tapDuctType is null) throw new InvalidOperationException("DuctType is null, nothing was found");

            using (var trans = new Transaction(doc, "Create Tap")) {
                _ = trans.Start();

                var (tap, tapError) = Ducts.MakeTakeoffWithBranch(
                    doc,
                    trunkDuct as MEPCurve,
                    location,
                    normal,
                    tapSizeFeet,
                    tapDuctType,
                    balloon
                );

                if (tapError is ElementIntersectException) {
                    (tap, tapError) = TapPlacer(
                        doc,
                        face,
                        trunkDuct,
                        locationAdjusted,
                        tapSizeFeet,
                        tapDuctType,
                        balloon
                    );
                }

                if (tap is not null && tapError is null) {
                    _ = trans.Commit();
                    balloon.Add(new StackFrame(), $"Created a {tapSizeInches}\" tap successfully.");
                    balloon?.Show();
                    return true;
                }

                _ = trans.RollBack();
                balloon.Show();
                return false;
            }
        } catch (Exception ex) {
            balloon.AddException(new StackFrame(), ex);
            balloon.Show();
            return false; // Don't throw, just return false so user can see debug info
        }
    }

    private static Result<FamilyInstance> TapPlacer(
        Document doc,
        Face face,
        Element trunkDuct,
        UV adjustedPosition,
        double tapSizeFeet,
        DuctType ductType,
        BalloonCollector balloon = null
    ) {
        FamilyInstance tap;
        Exception tapError;

        // Get all radial points at 45 degree increments in order of center-proximity
        var altPositions = Enumerable.Range(1, 9)
            .Select(i =>
                Faces.GetPointAtAngle(adjustedPosition, tapSizeFeet / 2, i * 45.0))
            .ToList()
            .OrderBy(p => (p - Faces.GetCenter(face)).GetLength()); // Order by distance from center

        try {
            foreach (var altPosition in altPositions) {
                if (!Faces.IsPointInside(face, altPosition, tapSizeFeet / 2)) continue;
                var altPoint = face.Evaluate(altPosition);
                var altNormal = face.ComputeNormal(altPosition);
                (tap, tapError) = Ducts.MakeTakeoffWithBranch(
                    doc,
                    trunkDuct as MEPCurve,
                    altPoint,
                    altNormal,
                    tapSizeFeet,
                    ductType,
                    balloon);
                if (tap is null || tapError is not null) continue;

                balloon?.AddDebug(new StackFrame(),
                    $"Found working position at UV: ({altPosition.U:F3}, {altPosition.V:F3})"
                );
                return tap;
            }

            return new InvalidOperationException("No fallback tap could be placed");
        } catch (Exception ex) {
            return ex;
        }
    }

    private static double TapSizer(Face face, double defaultTapSizeInches) {
        var sizesInches = face switch {
            PlanarFace => new[] { 16.0, 14.0, 12.0, 10.0, 8.0, 6.0, 5.0, 4.0, 3.0 },
            CylindricalFace => new[] { 16.0, 14.0, 12.0, 10.0, 8.0, 6.0, 5.0, 4.0 },
            _ => Array.Empty<double>()
        };
        var faceMinSizeInches = face switch {
            PlanarFace pf => Faces.GetMinSizePlanar(pf),
            CylindricalFace cf => Faces.GetMinSizeCylindrical(cf),
            _ => 0.0
        };

        // Return default tap size if it fits on face. Happy path
        if (defaultTapSizeInches <= faceMinSizeInches
            && sizesInches.Contains(defaultTapSizeInches))
            return defaultTapSizeInches;

        // Otherwise, get size that fits. Implicitly, default is 0.0 (bc double's def is 0.0)
        return sizesInches.FirstOrDefault(size => size <= faceMinSizeInches);
    }
}

public class DuctFaceSelectionFilter : ISelectionFilter {
    public bool AllowElement(Element elem) {
        // Only allow ducts
#if REVIT2025 || REVIT2026
        return elem.Category?.Id.Value == (int)BuiltInCategory.OST_DuctCurves;
#else
        return elem.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_DuctCurves;
#endif
    }

    public bool AllowReference(Reference reference, XYZ position) =>
        // Allow face selection on ducts
        true;
}