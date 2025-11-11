using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Helpers;
using AddinCmdPalette.Services;
using PeServices.Storage;

namespace AddinCmdPalette.Commands;

/// <summary>
///     Factory service for creating command palette instances
/// </summary>
public static class CommandPaletteService {
    /// <summary>
    ///     Creates a command palette window configured for Revit commands
    /// </summary>
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
            },
            "CommandPalette"
        );

        // Create actions
        var actions = new List<PaletteAction> {
            new() {
                Name = "Execute Command",
                ExecuteAsync = async item => {
                    if (item is PostableCommandItem cmdItem) {
                        await Task.Run(() => {
                            var (success, error) = PeRevit.Lib.Commands.Execute(uiApp, cmdItem.Command);
                            if (error is not null) throw error;
                            if (success) commandHelper.UpdateCommandUsage(cmdItem.Command);
                        });
                    }
                },
                CanExecute = item => {
                    if (item is PostableCommandItem cmdItem)
                        return PeRevit.Lib.Commands.IsAvailable(uiApp, cmdItem.Command);
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