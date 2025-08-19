using PE_Addin_CommandPalette.M;

namespace PE_Addin_CommandPalette.H;

/// <summary>
///     Service for executing PostableCommand items in Revit
/// </summary>
public class CommandExecutionHelper {
    private readonly UIApplication _uiApplication;

    public CommandExecutionHelper(UIApplication uiApplication) =>
        this._uiApplication =
            uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));

    /// <summary>
    ///     Executes the specified PostableCommand
    /// </summary>
    /// <param name="commandItem">The command item to execute</param>
    /// <returns>True if execution was successful, false otherwise</returns>
    public bool ExecuteCommand(PostableCommandItem commandItem) {
        if (commandItem == null)
            return false;

        try {
            RevitCommandId commandId = null;
            if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                commandId = RevitCommandId.LookupCommandId(commandItem.CustomCommandId);
            else
                commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);

            if (commandId == null) {
                throw new InvalidOperationException(
                    $"Command '{commandItem.Name}' is not available in this context."
                );
            }

            if (!this._uiApplication.CanPostCommand(commandId)) {
                throw new InvalidOperationException(
                    $"Command '{commandItem.Name}' cannot be executed at this time."
                );
            }

            this._uiApplication.PostCommand(commandId);
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
    public bool IsCommandAvailable(PostableCommandItem commandItem) {
        if (commandItem == null)
            return false;

        try {
            RevitCommandId commandId = null;
            if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                commandId = RevitCommandId.LookupCommandId(commandItem.CustomCommandId);
            else
                commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);

            // For custom commands, CanPostCommand may not be meaningful, so just check commandId.
            return commandId != null
                   && (
                       string.IsNullOrEmpty(commandItem.CustomCommandId)
                           ? this._uiApplication.CanPostCommand(commandId)
                           : true
                   );
        } catch {
            return false;
        }
    }

    /// <summary>
    ///     Gets the availability status text for a command
    /// </summary>
    /// <param name="commandItem">The command item to check</param>
    /// <returns>Status text describing command availability</returns>
    public string GetCommandStatus(PostableCommandItem commandItem) {
        if (commandItem == null)
            return "Invalid command";

        try {
            RevitCommandId commandId = null;
            if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                commandId = RevitCommandId.LookupCommandId(commandItem.CustomCommandId);
            else
                commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);

            if (commandId == null)
                return "Command not available";

            if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                return "Ready"; // For custom commands, assume always ready

            if (!this._uiApplication.CanPostCommand(commandId))
                return "Command disabled";

            return "Ready";
        } catch (Exception ex) {
            return $"Error: {ex.Message}";
        }
    }
}