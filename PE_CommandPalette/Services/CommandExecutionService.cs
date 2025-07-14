using System;
using PE_CommandPalette.Models;

namespace PE_CommandPalette.Services
{
    /// <summary>
    /// Service for executing PostableCommand items in Revit
    /// </summary>
    public class CommandExecutionService
    {
        private readonly UIApplication _uiApplication;

        public CommandExecutionService(UIApplication uiApplication)
        {
            _uiApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
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
                // Get the RevitCommandId for the PostableCommand
                RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);
                
                if (commandId == null)
                {
                    ShowError($"Command '{commandItem.Name}' is not available in this context.");
                    return false;
                }

                // Check if the command can be executed
                if (!_uiApplication.CanPostCommand(commandId))
                {
                    ShowError($"Command '{commandItem.Name}' cannot be executed at this time.");
                    return false;
                }

                // Post the command to Revit
                _uiApplication.PostCommand(commandId);

                // Update usage statistics
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
                RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);
                return commandId != null && _uiApplication.CanPostCommand(commandId);
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
                RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(commandItem.Command);
                
                if (commandId == null)
                    return "Command not available";

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
            try
            {
                // Use TaskDialog for error messages
                TaskDialog dialog = new TaskDialog("Command Palette Error")
                {
                    MainContent = message,
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    DefaultButton = TaskDialogResult.Ok
                };
                dialog.Show();
            }
            catch
            {
                // Fallback to system message if TaskDialog fails
                System.Windows.MessageBox.Show(message, "Command Palette Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}