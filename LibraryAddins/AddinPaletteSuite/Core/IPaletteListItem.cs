using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace AddinPaletteSuite.Core;

/// <summary>
///     Interface that all palette items must implement for display and interaction
/// </summary>
public interface IPaletteListItem : INotifyPropertyChanged {
    /// <summary> Main display text (e.g., command name, view name) </summary>
    string PrimaryText { get; }

    /// <summary> Subtitle/description text (e.g., menu paths, view type) </summary>
    string SecondaryText { get; }

    /// <summary> Badge/pill text (e.g., keyboard shortcuts) </summary>
    string PillText { get; }

    /// <summary> Full tooltip text for detailed information </summary>
    string TooltipText { get; }

    /// <summary> Item icon (optional, can be null) </summary>
    BitmapImage Icon { get; }

    /// <summary> Whether this item is currently selected in the UI </summary>
    bool IsSelected { get; set; }

    /// <summary> Search relevance score for filtering/ranking (set by search service) </summary>
    double SearchScore { get; set; }
}