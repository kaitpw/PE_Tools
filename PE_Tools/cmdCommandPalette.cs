using AddinCmdPalette.ViewModels;
using AddinCmdPalette.Views;
using PeServices.Storage;

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

            // Create persistence service for the command palette
            var persistence = new Storage(nameof(CmdCommandPalette));

            var paletteWindow = new CommandPaletteWindow();
            var viewModel = new CommandPaletteViewModel(commandData.Application, persistence);
            paletteWindow.DataContext = viewModel;
            paletteWindow.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            throw new InvalidOperationException($"Error opening command palette: {ex.Message}");
        }
    }
}