using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PE_Lib;

namespace PE_Tools
{
    [Transaction(TransactionMode.Manual)]
    public class cmdMep2040 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elementSet
        )
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // --- Collect sustainability metrics ---
            // 1. Total length of metal pipes (e.g., "Copper")
            // TODO: Confirm material name for metal pipes in your design system
            double metalPipeLength = Utils.TotalPipeLength(doc);

            // 2. Total volume of refrigerant line
            // TODO: Confirm system type name for refrigerant lines in your design system
            double refrigerantVolume = Utils.TotalPipeVolume(doc, "RL - Refrigerant Liquid");

            // 3. Count of each type of MEP equipment
            var equipmentCounts = Utils.CountMEPEquipmentByType(doc);

            // --- Format results ---
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Total Pipe Length: {metalPipeLength:F2} ft");
            sb.AppendLine($"Total RL Volume: {refrigerantVolume:F2} ft³");
            sb.AppendLine("\nMEP Equipment Counts:");
            foreach (var kvp in equipmentCounts)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            // Show results in a balloon (or use TaskDialog if preferred)
            UiUtils.ShowBalloon(sb.ToString(), "Sustainability Metrics");

            return Result.Succeeded;
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "CmdBtnMep2040";
            string buttonTitle = "MEP 2040";

            PE_Init.ButtonDataClass myButtonData = new PE_Init.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Green_32,
                Properties.Resources.Green_16,
                "Click to analyze MEP sustainability metrics (pipe length, refrigerant volume, equipment count)."
            );

            return myButtonData.Data;
        }
    }
}
