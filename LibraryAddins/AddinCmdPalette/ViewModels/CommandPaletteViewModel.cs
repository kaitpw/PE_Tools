using AddinCmdPalette.Helpers;
using AddinCmdPalette.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeLib;
using System.Collections.ObjectModel;

namespace AddinCmdPalette.ViewModels;

/// <summary>
///     ViewModel for the Command Palette window
/// </summary>
public partial class CommandPaletteViewModel : ObservableObject {
    /// <summary> Whether a command is currently being executed </summary>
    [ObservableProperty] private bool _isExecutingCommand;

    /// <summary> Current search text </summary>
    [ObservableProperty] private string _searchText = string.Empty;

#nullable enable
    /// <summary> Currently selected command </summary>
    [ObservableProperty] private PostableCommandItem? _selectedCommand;
#nullable disable

    /// <summary> Currently selected index in the filtered list </summary>
    [ObservableProperty] private int _selectedIndex = -1;

    /// <summary> The UI application instance for executing commands </summary>
    [ObservableProperty] private UIApplication _uiapp;

    public CommandPaletteViewModel(UIApplication uiApp) {
        this._uiapp = uiApp;
        this.FilteredCommands = new ObservableCollection<PostableCommandItem>();

        // Load commands synchronously for immediate display
        var commands = PostableCommandHelper.Instance.GetAllCommands();
        foreach (var command in commands)
            this.FilteredCommands.Add(command);

        // Select first item by default
        if (this.FilteredCommands.Count > 0) 
            this.SelectedIndex = 0;
    }

    /// <summary> Filtered list of commands based on search text </summary>
    public ObservableCollection<PostableCommandItem> FilteredCommands { get; }

    /// <summary> Status text for the currently selected command </summary>
    public string CommandStatus =>
        this.SelectedCommand == null
            ? "No command selected"
            : Commands.GetStatus(this.Uiapp, this.SelectedCommand.Command);


    [RelayCommand(CanExecute = nameof(CanExecuteSelectedCommand))]
    private async Task ExecuteSelectedCommandAsync() {
        if (this.SelectedCommand == null) return;
        this.IsExecutingCommand = true;

        var (success, error) = await Task.Run(() =>
            Commands.Execute(this.Uiapp, this.SelectedCommand.Command));
        if (error is not null) throw error; // TODO: come back to the error handling here
        if (success) PostableCommandHelper.Instance.UpdateCommandUsage(this.SelectedCommand.Command);
    }

    [RelayCommand]
    private void MoveSelectionUp() {
        if (this.SelectedIndex > 0) this.SelectedIndex--;
    }

    [RelayCommand]
    private void MoveSelectionDown() {
        if (this.SelectedIndex < this.FilteredCommands.Count - 1) this.SelectedIndex++;
    }

    [RelayCommand]
    private void ClearSearch() => this.SearchText = string.Empty;


    #region Methods

    /// <summary>
    ///     Filters commands based on current search text
    /// </summary>
    private void FilterCommands() {
        if (string.IsNullOrWhiteSpace(this.SearchText)) {
            // Show all commands when no search text
            var allCommands = PostableCommandHelper.Instance.GetAllCommands();
            this.FilteredCommands.Clear();
            foreach (var command in allCommands)
                this.FilteredCommands.Add(command);
        } else {
            // Filter commands based on search
            var filtered = PostableCommandHelper.Instance.FilterCommands(this.SearchText);
            this.FilteredCommands.Clear();
            foreach (var command in filtered)
                this.FilteredCommands.Add(command);
        }

        // Reset selection to first item
        this.SelectedIndex = this.FilteredCommands.Count > 0 ? 0 : -1;
    }

    /// <summary>
    ///     Checks if the selected command can be executed
    /// </summary>
    private bool CanExecuteSelectedCommand() =>
        this.SelectedCommand != null
        && Commands.IsAvailable(this.Uiapp, this.SelectedCommand.Command)
        && !this.IsExecutingCommand;

    #endregion

    #region Property Change Handlers

    partial void OnSearchTextChanged(string value) => this.FilterCommands();

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

    partial void OnSelectedIndexChanged(int value) {
        // Update selected command based on index
        if (value >= 0 && value < this.FilteredCommands.Count)
            this.SelectedCommand = this.FilteredCommands[value];
        else
            this.SelectedCommand = null;
    }

    #endregion
}