# Wpf.Ui Development Guide for PE_Tools

## TL;DR - Critical Requirements

**This project successfully uses Wpf.Ui 4.0.3 in a Revit add-in context. Key
differences from standard WPF apps:**

1. ✅ **Assembly resolver required** - Revit doesn't use standard assembly
   loading, so we manually resolve Wpf.Ui DLLs in `App.cs`
2. ✅ **No App.xaml** - Resources loaded at Window/UserControl level via
   `MergedDictionaries`
3. ✅ **URI namespace only** - Always use
   `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"`, never CLR namespaces
4. ✅ **Resource merge pattern** - Each XAML file must merge
   `WpfUiResources.xaml` in its Resources section
5. ❌ **No manual resource loading** - Don't load resources in code-behind, use
   XAML declarations

**Working Example:**

```xml
<Window xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Window.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="WpfUiResources.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Window.Resources>
    <Grid>
        <ui:DynamicScrollBar ... />
    </Grid>
</Window>
```

---

## Architecture Overview

### Why Assembly Resolution is Required

Revit add-ins load in a special AppDomain that doesn't follow standard .NET
assembly probing rules. When XAML tries to instantiate Wpf.Ui controls, the
runtime can't find the DLLs even though they're in the same directory.

**Solution:** Custom `AssemblyResolve` event handler in `App.cs`
(IExternalApplication)

```csharp
public Result OnStartup(UIControlledApplication app) {
    // CRITICAL: Set up assembly resolver FIRST
    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    
    // ... rest of startup code
}

private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
    var assemblyName = new AssemblyName(args.Name);
    
    // Handle Wpf.Ui assemblies explicitly
    if (assemblyName.Name != "Wpf.Ui" && 
        assemblyName.Name != "Wpf.Ui.Abstractions") {
        return null;
    }

    // Load from add-in directory
    var addinPath = typeof(App).Assembly.Location;
    var addinDirectory = Path.GetDirectoryName(addinPath);
    var assemblyPath = Path.Combine(addinDirectory, $"{assemblyName.Name}.dll");
    
    if (File.Exists(assemblyPath)) {
        return Assembly.LoadFrom(assemblyPath);
    }
    
    return null;
}

public Result OnShutdown(UIControlledApplication a) {
    // Clean up the resolver
    AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
    return Result.Succeeded;
}
```

### Resource Dictionary Structure

Since we have no `App.xaml`, we created a shared resource dictionary that
initializes Wpf.Ui properly:

**File:** `LibraryAddins/AddinCmdPalette/Core/WpfUiResources.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    
    <!-- Merge Wpf.Ui base theme and control dictionaries -->
    <ResourceDictionary.MergedDictionaries>
        <ui:ThemesDictionary Theme="Dark" />
        <ui:ControlsDictionary />
    </ResourceDictionary.MergedDictionaries>
    
    <!-- Custom color overrides (optional) -->
    <SolidColorBrush x:Key="BackgroundFillColorPrimaryBrush" Color="#18181B" />
    <SolidColorBrush x:Key="TextFillColorPrimaryBrush" Color="#FAFAFA" />
    <!-- ... more custom colors -->
</ResourceDictionary>
```

**Key Points:**

- `ThemesDictionary` must come BEFORE `ControlsDictionary`
- Custom color overrides come AFTER base dictionaries
- Use `Theme="Dark"` or `Theme="Light"` depending on your needs

---

## Creating New WPF Windows/Controls

### Pattern for Windows

```xml
<Window x:Class="AddinCmdPalette.Core.MyNewWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        xmlns:local="clr-namespace:AddinCmdPalette.Core">

    <Window.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- REQUIRED: Merge WpfUiResources.xaml -->
                <ResourceDictionary Source="WpfUiResources.xaml" />
            </ResourceDictionary.MergedDictionaries>
            
            <!-- Your local resources here -->
            <local:MyConverter x:Key="MyConverter" />
        </ResourceDictionary>
    </Window.Resources>

    <Grid Background="{DynamicResource BackgroundFillColorPrimaryBrush}">
        <ui:Button Content="Click Me" />
    </Grid>
</Window>
```

### Pattern for UserControls

```xml
<UserControl x:Class="AddinCmdPalette.Core.MyControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">

    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="WpfUiResources.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>

    <Border Background="{DynamicResource BackgroundFillColorPrimaryBrush}">
        <ui:SymbolIcon Symbol="Fluent24" />
    </Border>
</UserControl>
```

### Code-Behind Pattern

**DO NOT** manually load resources in code-behind. This pattern is WRONG:

```csharp
// ❌ WRONG - Don't do this
public MyWindow() {
    InitializeComponent();
    LoadResources(); // BAD!
}

private void LoadResources() {
    var uri = new Uri("pack://...");
    var dict = Application.LoadComponent(uri);
    Resources.MergedDictionaries.Add(dict);
}
```

**Correct pattern:**

