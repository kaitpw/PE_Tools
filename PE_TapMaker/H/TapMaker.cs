using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;
using PE_Lib;
using PE_MEP;

namespace PE_TapMaker.H;

public static class TapMaker
{
    /// <summary>Place a tap on a duct face at the coordinate that a user clicks</summary>
    public static bool PlaceTapOnDuct(UIApplication uiApplication)
    {
        try
        {
            var (selection, selectionError) = Pickers.FacePosition(
                uiApplication,
                new DuctFaceSelectionFilter(),
                "where you want to place the tap"
            );

            return selectionError is null
                   && selection.element is not null
                   && selection.elementFace is not null
                   && selection.clickPosition is not null
                   && CreateTapOnFace(
                       uiApplication,
                       selection.element,
                       selection.elementFace,
                       selection.clickPosition
                   );
        } catch (Exception ex)
        {
            TaskDialog.Show("Error", $"Failed to place tap: {ex.Message}");
            return false;
        }
    }


    private static bool CreateTapOnFace(
        UIApplication uiApplication,
        Element ductElement,
        Face face,
        UV clickPosition,
        double tapSizeInches = 6.0
    )
    {
        var balloon = new BalloonCollector();

        try
        {
            var uidoc = uiApplication.ActiveUIDocument;
            var doc = uidoc.Document;

            balloon.AddDebugMessage("Duct", $"{ductElement.Name} (ID: {ductElement.Id})");

            using (var trans = new Transaction(doc, "Create Tap"))
            {
                trans.Start();

                // Use the user's click position instead of face center
                var clickPoint = face.Evaluate(clickPosition);
                balloon.AddDebugMessage(
                    "Click Position",
                    $"UV: ({clickPosition.U:F3}, {clickPosition.V:F3}) -> XYZ: ({clickPoint.X:F2}, {clickPoint.Y:F2}, {clickPoint.Z:F2})"
                );

                // Get face normal at click position
                var normal = face.ComputeNormal(clickPosition);
                balloon.AddDebugMessage(
                    "Face Normal",
                    $"({normal.X:F2}, {normal.Y:F2}, {normal.Z:F2})"
                );

                tapSizeInches = TapSizer(face, tapSizeInches);
                var success = CreateTap(
                    doc,
                    ductElement as MEPCurve, // TODO: should this throw exception or be null if cast doesnt work
                    clickPoint,
                    normal,
                    tapSizeInches / 12.0,
                    balloon
                );

                if (success)
                {
                    balloon.AddDebugMessage(
                        "SUCCESS",
                        $"Created tap with {tapSizeInches}\" size"
                    );
                    trans.Commit();
                    balloon.AddDebugMessage("Transaction", "Committed successfully");
                    balloon.Show();
                    return true;
                }

                balloon.AddDebugMessage(
                    "Failed Size",
                    $"{tapSizeInches}\" tap failed, trying next smaller size..."
                );
                // Continue to try next smaller size


                // If we get here, all sizes failed
                balloon.AddDebugMessage(
                    "FINAL RESULT",
                    "All tap sizes failed - insufficient space or other error"
                );
                trans.RollBack();
                balloon.AddDebugMessage("Transaction", "Rolled back due to failure");
                balloon.Show();
                return false;
            }
        } catch (Exception ex)
        {
            balloon.AddException("CreateTapOnFace", ex);
            balloon.Show();
            return false; // Don't throw, just return false so user can see debug info
        }
    }

    private static double TapSizer(Face face, double defaultTapSizeInches)
    {
        var sizesInches = face switch
        {
            PlanarFace => new[] { 16.0, 14.0, 12.0, 10.0, 8.0, 6.0, 5.0, 4.0, 3.0 },
            CylindricalFace => new[] { 16.0, 14.0, 12.0, 10.0, 8.0, 6.0, 5.0, 4.0 },
            _ => Array.Empty<double>()
        };
        var faceMinSizeInches = face switch
        {
            PlanarFace pf => GetPlanarFaceMinSize(pf),
            CylindricalFace cf => GetCylindricalFaceMinSize(cf),
            _ => 0.0
        };

        // Return default tap size if it fits on face. Happy path
        if (defaultTapSizeInches <= faceMinSizeInches
            && sizesInches.Contains(defaultTapSizeInches))
            return defaultTapSizeInches;

        // Otherwise, get size that fits. Implicitly, default is 0.0 (bc double's def is 0.0)
        return sizesInches.FirstOrDefault(size => size <= faceMinSizeInches);
    }

