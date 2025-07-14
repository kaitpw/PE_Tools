using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PE_CommandPalette.Models;
using PE_CommandPalette.Services;

namespace PE_CommandPalette.ViewModels
{
    /// <summary>
    /// ViewModel for the Command Palette window
    /// </summary>
    public class CommandPaletteViewModel : INotifyPropertyChanged
    {
        private readonly CommandExecutionService _executionService;
        private string _searchText = string.Empty;
        private PostableCommandItem _selectedCommand;
        private int _selectedIndex = -1;
        private bool _isLoading = true;

        public CommandPaletteViewModel(UIApplication uiApplication)
        {
            _executionService = new CommandExecutionService(uiApplication);
            FilteredCommands = new ObservableCollection<PostableCommandItem>();
            
            // Initialize commands asynchronously for better startup performance
            Task.Run(LoadCommandsAsync);
        }

        #region Properties

        /// <summary>
        /// Current search text
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    FilterCommands();
                }
            }
        }

        /// <summary>
        /// Currently selected command
        /// </summary>
        public PostableCommandItem SelectedCommand
        {
            get => _selectedCommand;
            set
            {
                if (_selectedCommand != value)
                {
                    // Clear previous selection
                    if (_selectedCommand != null)
                        _selectedCommand.IsSelected = false;

                    _selectedCommand = value;

                    // Set new selection
                    if (_selectedCommand != null)
                        _selectedCommand.IsSelected = true;

                    OnPropertyChanged(nameof(SelectedCommand));
                    OnPropertyChanged(nameof(CommandStatus));
                }
            }
        }

        /// <summary>
        /// Currently selected index in the filtered list
        /// </summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    OnPropertyChanged(nameof(SelectedIndex));
                    
                    // Update selected command based on index
                    if (_selectedIndex >= 0 && _selectedIndex < FilteredCommands.Count)
                    {
                        SelectedCommand = FilteredCommands[_selectedIndex];
                    }
                    else
                    {
                        SelectedCommand = null;
                    }
                }
            }
        }

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

        /// <summary>
        /// Whether the command list is currently loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        /// <summary>
        /// Total number of available commands
        /// </summary>
        public int TotalCommandCount => PostableCommandService.Instance.GetAllCommands().Count;

        #endregion

        #region Commands

        private ICommand _executeCommand;
        public ICommand ExecuteCommand => _executeCommand ??= new RelayCommand(ExecuteSelectedCommand, CanExecuteSelectedCommand);

        private ICommand _moveUpCommand;
        public ICommand MoveUpCommand => _moveUpCommand ??= new RelayCommand(MoveSelectionUp);

        private ICommand _moveDownCommand;
        public ICommand MoveDownCommand => _moveDownCommand ??= new RelayCommand(MoveSelectionDown);

        private ICommand _clearSearchCommand;
        public ICommand ClearSearchCommand => _clearSearchCommand ??= new RelayCommand(ClearSearch);

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
                    foreach (var command in commands.Take(50)) // Show first 50 initially for performance
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
            foreach (var command in filtered.Take(100)) // Limit results for performance
            {
                FilteredCommands.Add(command);
            }

            // Reset selection to first item
            SelectedIndex = FilteredCommands.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// Executes the currently selected command
        /// </summary>
        private void ExecuteSelectedCommand()
        {
            if (SelectedCommand != null)
            {
                _executionService.ExecuteCommand(SelectedCommand);
            }
        }

        /// <summary>
        /// Checks if the selected command can be executed
        /// </summary>
        private bool CanExecuteSelectedCommand()
        {
            return SelectedCommand != null && _executionService.IsCommandAvailable(SelectedCommand);
        }

        /// <summary>
        /// Moves selection up in the list
        /// </summary>
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
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Simple RelayCommand implementation for MVVM
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}