using AddinCmdPalette.Actions;
using AddinCmdPalette.Commands;
using AddinCmdPalette.Core;
using AddinCmdPalette.Helpers;
using AddinCmdPalette.Services;
using PeRevit.Lib;
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
            var persistence = new Storage(nameof(CmdCommandPalette));

            // Create and show palette using new API
            var palette = CommandPaletteService.Create(uiapp, persistence);
            palette.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            throw new InvalidOperationException($"Error opening command palette: {ex.Message}");
        }
    }
}

public static class CommandPaletteService {
    public static SelectablePalette Create(
        UIApplication uiApp,
        Storage persistence
    ) {
        // Load commands using existing helper
        var commandHelper = new PostableCommandHelper(persistence);
        var commandItems = commandHelper.GetAllCommands();

        // Convert to ISelectableItem adapters
        var selectableItems = commandItems
            .Cast<ISelectableItem>()
            .ToList();

        // Create search filter service
        var searchService = new SearchFilterService(
            persistence,
            item => {
                if (item is PostableCommandItem cmdItem)
                    return cmdItem.Command.Value.ToString() ?? string.Empty;
                return item.PrimaryText;
            });

        // Create actions
        var actions = new List<PaletteAction> {
            new() {
                Name = "Execute Command",
                ExecuteAsync = async item => {
                    if (item is PostableCommandItem cmdItem) {
                        await Task.Run(() => {
                            var (success, error) = Commands.Execute(uiApp, cmdItem.Command);
                            if (error is not null) throw error;
                            if (success) commandHelper.UpdateCommandUsage(cmdItem.Command);
                        });
                    }
                },
                CanExecute = item => {
                    if (item is PostableCommandItem cmdItem)
                        return Commands.IsAvailable(uiApp, cmdItem.Command);
                    return false;
                }
            }
        };

        // Create view model
        var viewModel = new SelectablePaletteViewModel(selectableItems, searchService);

        // Create and return palette window
        return new SelectablePalette(viewModel, actions);
    }
}