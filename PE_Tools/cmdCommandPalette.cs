using PE_CommandPalette.Views;

namespace PE_Tools
{
    [Transaction(TransactionMode.Manual)]
    public class cmdCommandPalette : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elementSet
        )
        {
            try
            {
                // Get Revit application and document variables
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc?.Document;

                // Create and show the command palette window
                var commandPalette = new CommandPaletteWindow(uiapp);
                
                // Show as modal dialog to ensure proper focus handling
                var result = commandPalette.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error opening command palette: {ex.Message}";
                return Result.Failed;
            }
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "CmdBtnCommandPalette";
            string buttonTitle = "Command Palette";

            PE_Init.ButtonDataClass myButtonData = new PE_Init.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Yellow_32,
                Properties.Resources.Yellow_16,
                "Open the command palette to search and execute Revit commands quickly. Use Ctrl+K for quick access."
            );

            return myButtonData.Data;
        }
    }
}