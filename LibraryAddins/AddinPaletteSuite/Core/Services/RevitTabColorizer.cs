using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace AddinPaletteSuite.Core.Services;

/// <summary>
///     Service for reading document colors from Revit's UI (set by pyRevit or other addins).
///     Instead of setting colors ourselves, we read what's already in the UI.
/// </summary>
public static class RevitTabColorReader {
    private static Visual _cachedMainWindow;
    private static DateTime _lastCacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Gets the tab color for a specific document by reading it from the Revit UI.
    ///     Returns null if no color is found or if pyRevit colorization is not active.
    /// </summary>
    public static WpfColor? GetDocumentColorFromUI(Document doc) {
        if (doc == null) return null;

        try {
            var mainWindow = GetCachedMainRevitWindow();
            if (mainWindow == null) return null;

            var dockingManager = mainWindow.FindDescendantsByTypeName("DockingManager").FirstOrDefault();
            if (dockingManager == null) return null;

            var docPanes = dockingManager.FindDescendantsByTypeName("LayoutDocumentPaneControl");

            // Build possible document name patterns
            // For family files, the tooltip includes .rfa extension
            var docTitleWithExt = doc.Title;
            if (doc.IsFamilyDocument && !doc.Title.EndsWith(".rfa")) {
                docTitleWithExt = doc.Title + ".rfa";
            }

            foreach (var pane in docPanes) {
                var tabs = pane.FindDescendants<TabItem>();

                foreach (var tab in tabs) {
                    var tooltip = tab.ToolTip?.ToString();
                    if (string.IsNullOrEmpty(tooltip)) continue;

                    // Tab tooltip format: "{DocumentName} - {ViewTitle}"
                    // Check if this tab belongs to our document (try both with and without extension)
                    var isMatch = tooltip.StartsWith($"{doc.Title} - ") ||
                                  tooltip.StartsWith($"{docTitleWithExt} - ");

                    if (isMatch) {
                        Debug.WriteLine($"[RevitTabColorReader] Found matching tab: '{tooltip}'");
                        Debug.WriteLine($"[RevitTabColorReader]   Background: {tab.Background?.GetType().Name ?? "null"}");
                        Debug.WriteLine($"[RevitTabColorReader]   BorderBrush: {tab.BorderBrush?.GetType().Name ?? "null"}");
                        Debug.WriteLine($"[RevitTabColorReader]   BorderThickness: {tab.BorderThickness}");

                        WpfColor? backgroundColorValue = null;
                        WpfColor? borderColorValue = null;

                        if (tab.Background is SolidColorBrush backgroundBrush) {
                            backgroundColorValue = backgroundBrush.Color;
                            Debug.WriteLine($"[RevitTabColorReader]   Background color: R={backgroundBrush.Color.R} G={backgroundBrush.Color.G} B={backgroundBrush.Color.B}");
                        }

                        if (tab.BorderBrush is SolidColorBrush borderBrush) {
                            borderColorValue = borderBrush.Color;
                            Debug.WriteLine($"[RevitTabColorReader]   BorderBrush color: R={borderBrush.Color.R} G={borderBrush.Color.G} B={borderBrush.Color.B}");
                        }

                        // In border mode, pyRevit uses BorderBrush for color and background is theme-based (white in light, dark in dark mode)
                        // Detect border mode: BorderThickness > 0 AND BorderBrush is significantly different from Background
                        if (borderColorValue.HasValue && backgroundColorValue.HasValue && tab.BorderThickness.Top > 0) {
                            var bg = backgroundColorValue.Value;
                            var border = borderColorValue.Value;

                            // Calculate color difference between background and border
                            var colorDiff = Math.Abs(bg.R - border.R) + Math.Abs(bg.G - border.G) + Math.Abs(bg.B - border.B);

                            // If border is significantly different from background (diff > 100), use border color
                            if (colorDiff > 100) {
                                Debug.WriteLine($"[RevitTabColorReader]   Color difference: {colorDiff} - Using BorderBrush (border mode detected)");
                                return borderColorValue.Value;
                            }
                        }

                        // Otherwise use background color (fill mode)
                        if (backgroundColorValue.HasValue) {
                            Debug.WriteLine($"[RevitTabColorReader]   ✓ Using Background color (fill mode)");
                            return backgroundColorValue.Value;
                        }

                        Debug.WriteLine($"[RevitTabColorReader]   ✗ No usable color found");
                    }
                }
            }

            return null;
        } catch {
            return null;
        }
    }

    /// <summary>
    ///     Gets the main Revit window with caching to avoid repeated lookups
    /// </summary>
    private static Visual GetCachedMainRevitWindow() {
        var now = DateTime.UtcNow;

        // Return cached window if still valid
        if (_cachedMainWindow != null && (now - _lastCacheTime) < CacheExpiry)
            return _cachedMainWindow;

        // Cache expired or not set, get fresh window
        try {
            var revitHandle = Process.GetCurrentProcess().MainWindowHandle;
            if (revitHandle == IntPtr.Zero) return null;

            var hwndSource = HwndSource.FromHwnd(revitHandle);
            var window = hwndSource?.RootVisual as Visual;

            if (window != null) {
                _cachedMainWindow = window;
                _lastCacheTime = now;
            }

            return window;
        } catch {
            return null;
        }
    }

    /// <summary>
    ///     Extension method to find all descendants of a specific type in the WPF visual tree
    /// </summary>
    private static IEnumerable<T> FindDescendants<T>(this DependencyObject parent) where T : DependencyObject {
        if (parent == null) yield break;

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++) {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                yield return typedChild;

            foreach (var descendant in FindDescendants<T>(child))
                yield return descendant;
        }
    }

    /// <summary>
    ///     Find descendants by type name (for types we don't have references to, like DockingManager)
    /// </summary>
    private static IEnumerable<DependencyObject> FindDescendantsByTypeName(this DependencyObject parent, string typeName) {
        if (parent == null) yield break;

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++) {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child.GetType().Name == typeName)
                yield return child;

            foreach (var descendant in FindDescendantsByTypeName(child, typeName))
                yield return descendant;
        }
    }
}

