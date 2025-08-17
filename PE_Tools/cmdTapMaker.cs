using PE_Init;
using PE_TapMaker.H;
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

            // Simply place the tap where the user clicks - no UI needed!
            var success = TapMaker.PlaceTapOnDuct(uiapp);

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
}