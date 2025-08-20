using PE_Addin_CommandPalette.M;

namespace PE_Addin_CommandPalette.H;

/// <summary>
///     Service for executing PostableCommand items in Revit
/// </summary>
public class CommandExecutionHelper {

    private RevitCommandId GetCommandId(PostableCommandItem commandItem) =>
        commandItem == null
            ? throw new ArgumentNullException(nameof(commandItem))
            : !string.IsNullOrEmpty(commandItem.ExternalCommandId)
                ? RevitCommandId.LookupCommandId(commandItem.ExternalCommandId)
                : RevitCommandId.LookupPostableCommandId(commandItem.Command);

    private bool GetPseudoAvailability(UIApplication uiapp, PostableCommandItem commandItem) {
        var commandId = this.GetCommandId(commandItem);
        var pseudoAvailability = uiapp.CanPostCommand(commandId)
            || !string.IsNullOrEmpty(commandItem.ExternalCommandId);// For external commands, CanPostCommand may not be meaningful, so just check commandId.
        return commandId is not null && pseudoAvailability;
    }

    /// <summary>
    ///     Executes the specified PostableCommand
    /// </summary>
    /// <param name="commandItem">The command item to execute</param>
    /// <returns>True if execution was successful, false otherwise</returns>
    public bool ExecuteCommand(UIApplication uiapp, PostableCommandItem commandItem) {
        try {
            var commandId = this.GetCommandId(commandItem);
            if (commandId is null) throw new InvalidOperationException($"Command '{commandItem.Name}' is not available in this context.");

            if (!this.GetPseudoAvailability(uiapp, commandItem)) {
                throw new InvalidOperationException(
                    $"Command '{commandItem.Name}' cannot be executed at this time."
                );
            }

            uiapp.PostCommand(commandId);
            PostableCommandHelper.Instance.UpdateCommandUsage(commandItem);

            return true;
        } catch (Exception ex) {
            throw ex;
        }
    }

    /// <summary>
    ///     Checks if a command is available for execution
    /// </summary>
    /// <param name="commandItem">The command item to check</param>
    /// <returns>True if the command is available, false otherwise</returns>
    public bool IsCommandAvailable(UIApplication uiapp, PostableCommandItem commandItem) {
        try {
            var commandId = this.GetCommandId(commandItem);
            var pseudoAvailability = this.GetPseudoAvailability(uiapp, commandItem);
            return commandId is not null && pseudoAvailability;
        } catch {
            return false;
        }
    }

    /// <summary>
    ///     Gets the availability status text for a command
    /// </summary>
    /// <param name="commandItem">The command item to check</param>
    /// <returns>Status text describing command availability</returns>
    public string GetCommandStatus(UIApplication uiapp, PostableCommandItem commandItem) { // TODO: this may be useless
        try {
            var commandId = this.GetCommandId(commandItem);
            var pseudoAvailability = this.GetPseudoAvailability(uiapp, commandItem);
            return commandId is null
                ? "Command not available"
                : pseudoAvailability
                    ? "Command is available"
                    : "Command is disabled";
        } catch {
            return "Command availability unknown";
        }
    }
}