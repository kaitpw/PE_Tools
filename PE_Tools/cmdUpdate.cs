using PE_Init;
using PE_Lib;
using PE_Tools.Properties;
using ricaun.Revit.Github;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class cmdUpdate : IExternalCommand {
    internal static PushButtonData GetButtonData() {
        return new ButtonDataClass(
            "Update",
            MethodBase.GetCurrentMethod().DeclaringType?.FullName,
            Resources.Blue_32,
            Resources.Blue_16,
            "Click this button to update PE Tools to the latest release. You will need to restart Revit"
        ).Data;
    }

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        // Revit application and document variables
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;

        // Fetch the latest Github release
        var request = new GithubRequestService("kaitpw", "PE_Tools");
        Task.Run(async () => {
            var result = await request.Initialize(text => {
                Console.WriteLine(text);
            }
            );
            UiUtils.ShowBalloon($"Download: {result}");
        });

        return Result.Succeeded;
    }
}