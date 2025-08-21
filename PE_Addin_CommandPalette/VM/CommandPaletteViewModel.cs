using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PE_Addin_CommandPalette.H;
using PE_Addin_CommandPalette.M;
using PeLib;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace PE_Addin_CommandPalette.VM;

/// <summary>
///     ViewModel for the Command Palette window
/// </summary>
public partial class CommandPaletteViewModel : ObservableObject {
    private readonly Command _executionService;

    public CommandPaletteViewModel(UIApplication UIApplication, Dispatcher uiDispatcher) {
        this._uiapp = UIApplication;
        this._uiDispatcher = uiDispatcher;
        this._executionService = new Command();
        this.FilteredCommands = new ObservableCollection<PostableCommandItem>();

        // Initialize commands asynchronously for better startup performance
        _ = Task.Run(this.LoadCommandsAsync);
    }

    #region Properties

    /// <summary>
    ///     The UI application instance for executing commands
    /// </summary>
    [ObservableProperty] private UIApplication _uiapp;

    private readonly Dispatcher _uiDispatcher;

    /// <summary>
    ///     Current search text
    /// </summary>
    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>
    ///     Currently selected command
    /// </summary>
    [ObservableProperty] private PostableCommandItem _selectedCommand;

    /// <summary>
    ///     Currently selected index in the filtered list
    /// </summary>
    [ObservableProperty] private int _selectedIndex = -1;

    /// <summary>
    ///     Whether the command list is currently loading
    /// </summary>
    [ObservableProperty] private bool _isLoading = true;

    /// <summary>
    ///     Whether a command is currently being executed
    /// </summary>
    [ObservableProperty] private bool _isExecutingCommand;

    /// <summary>
    ///     Filtered list of commands based on search text
    /// </summary>
    public ObservableCollection<PostableCommandItem> FilteredCommands { get; }

    /// <summary>
    ///     Status text for the currently selected command
    /// </summary>
    public string CommandStatus =>
        this.SelectedCommand == null
        ? "No command selected"
        : this._executionService.GetStatus(this.Uiapp, this.SelectedCommand.Command);

    #endregion

    #region Commands

    /// <summary>
    ///     Executes the currently selected command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteSelectedCommand))]
    private async Task ExecuteSelectedCommandAsync() {
        if (this.SelectedCommand == null)
            return;

        this.IsExecutingCommand = true;

        (bool success, Exception error) = await Task.Run(() =>
            this._executionService.Execute(this.Uiapp, this.SelectedCommand.Command));
        if (error is not null) throw error; // TODO: come back to the error handling here
        if (success) PostableCommandHelper.Instance.UpdateCommandUsage(this.SelectedCommand.Command);

    }

    /// <summary>
    ///     Moves selection up in the list
    /// </summary>
    [RelayCommand]
    private void MoveSelectionUp() {
        if (this.SelectedIndex > 0) this.SelectedIndex--;
    }

    /// <summary>
    ///     Moves selection down in the list
    /// </summary>
    [RelayCommand]
    private void MoveSelectionDown() {
        if (this.SelectedIndex < this.FilteredCommands.Count - 1) this.SelectedIndex++;
    }

    /// <summary>
    ///     Clears the search text
    /// </summary>
    [RelayCommand]
    private void ClearSearch() => this.SearchText = string.Empty;

    #endregion

    #region Methods

    /// <summary>
    ///     Loads commands asynchronously
    /// </summary>
    private async Task LoadCommandsAsync() =>
        await Task.Run(() => {
            // Load commands on background thread
            var commands = PostableCommandHelper.Instance.GetAllCommands();

            // Update UI on main thread
            this._uiDispatcher.Invoke(() => {
                this.FilteredCommands.Clear();
                foreach (var command in commands) // Show first 50 initially for performance
                    this.FilteredCommands.Add(command);

                // Select first item by default
                if (this.FilteredCommands.Count > 0) this.SelectedIndex = 0;

                this.IsLoading = false;
            });
        });

    /// <summary>
    ///     Filters commands based on current search text
    /// </summary>
    private void FilterCommands() {
        var filtered = PostableCommandHelper.Instance.FilterCommands(this.SearchText);

        this.FilteredCommands.Clear();
        foreach (var command in filtered) // Limit results for performance
            this.FilteredCommands.Add(command);

        // Reset selection to first item
        this.SelectedIndex = this.FilteredCommands.Count > 0 ? 0 : -1;
    }

    /// <summary>
    ///     Checks if the selected command can be executed
    /// </summary>
    private bool CanExecuteSelectedCommand() =>
        this.SelectedCommand != null
        && this._executionService.IsAvailable(this.Uiapp, this.SelectedCommand.Command)
        && !this.IsExecutingCommand;

    #endregion

    #region Property Change Handlers

    /// <summary>
    ///     Handles changes to the SearchText property
    /// </summary>
    partial void OnSearchTextChanged(string value) => this.FilterCommands();

    /// <summary>
    ///     Handles changes to the SelectedCommand property
    /// </summary>
    partial void OnSelectedCommandChanged(PostableCommandItem value) {
        // Clear previous selection
        if (this.FilteredCommands.Any(cmd => cmd != value)) {
            foreach (var cmd in this.FilteredCommands)
                cmd.IsSelected = false;
        }

        // Set new selection
        if (value != null) value.IsSelected = true;

        // Notify that CommandStatus has changed
        this.OnPropertyChanged(nameof(this.CommandStatus));
    }

    /// <summary>
    ///     Handles changes to the SelectedIndex property
    /// </summary>
    partial void OnSelectedIndexChanged(int value) {
        // Update selected command based on index
        if (value >= 0 && value < this.FilteredCommands.Count)
            this.SelectedCommand = this.FilteredCommands[value];
        else
            this.SelectedCommand = null;
    }

    #endregion
}

/// <summary>
///     Event arguments for command execution completion
/// </summary>
public class CommandExecutionCompletedEventArgs : EventArgs {
    public PostableCommandItem Command { get; set; }
    public bool Success { get; set; }
    public Exception Error { get; set; }
}