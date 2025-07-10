using PE_Lib;
using ricaun.Revit.Github;

namespace PE_Tools
{
    [Transaction(TransactionMode.Manual)]
    public class Update : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elementSet
        )
        {
            // Revit application and document variables
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Fetch the latest Github release
            var request = new GithubRequestService("kaitpw", "PE_Tools");
            Task.Run(async () =>
            {
                var result = await request.Initialize(
                    (text) =>
                    {
                        Console.WriteLine(text);
                    }
                );
                Utils.ShowBalloon($"Download: {result}");
            });

            TaskDialog.Show("a message", $"message {PE_Lib.Utils.Num()}");

            return Result.Succeeded;
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "CmdBtnUpdate";
            string buttonTitle = "Update";

            PE_Init.ButtonDataClass myButtonData = new PE_Init.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "Click this button to update PE Tools to the latest release."
            );

            return myButtonData.Data;
        }
    }
}
