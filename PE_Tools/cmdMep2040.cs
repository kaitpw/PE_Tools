using PE_Init;
using PE_Lib;
using PE_Tools.Properties;
using System.Text;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class cmdMep2040 : IExternalCommand {

    internal static PushButtonData GetButtonData() {
        return new ButtonDataClass(
            "MEP 2040",
            MethodBase.GetCurrentMethod()?.DeclaringType?.FullName,
            Resources.Green_32,
            Resources.Green_16,
            "Click to analyze MEP sustainability metrics (pipe length, refrigerant volume, equipment count)."
        ).Data;
    }

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;

        // --- Collect sustainability metrics ---
        // 1. Total length of metal pipes (e.g., "Copper")
        // TODO: Confirm material name for metal pipes in your design system
        var metalPipeLength = Utils.TotalPipeLength(doc);

        // 2. Total volume of refrigerant line
        // TODO: Confirm system type name for refrigerant lines in your design system
        var refrigerantVolume = Utils.TotalPipeVolume(doc, "RL - Refrigerant Liquid");

        // 3. Count of each type of MEP equipment
        var equipmentCounts = Utils.CountMEPEquipmentByType(doc);

        // --- Format results ---
        var sb = new StringBuilder();
        sb.AppendLine($"Total Pipe Length: {metalPipeLength:F2} ft");
        sb.AppendLine($"Total RL Volume: {refrigerantVolume:F2} ft³");
        sb.AppendLine("\nMEP Equipment Counts:");
        foreach (var kvp in equipmentCounts)
            sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

        // Show results in a balloon (or use TaskDialog if preferred)
        UiUtils.ShowBalloon(sb.ToString(), "Sustainability Metrics");

        return Result.Succeeded;
    }
}