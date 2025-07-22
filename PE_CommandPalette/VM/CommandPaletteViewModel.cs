using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PE_CommandPalette.H;
using PE_CommandPalette.M;

namespace PE_CommandPalette.VM
{
    /// <summary>
    /// ViewModel for the Command Palette window
    /// </summary>
    public partial class CommandPaletteViewModel : ObservableObject
    {
        private readonly CommandExecutionHelper _executionService;

        public CommandPaletteViewModel(UIApplication UIApplication, Dispatcher uiDispatcher)
        {
            _uiapp = UIApplication;
            _uiDispatcher = uiDispatcher;
            _executionService = new CommandExecutionHelper(_uiapp);
            FilteredCommands = new ObservableCollection<PostableCommandItem>();

            // Initialize commands asynchronously for better startup performance
            Task.Run(LoadCommandsAsync);
        }

        #region Properties

        /// <summary>
        /// The UI application instance for executing commands
        /// </summary>
        [ObservableProperty]
        private UIApplication _uiapp;

        private readonly Dispatcher _uiDispatcher;

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

            bool success = await Task.Run(() => _executionService.ExecuteCommand(SelectedCommand));
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
                var commands = PostableCommandHelper.Instance.GetAllCommands();

                // Update UI on main thread
                _uiDispatcher.Invoke(() =>
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
            var filtered = PostableCommandHelper.Instance.FilterCommands(SearchText);

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
        }

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
