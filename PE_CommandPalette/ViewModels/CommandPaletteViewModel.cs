using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PE_CommandPalette.Models;
using PE_CommandPalette.Services;

namespace PE_CommandPalette.ViewModels
{
    /// <summary>
    /// ViewModel for the Command Palette window
    /// </summary>
    public partial class CommandPaletteViewModel : ObservableObject
    {
        private readonly CommandExecutionService _executionService;

        /// <summary>
        /// Event fired when a command execution completes
        /// </summary>
        public event EventHandler<CommandExecutionCompletedEventArgs> CommandExecutionCompleted;

        public CommandPaletteViewModel(UIApplication UIApplication)
        {
            uiapp = UIApplication;
            _executionService = new CommandExecutionService(uiapp);
            FilteredCommands = new ObservableCollection<PostableCommandItem>();

            // Initialize commands asynchronously for better startup performance
            Task.Run(LoadCommandsAsync);
        }

        #region Properties

        /// <summary>
        /// The UI application instance for executing commands
        /// </summary>
        [ObservableProperty]
        private UIApplication uiapp;

        /// <summary>
        /// Current search text
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>
        /// Currently selected command
        /// </summary>
        [ObservableProperty]
        private PostableCommandItem _selectedCommand;

        /// <summary>
        /// Currently selected index in the filtered list
        /// </summary>
        [ObservableProperty]
        private int _selectedIndex = -1;

        /// <summary>
        /// Whether the command list is currently loading
        /// </summary>
        [ObservableProperty]
        private bool _isLoading = true;

        /// <summary>
        /// Whether a command is currently being executed
        /// </summary>
        [ObservableProperty]
        private bool _isExecutingCommand = false;

        /// <summary>
        /// Filtered list of commands based on search text
        /// </summary>
        public ObservableCollection<PostableCommandItem> FilteredCommands { get; }

        /// <summary>
        /// Status text for the currently selected command
        /// </summary>
        public string CommandStatus
        {
            get
            {
                if (SelectedCommand == null)
                    return "No command selected";

                return _executionService.GetCommandStatus(SelectedCommand);
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Executes the currently selected command
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteSelectedCommand))]
        private async Task ExecuteSelectedCommandAsync()
        {
            if (SelectedCommand == null)
                return;

            IsExecutingCommand = true;

            try
            {
                // Execute command on background thread to avoid blocking UI
                bool success = await Task.Run(() =>
                    _executionService.ExecuteCommand(SelectedCommand)
                );

                // Fire completion event
                CommandExecutionCompleted?.Invoke(
                    this,
                    new CommandExecutionCompletedEventArgs
                    {
                        Command = SelectedCommand,
                        Success = success,
                    }
                );
            }
            catch (Exception ex)
            {
                // Fire completion event with error
                CommandExecutionCompleted?.Invoke(
                    this,
                    new CommandExecutionCompletedEventArgs
                    {
                        Command = SelectedCommand,
                        Success = false,
                        Error = ex,
                    }
                );
            }
            finally
            {
                IsExecutingCommand = false;
            }
        }

        /// <summary>
        /// Moves selection up in the list
        /// </summary>
        [RelayCommand]
        private void MoveSelectionUp()
        {
            if (SelectedIndex > 0)
            {
                SelectedIndex--;
            }
        }

        /// <summary>
        /// Moves selection down in the list
        /// </summary>
        [RelayCommand]
        private void MoveSelectionDown()
        {
            if (SelectedIndex < FilteredCommands.Count - 1)
            {
                SelectedIndex++;
            }
        }

        /// <summary>
        /// Clears the search text
        /// </summary>
        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads commands asynchronously
        /// </summary>
        private async Task LoadCommandsAsync()
        {
            await Task.Run(() =>
            {
                // Load commands on background thread
                var commands = PostableCommandService.Instance.GetAllCommands();

                // Update UI on main thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    FilteredCommands.Clear();
                    foreach (var command in commands) // Show first 50 initially for performance
                    {
                        FilteredCommands.Add(command);
                    }

                    // Select first item by default
                    if (FilteredCommands.Count > 0)
                    {
                        SelectedIndex = 0;
                    }

                    IsLoading = false;
                });
            });
        }

        /// <summary>
        /// Filters commands based on current search text
        /// </summary>
        private void FilterCommands()
        {
            var filtered = PostableCommandService.Instance.FilterCommands(SearchText);

            FilteredCommands.Clear();
            foreach (var command in filtered) // Limit results for performance
            {
                FilteredCommands.Add(command);
            }

            // Reset selection to first item
            SelectedIndex = FilteredCommands.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// Checks if the selected command can be executed
        /// </summary>
        private bool CanExecuteSelectedCommand()
        {
            return SelectedCommand != null
                && _executionService.IsCommandAvailable(SelectedCommand)
                && !IsExecutingCommand;
        }

        #endregion

        #region Property Change Handlers

        /// <summary>
        /// Handles changes to the SearchText property
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            FilterCommands();
        }

        /// <summary>
        /// Handles changes to the SelectedCommand property
        /// </summary>
        partial void OnSelectedCommandChanged(PostableCommandItem value)
        {
            // Clear previous selection
            if (FilteredCommands.Any(cmd => cmd != value))
            {
                foreach (var cmd in FilteredCommands)
                {
                    cmd.IsSelected = false;
                }
            }

            // Set new selection
            if (value != null)
            {
                value.IsSelected = true;
            }

            // Notify that CommandStatus has changed
            OnPropertyChanged(nameof(CommandStatus));
        }

        /// <summary>
        /// Handles changes to the SelectedIndex property
        /// </summary>
        partial void OnSelectedIndexChanged(int value)
        {
            // Update selected command based on index
            if (value >= 0 && value < FilteredCommands.Count)
            {
                SelectedCommand = FilteredCommands[value];
            }
            else
            {
                SelectedCommand = null;
            }

            // Raise event to scroll selected item into view
            ScrollIntoViewRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event fired when the selected index changes, to scroll the selected item into view.
        /// </summary>
        public event EventHandler ScrollIntoViewRequested;

        #endregion
    }

    /// <summary>
    /// Event arguments for command execution completion
    /// </summary>
    public class CommandExecutionCompletedEventArgs : EventArgs
    {
        public PostableCommandItem Command { get; set; }
        public bool Success { get; set; }
        public Exception Error { get; set; }
    }
}