    private static double GetPlanarFaceMinSize(PlanarFace face)
    {
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

    private static double GetCylindricalFaceMinSize(CylindricalFace face)
    {
        // In all versions, use get_Radius(0) method to get the radius vector
        var radius = face.get_Radius(0);
        var radiusValue = radius.GetLength(); // Get the magnitude of the radius vector
        var diameter = 2 * radiusValue;
        return UnitUtils.ConvertFromInternalUnits(diameter, UnitTypeId.Inches);
    }


    /// <summary>
    ///     Create a proper takeoff fitting using Document.NewTakeoffFitting
    ///     This ensures the tap is connected to the duct system
    /// </summary>
    private static bool CreateTap(
        Document doc,
        MEPCurve ductElement,
        XYZ location,
        XYZ direction,
        double tapSizeInFeet,
        BalloonCollector balloon
    )
    {
        try
        {
            var tapDuctType =
                new[] { ConnectorProfileType.Round, ConnectorProfileType.Rectangular, ConnectorProfileType.Oval }
                    .Select(shape => Filters.DuctType(doc, shape, JunctionType.Tap))
                    .FirstOrDefault(result => result is not null);
            if (tapDuctType is null) return false;


            balloon.AddDebugMessage(
                "CreateTap",
                $"Tap duct type: {tapDuctType.Name}, Size: {tapSizeInFeet * 12.0:F1}\""
            );


            var (level, levelError) = Levels.LevelOfMepCurve(ductElement);
            if (level is null)
            {
                balloon.AddDebugMessage(
                    "CreateTap",
                    "FAILED: Could not get level from duct"
                );
                return false;
            }

            balloon.AddDebugMessage("CreateTap", $"Using level: {level.Name}");

            // Get system type from the main duct to ensure inheritance
            var (systemType, systemTypeError) = GetSystemTypeFromDuct(ductElement, balloon);
            if (systemType is null)
            {
                balloon.AddDebugMessage(
                    "CreateTap",
                    "FAILED: Could not get system type from main duct"
                );
                return false;
            }

            balloon.AddDebugMessage("CreateTap", $"Using system type from main duct: {systemType.Name}");

            // Create a short branch duct perpendicular to the main duct
            var branchStart = location;
            var branchEnd = location + (direction.Normalize() * 0.5); // 6 inch stub

            balloon.AddDebugMessage(
                "CreateTap",
                $"Branch: ({branchStart.X:F2},{branchStart.Y:F2},{branchStart.Z:F2}) to ({branchEnd.X:F2},{branchEnd.Y:F2},{branchEnd.Z:F2})"
            );

            // Create the branch duct - system inheritance will happen through the connection
            var branchDuct = Duct.Create(
                doc,
                systemType.Id,
                tapDuctType.Id,
                level.Id,
                branchStart,
                branchEnd
            );
            if (branchDuct is null)
            {
                balloon.AddDebugMessage(
                    "CreateTap",
                    "FAILED: Could not create branch duct"
                );
                return false;
            }

            balloon.AddDebugMessage(
                "CreateTap",
                $"Created branch duct ID: {branchDuct.Id}"
            );

            // Set the duct diameter to the correct tap size
            var diameterParam = branchDuct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
            if (diameterParam is { IsReadOnly: false })
            {
                diameterParam.Set(tapSizeInFeet);
                balloon.AddDebugMessage(
                    "CreateTap",
                    $"Set duct diameter to {tapSizeInFeet * 12.0:F1}\" ({tapSizeInFeet:F3} feet)"
                );
            } else
            {
                balloon.AddDebugMessage(
                    "CreateTap",
                    "WARNING: Could not set duct diameter parameter"
                );
            }

            // Copy system properties from main duct to branch duct
            CopySystemProperties(ductElement, branchDuct, balloon);

            // Get the connector from the branch duct closest to the main duct
            var branchConnector = GetConnectorClosestTo(branchDuct, location);
            if (branchConnector is null)
            {
                balloon.AddDebugMessage(
                    "CreateTap",
                    "FAILED: Could not find branch connector"
                );
                doc.Delete(branchDuct.Id); // Clean up
                return false;
            }

            balloon.AddDebugMessage(
                "CreateTap",
                $"Found branch connector at: ({branchConnector.Origin.X:F2},{branchConnector.Origin.Y:F2},{branchConnector.Origin.Z:F2})"
            );

            // Regenerate document to ensure connectors are properly established
            doc.Regenerate();

            // Re-get the connector after regeneration
            branchConnector = GetConnectorClosestTo(branchDuct, location);
            if (branchConnector is null)
            {
                balloon.AddDebugMessage(
                    "CreateTap",
                    "FAILED: Could not find branch connector after regeneration"
                );
                doc.Delete(branchDuct.Id); // Clean up
                return false;
            }

            // Create the takeoff fitting - this connects the branch to the main duct
            balloon.AddDebugMessage(
                "CreateTap",
                $"Attempting NewTakeoffFitting with branch connector (Domain: {branchConnector.Domain}) and main duct ID: {ductElement.Id}"
            );

            try
            {
                var takeoffFitting = doc.Create.NewTakeoffFitting(
                    branchConnector,
                    ductElement
                );

                if (takeoffFitting != null)
                {
                    balloon.AddDebugMessage(
                        "CreateTap",
                        $"SUCCESS: Created takeoff fitting ID: {takeoffFitting.Id}"
                    );

                    // Regenerate again to ensure proper connections
                    doc.Regenerate();

                    // Verify the connection was made successfully
                    if (branchConnector.IsConnected)
                    {
                        balloon.AddDebugMessage(
                            "CreateTap",
                            "SUCCESS: Branch connector is properly connected"
                        );
                    } else
                    {
                        balloon.AddDebugMessage(
                            "CreateTap",
                            "WARNING: Branch connector shows as not connected, but takeoff fitting was created"
                        );
                    }

                    balloon.AddDebugMessage(
                        "CreateTap",
                        "Tap creation completed successfully"
                    );

                    return true;
                }

                balloon.AddDebugMessage(
                    "CreateTap",
                    "FAILED: NewTakeoffFitting returned null"
                );
                doc.Delete(branchDuct.Id); // Clean up failed attempt
                return false;
            } catch (Exception takeoffEx)
            {
                balloon.AddException("NewTakeoffFitting", takeoffEx);
                balloon.AddDebugMessage(
                    "CreateTap",
                    $"FAILED: Exception during NewTakeoffFitting: {takeoffEx.Message}"
                );
                doc.Delete(branchDuct.Id); // Clean up failed attempt
                return false;
            }
        } catch (Exception ex)
        {
            balloon.AddException("CreateTap", ex);
            return false;
        }
    }

    /// <summary>
    ///     Get the system type from the main duct to ensure proper inheritance
    /// </summary>
    private static Result<MechanicalSystemType> GetSystemTypeFromDuct(MEPCurve mainDuct, BalloonCollector debugBalloon)
    {
        try
        {
            // First try to get the system type from the duct's MEP system
            if (mainDuct.MEPSystem != null)
            {
                var systemTypeId = mainDuct.MEPSystem.GetTypeId();
                if (systemTypeId != ElementId.InvalidElementId)
                {
                    var systemType = mainDuct.Document.GetElement(systemTypeId) as MechanicalSystemType;
                    if (systemType != null)
                    {
                        debugBalloon.AddDebugMessage("GetSystemTypeFromDuct",
                            $"Found system type from MEPSystem: {systemType.Name}");
                        return systemType;
                    }
                }
            }

            // Fallback: try to get system type from duct's system type parameter
            var systemTypeParam = mainDuct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
            if (systemTypeParam != null && systemTypeParam.AsElementId() != ElementId.InvalidElementId)
            {
                var systemType = mainDuct.Document.GetElement(systemTypeParam.AsElementId()) as MechanicalSystemType;
                if (systemType != null)
                {
                    debugBalloon.AddDebugMessage("GetSystemTypeFromDuct",
                        $"Found system type from parameter: {systemType.Name}");
                    return systemType;
                }
            }

            // Last resort: use any available system type
            debugBalloon.AddDebugMessage("GetSystemTypeFromDuct", "Main duct has no system type, using fallback");
            return new InvalidOperationException($"Could not find system type of {mainDuct}");
        } catch (Exception ex)
        {
            debugBalloon.AddException("GetSystemTypeFromDuct", ex);
            return new  InvalidOperationException($"Could not find system type of {mainDuct}");
        }
    }

    /// <summary>
    ///     Copy system properties from main duct to branch duct for proper inheritance
    /// </summary>
    private static void CopySystemProperties(MEPCurve mainDuct, MEPCurve branchDuct, BalloonCollector debugBalloon)
    {
        try
        {
            // Copy system name if available
            var mainSystemName = mainDuct.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            var branchSystemName = branchDuct.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            if (mainSystemName != null && branchSystemName != null && !branchSystemName.IsReadOnly)
            {
                branchSystemName.Set(mainSystemName.AsString());
                debugBalloon.AddDebugMessage("CopySystemProperties",
                    $"Copied system name: {mainSystemName.AsString()}");
            }

            // Copy system abbreviation
            var mainSystemAbbrev = mainDuct.get_Parameter(BuiltInParameter.RBS_SYSTEM_ABBREVIATION_PARAM);
            var branchSystemAbbrev = branchDuct.get_Parameter(BuiltInParameter.RBS_SYSTEM_ABBREVIATION_PARAM);
            if (mainSystemAbbrev != null && branchSystemAbbrev != null && !branchSystemAbbrev.IsReadOnly)
            {
                branchSystemAbbrev.Set(mainSystemAbbrev.AsString());
                debugBalloon.AddDebugMessage("CopySystemProperties",
                    $"Copied system abbreviation: {mainSystemAbbrev.AsString()}");
            }

            // Copy system classification if it exists as a parameter
            var mainSystemClass = mainDuct.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
            var branchSystemClass = branchDuct.get_Parameter(BuiltInParameter.RBS_SYSTEM_CLASSIFICATION_PARAM);
            if (mainSystemClass != null && branchSystemClass != null && !branchSystemClass.IsReadOnly)
            {
                branchSystemClass.Set(mainSystemClass.AsString());
                debugBalloon.AddDebugMessage("CopySystemProperties",
                    $"Copied system classification: {mainSystemClass.AsString()}");
            }

            debugBalloon.AddDebugMessage("CopySystemProperties", "System property copying completed");
        } catch (Exception ex)
        {
            debugBalloon.AddException("CopySystemProperties", ex);
        }
    }


    /// <summary>Get the connector on an element closest to the specified location</summary>
    private static Connector GetConnectorClosestTo(Element element, XYZ location)
    {
        ConnectorManager cm = null;

        if (element is MEPCurve mepCurve)
            cm = mepCurve.ConnectorManager;
        else if (element is FamilyInstance fi && fi.MEPModel != null)
            cm = fi.MEPModel.ConnectorManager;

        if (cm is null)
            return null;

        Connector closestConnector = null;
        var minDistance = double.MaxValue;

        foreach (Connector connector in cm.Connectors)
        {
            var distance = location.DistanceTo(connector.Origin);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestConnector = connector;
            }
        }

        return closestConnector;
    }

    private static UV GetFaceCenter(Face face)
    {
        var bbox = face.GetBoundingBox();
        return new UV((bbox.Min.U + bbox.Max.U) / 2.0, (bbox.Min.V + bbox.Max.V) / 2.0);
    }
}

public class DuctFaceSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
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