using System;
using PE_CommandPalette.M;

namespace PE_CommandPalette.H
{
    /// <summary>
    /// Service for executing PostableCommand items in Revit
    /// </summary>
    public class CommandExecutionHelper
    {
        private readonly UIApplication _uiApplication;

        public CommandExecutionHelper(UIApplication uiApplication)
        {
            _uiApplication =
                uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
        }

        /// <summary>
        /// Executes the specified PostableCommand
        /// </summary>
        /// <param name="commandItem">The command item to execute</param>
        /// <returns>True if execution was successful, false otherwise</returns>
        public bool ExecuteCommand(PostableCommandItem commandItem)
        {
            if (commandItem == null)
                return false;

            try
            {
                RevitCommandId commandId = null;
                if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                    commandId = RevitCommandId.LookupCommandId(commandItem.CustomCommandId);
                else
                    commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);

                if (commandId == null)
                {
                    ShowError($"Command '{commandItem.Name}' is not available in this context.");
                    return false;
                }

                // For custom commands, CanPostCommand may always return true or may not be reliable.
                var canPost = _uiApplication.CanPostCommand(commandId);
                if (!canPost)
                {
                    ShowError($"Command '{commandItem.Name}' cannot be executed at this time.");
                    return false;
                }

                _uiApplication.PostCommand(commandId);
                PostableCommandService.Instance.UpdateCommandUsage(commandItem);

                return true;
            }
            catch (Exception ex)
            {
                ShowError($"Error executing command '{commandItem.Name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a command is available for execution
        /// </summary>
        /// <param name="commandItem">The command item to check</param>
        /// <returns>True if the command is available, false otherwise</returns>
        public bool IsCommandAvailable(PostableCommandItem commandItem)
        {
            if (commandItem == null)
                return false;

            try
            {
                RevitCommandId commandId = null;
                if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                    commandId = RevitCommandId.LookupCommandId(commandItem.CustomCommandId);
                else
                    commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);

                // For custom commands, CanPostCommand may not be meaningful, so just check commandId.
                return commandId != null
                    && (
                        string.IsNullOrEmpty(commandItem.CustomCommandId)
                            ? _uiApplication.CanPostCommand(commandId)
                            : true
                    );
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the availability status text for a command
        /// </summary>
        /// <param name="commandItem">The command item to check</param>
        /// <returns>Status text describing command availability</returns>
        public string GetCommandStatus(PostableCommandItem commandItem)
        {
            if (commandItem == null)
                return "Invalid command";

            try
            {
                RevitCommandId commandId = null;
                if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                    commandId = RevitCommandId.LookupCommandId(commandItem.CustomCommandId);
                else
                    commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);

                if (commandId == null)
                    return "Command not available";

                if (!string.IsNullOrEmpty(commandItem.CustomCommandId))
                    return "Ready"; // For custom commands, assume always ready

                if (!_uiApplication.CanPostCommand(commandId))
                    return "Command disabled";

                return "Ready";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        private void ShowError(string message)
        {
            TaskDialog dialog = new TaskDialog("Command Palette Error")
            {
                MainContent = message,
                CommonButtons = TaskDialogCommonButtons.Ok,
                DefaultButton = TaskDialogResult.Ok,
            };
            dialog.Show();
        }
    }
}