```csharp
// ✅ CORRECT - Resources load automatically from XAML
public MyWindow() {
    InitializeComponent(); // This merges resources from XAML
    DataContext = viewModel;
}
```

---

## Using Wpf.Ui Controls

### Namespace Declaration

**Always use the URI namespace:**

```xml
<!-- ✅ CORRECT -->
xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"

<!-- ❌ WRONG - Don't use CLR namespace -->
xmlns:ui="clr-namespace:Wpf.Ui.Controls;assembly=WPF-UI"
```

### Common Controls

```xml
<!-- Buttons -->
<ui:Button Content="Click Me" />

<!-- Icons -->
<ui:SymbolIcon Symbol="Fluent24" />

<!-- Scrollbar (for custom templates) -->
<ui:DynamicScrollBar Orientation="Vertical" />

<!-- Cards -->
<ui:Card>
    <StackPanel>
        <TextBlock Text="Title" />
        <TextBlock Text="Content" />
    </StackPanel>
</ui:Card>
```

### Using Dynamic Resources

Wpf.Ui provides many theme-aware brushes:

```xml
<Border Background="{DynamicResource BackgroundFillColorPrimaryBrush}"
        BorderBrush="{DynamicResource ControlStrokeColorDefaultBrush}">
    <TextBlock Foreground="{DynamicResource TextFillColorPrimaryBrush}" 
               Text="Themed Text" />
</Border>
```

**Common Resource Keys:**

- `BackgroundFillColorPrimaryBrush` - Main background
- `BackgroundFillColorSecondaryBrush` - Secondary/hover background
- `TextFillColorPrimaryBrush` - Primary text
- `TextFillColorSecondaryBrush` - Secondary/dimmed text
- `TextFillColorTertiaryBrush` - Tertiary/hint text
- `ControlStrokeColorDefaultBrush` - Borders/strokes

---

## Troubleshooting

### Error: "Could not load file or assembly 'Wpf.Ui'"

**Cause:** Assembly resolver not set up or set up too late

**Solution:** Ensure `AssemblyResolve` handler is registered FIRST in
`OnStartup()`:

```csharp
public Result OnStartup(UIControlledApplication app) {
    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve; // FIRST!
    // ... rest of code
}
```

### Error: "Cannot find resource 'WpfUiResources.xaml'"

**Cause:** Resource dictionary path is incorrect or file isn't in the same
directory

**Solution:** Ensure `WpfUiResources.xaml` is in the same folder as your XAML
file, or use correct relative path:

```xml
<!-- Same directory -->
<ResourceDictionary Source="WpfUiResources.xaml" />

<!-- Parent directory -->
<ResourceDictionary Source="../WpfUiResources.xaml" />
```

### Controls Don't Appear Styled

**Cause:** `ControlsDictionary` not merged or merged in wrong order

**Solution:** Check `WpfUiResources.xaml` has both dictionaries in correct
order:

```xml
<ResourceDictionary.MergedDictionaries>
    <ui:ThemesDictionary Theme="Dark" />      <!-- FIRST -->
    <ui:ControlsDictionary />                 <!-- SECOND -->
</ResourceDictionary.MergedDictionaries>
```

### DLLs Missing at Runtime

**Verify Wpf.Ui DLLs are copied to output:**

```powershell
# Check the add-in directory
ls "$env:APPDATA\Autodesk\Revit\Addins\2025\PE_Tools" | grep Wpf.Ui
```

Should see:

- `Wpf.Ui.dll` (~5.9 MB)
- `Wpf.Ui.Abstractions.dll` (~7 KB)

---

## Best Practices

1. **Single Resource Dictionary** - Create one `WpfUiResources.xaml` and reuse
   it across all XAML files
2. **URI Namespace Only** - Never use CLR namespace declarations for Wpf.Ui
   controls
3. **DynamicResource for Brushes** - Always use `{DynamicResource}` for theme
   colors, never hardcode
4. **No Code-Behind Loading** - Let XAML handle resource merging via
   `MergedDictionaries`
5. **Assembly Resolver First** - Register the resolver before any other startup
   code
6. **Clean Up on Shutdown** - Unregister the resolver in `OnShutdown()`

---

## Migration Checklist

When adding Wpf.Ui to a new window/control:

- [ ] Add `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"` namespace
- [ ] Add `WpfUiResources.xaml` to `MergedDictionaries` in Resources section
- [ ] Remove any CLR namespace declarations (`clr-namespace:Wpf.Ui...`)
- [ ] Replace any `uiControls:` prefixes with `ui:`
- [ ] Remove manual resource loading from code-behind
- [ ] Replace hardcoded colors with `{DynamicResource}` theme brushes
- [ ] Test in Revit to verify assembly resolution works

---

## References

- Wpf.Ui Documentation: https://wpfui.lepo.co/
- Wpf.Ui GitHub: https://github.com/lepoco/wpfui
- Assembly Resolution in Revit:
  https://thebuildingcoder.typepad.com/blog/about-the-author.html#5.36
