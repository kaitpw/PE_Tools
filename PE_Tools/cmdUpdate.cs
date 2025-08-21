using PE_Tools.Properties;
using PeRevitUI;
using ricaun.Revit.Github;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdUpdate : IExternalCommand {
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
        var result = RunRequest(request);
            
            new Balloon()
                .Add(Balloon.LogLevel.INFO, $"Download: {result}")
                .Show(() => { }, "None"
                    // TODO: Figure out how to get the request to rerun
                    // RunRequest(request),
                    // "Click to Retry Download"
                    );

        return Result.Succeeded;
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "Update",
            MethodBase.GetCurrentMethod().DeclaringType?.FullName,
            Resources.Blue_32,
            Resources.Blue_16,
            "Click this button to update PE Tools to the latest release. You will need to restart Revit"
        ).Data;
    
    public static Task<bool> RunRequest( GithubRequestService request) =>
        Task.Run(() => request.Initialize(Console.WriteLine));
}