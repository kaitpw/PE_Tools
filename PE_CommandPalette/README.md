# PE Command Palette

A modern command palette for Autodesk Revit that provides quick access to all PostableCommand enumeration values with search and keyboard navigation.

## Features

- **Fast Search**: Fuzzy search through all available Revit commands
- **Keyboard Navigation**: Use arrow keys to navigate and Enter to execute
- **Modern UI**: Dark theme with smooth animations and modern styling
- **Keyboard Shortcuts**: Displays Revit keyboard shortcuts for each command
- **Menu Paths**: Shows where each command can be found in the Revit UI
- **Usage Tracking**: Tracks command usage for better prioritization
- **Performance Optimized**: Virtualized list for smooth scrolling with large command sets

## Architecture

The command palette is built using the MVVM pattern with the following components:

- **Models/PostableCommandItem.cs**: Data model for command items with metadata
- **Services/PostableCommandService.cs**: Singleton service for managing PostableCommand enumeration
- **Services/CommandExecutionService.cs**: Service for executing commands in Revit
- **Services/KeyboardShortcutsService.cs**: Service for parsing Revit keyboard shortcuts XML
- **ViewModels/CommandPaletteViewModel.cs**: Main view model with search and navigation logic
- **Views/CommandPaletteWindow.xaml**: Modern WPF UI with dark theme

## Keyboard Shortcuts Integration

The command palette automatically loads and displays keyboard shortcuts from Revit's KeyboardShortcuts.xml file:

- **Automatic Detection**: Detects the current Revit version and loads the appropriate shortcuts file
- **HTML Entity Decoding**: Properly handles XML entities like `&gt;` and `&amp;`
- **Command Name Override**: Uses official command names from the XML file when available
- **Shortcut Display**: Shows primary keyboard shortcuts in a highlighted badge
- **Path Information**: Displays truncated menu paths with full paths available on hover

### File Location
The keyboard shortcuts file is located at:
```
C:\Users\[username]\AppData\Roaming\Autodesk\Revit\Autodesk Revit [version]\KeyboardShortcuts.xml
```

## Usage

1. **Open Command Palette**: Use the ribbon button or assigned keyboard shortcut
2. **Search Commands**: Type to filter commands by name
3. **Navigate**: Use ↑/↓ arrow keys to move through results
4. **Execute**: Press Enter to execute the selected command
5. **Close**: Press Escape to close the palette

## Search Features

- **Fuzzy Matching**: Finds commands even with partial or misspelled search terms
- **Priority Scoring**: Commands are ranked by:
  - Search relevance score
  - Usage frequency
  - Last used timestamp
- **Real-time Filtering**: Results update as you type

## UI Features

- **Dark Theme**: Modern dark interface that matches contemporary IDEs
- **Smooth Animations**: Subtle animations for better user experience
- **Responsive Design**: Adapts to different window sizes
- **Tooltips**: Hover over paths to see full menu locations
- **Status Bar**: Shows command status and usage information

## Performance

- **Virtualized List**: Only renders visible items for smooth scrolling
- **Background Loading**: Commands load asynchronously to prevent UI blocking
- **Caching**: Shortcuts and command data are cached for fast access
- **Efficient Search**: Optimized search algorithms for large command sets

## Dependencies

- **Autodesk Revit API** (PostableCommand, UIApplication)
- **CommunityToolkit.Mvvm** (MVVM framework)
- **System.Xml.Linq** (XML parsing for shortcuts)
- **Custom command registration beyond PostableCommand**

## Development

The project uses the MVVM pattern with:
- **ObservableObject**: For property change notifications
- **RelayCommand**: For command binding
- **Dependency Injection**: For service management
- **Async/Await**: For non-blocking operations

## Future Enhancements

- Custom command registration
- Command categories and filtering
- User-defined shortcuts
- Command history and favorites
- Integration with external tools
