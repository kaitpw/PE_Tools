# PE Command Palette

A modern command palette for Revit that provides quick access to all
PostableCommand enumeration values with search and keyboard navigation.

## Features

- **Fast Search**: Real-time filtering with fuzzy matching
- **Keyboard Navigation**: Arrow keys to navigate, Enter to execute, Escape to
  close
- **Usage Tracking**: Tracks command usage frequency for better prioritization
- **Modern UI**: VS Code-style command palette with dark theme
- **Performance Optimized**: Lazy loading and virtualized scrolling for fast
  startup
- **MVVM Architecture**: Built with CommunityToolkit.MVVM for clean, maintainable code

## Architecture

### Components

- **Models/PostableCommandItem.cs**: Data model for command items with metadata
- **Services/PostableCommandService.cs**: Singleton service for managing
  PostableCommand enumeration
- **Services/CommandExecutionService.cs**: Service for executing commands in
  Revit
- **ViewModels/CommandPaletteViewModel.cs**: MVVM view model with search and
  navigation logic using CommunityToolkit.MVVM
- **Views/CommandPaletteWindow.xaml**: WPF window with modern styling
- **Views/CommandPaletteWindow.xaml.cs**: Code-behind with keyboard handling

### CommunityToolkit.MVVM Implementation

This project has been refactored to use **CommunityToolkit.MVVM**, a modern MVVM library that simplifies development through source generators and built-in functionality.

#### Key Concepts Used:

##### 1. **ObservableObject** - Base Class for ViewModels
- **What it does**: Automatically implements `INotifyPropertyChanged` and provides helper methods
- **Benefits**: No more manual property change notifications, cleaner code
- **Usage**: `public partial class CommandPaletteViewModel : ObservableObject`

##### 2. **ObservableProperty** - Automatic Property Generation
- **What it does**: Source generator that creates full properties from private fields
- **Benefits**: Reduces boilerplate code, automatic change notifications
- **Usage**: `[ObservableProperty] private string _searchText = string.Empty;`
- **Generated**: Creates `SearchText` property with getter, setter, and change notifications

##### 3. **RelayCommand** - Modern Command Implementation
- **What it does**: Provides type-safe command implementation with async support
- **Benefits**: No manual `ICommand` implementation, supports async operations
- **Usage**: `[RelayCommand(CanExecute = nameof(CanExecuteSelectedCommand))] private void ExecuteSelectedCommand()`
- **Generated**: Creates `ExecuteSelectedCommandCommand` property

##### 4. **Partial Methods** - Property Change Handlers
- **What it does**: Automatically generated methods for handling property changes
- **Benefits**: Clean separation of concerns, automatic invocation
- **Usage**: `partial void OnSearchTextChanged(string value) { FilterCommands(); }`

#### Before vs After Comparison:

**Before (Manual MVVM):**
```csharp
public class CommandPaletteViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    
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
    
    private ICommand _executeCommand;
    public ICommand ExecuteCommand => _executeCommand ??= new RelayCommand(ExecuteSelectedCommand, CanExecuteSelectedCommand);
    
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

**After (CommunityToolkit.MVVM):**
```csharp
public partial class CommandPaletteViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [RelayCommand(CanExecute = nameof(CanExecuteSelectedCommand))]
    private void ExecuteSelectedCommand()
    {
        // Implementation
    }
    
    partial void OnSearchTextChanged(string value)
    {
        FilterCommands();
    }
}
```

#### Benefits of the Refactoring:

1. **Reduced Code**: ~60% less boilerplate code
2. **Better Performance**: Source generators create optimized code at compile time
3. **Type Safety**: Strongly typed commands and properties
4. **Maintainability**: Cleaner, more readable code
5. **Modern Patterns**: Uses latest .NET and MVVM best practices

### Key Features

#### Search Algorithm

- Exact match: 100 points
- Starts with search: 90 points
- Contains search: 70 points
- Fuzzy match: 0-50 points based on character matching

#### Performance Optimizations

- Singleton pattern for command service
- Lazy initialization of command list
- Virtualized ListBox for large datasets
- Debounced search input (150ms delay)
- Limited result sets (100 items max)

#### Keyboard Shortcuts

- `↑/↓`: Navigate selection
- `Enter`: Execute selected command
- `Escape`: Clear search or close window
- `Tab`: Disabled to maintain focus

## Usage

The command palette is accessible via:

1. **Ribbon Button**: "Command Palette" button in the PE TOOLS tab
2. **Hotkey Support**: Ready for Ctrl+K binding (implementation in main project)

## Integration

This shared project integrates with PE_Tools via:

- `PE_Tools/cmdCommandPalette.cs`: External command entry point
- `PE_Tools/App.cs`: Ribbon button registration
- Shared project reference in PE_Tools.csproj

## Dependencies

- Autodesk Revit API (PostableCommand, UIApplication)
- WPF (.NET Framework 4.8 / .NET 8.0-windows)
- System.Windows.Interop (for Alt+Tab hiding)
- **CommunityToolkit.MVVM** (8.2.2) - Modern MVVM library

## Future Enhancements

- Custom command registration beyond PostableCommand
- Command categories and grouping
- Keyboard shortcut display
- Command history persistence
- Theme customization
- Plugin command integration
- Async command execution with progress indicators
- Command validation and error handling
