using Autodesk.Revit.DB.Mechanical;
using PE_Lib;

namespace PE_MEP;

public class MepCurve
{
    /// <summary>
    ///     Get the system type from the main duct to ensure proper inheritance
    /// </summary>
    private static Result<MechanicalSystemType> GetSystemTypeFromDuctTEMP(MEPCurve mainDuct,
        BalloonCollector debugBalloon)
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
            return new InvalidOperationException($"Could not find system type of {mainDuct}");
        }
    }
}