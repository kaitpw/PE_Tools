using PE_Addin_CommandPalette.V;
using PE_Addin_CommandPalette.VM;
using PE_Init;
using PE_Tools.Properties;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdCommandPalette : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        try {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            // 1. Create the View
            var paletteWindow = new CommandPaletteWindow();

            // 2. Get the Dispatcher from the View, capturing the dispatcher here is crucial for multi-targeting purposes
            var windowDispatcher = paletteWindow.Dispatcher;

            // 3. Create the ViewModel, passing dependencies
            var viewModel = new CommandPaletteViewModel(
                commandData.Application,
                windowDispatcher
            );

            // 4. Set the DataContext directly (this is standard practice)
            paletteWindow.DataContext = viewModel;

            // 5. Show the View
            paletteWindow.ShowDialog();

            return Result.Succeeded;
        } catch (Exception ex) {
            throw new InvalidOperationException($"Error opening command palette: {ex.Message}");
        }
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "Command Palette",
            MethodBase.GetCurrentMethod()?.DeclaringType?.FullName,
            Resources.Yellow_32,
            Resources.Yellow_16,
            "Open the command palette to search and execute Revit commands quickly. Use Ctrl+K for quick access."
        ).Data;
}