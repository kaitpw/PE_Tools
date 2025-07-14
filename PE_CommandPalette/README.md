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

## Architecture

### Components

- **Models/PostableCommandItem.cs**: Data model for command items with metadata
- **Services/PostableCommandService.cs**: Singleton service for managing
  PostableCommand enumeration
- **Services/CommandExecutionService.cs**: Service for executing commands in
  Revit
- **ViewModels/CommandPaletteViewModel.cs**: MVVM view model with search and
  navigation logic
- **Views/CommandPaletteWindow.xaml**: WPF window with modern styling
- **Views/CommandPaletteWindow.xaml.cs**: Code-behind with keyboard handling

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

## Future Enhancements

- Custom command registration beyond PostableCommand
- Command categories and grouping
- Keyboard shortcut display
- Command history persistence
- Theme customization
- Plugin command integration
