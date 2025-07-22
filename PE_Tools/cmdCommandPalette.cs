using System.Windows.Threading;
using PE_CommandPalette.V;
using PE_CommandPalette.VM;

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
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc?.Document;

                // 1. Create the View
                CommandPaletteWindow paletteWindow = new CommandPaletteWindow();

                // 2. Get the Dispatcher from the View, capturing the dispatcher here is crucial for multi-targeting purposes
                Dispatcher windowDispatcher = paletteWindow.Dispatcher;

                // 3. Create the ViewModel, passing dependencies
                CommandPaletteViewModel viewModel = new CommandPaletteViewModel(
                    commandData.Application,
                    windowDispatcher
                );

                // 4. Set the DataContext directly (this is standard practice)
                paletteWindow.DataContext = viewModel;

                // 5. Show the View
                paletteWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error opening command palette: {ex.Message}");
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
