using AddinCmdPalette.ViewModels;
using AddinCmdPalette.Views;
using PE_Tools.Properties;
using PeRevitUI;
using PeServices;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public partial class CmdCommandPalette : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        try {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            // Create persistence service for the command palette
            var persistence = new Storage("CommandPalette");

            var paletteWindow = new CommandPaletteWindow();
            var viewModel = new CommandPaletteViewModel(commandData.Application, persistence);
            paletteWindow.DataContext = viewModel;
            paletteWindow.Show();

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