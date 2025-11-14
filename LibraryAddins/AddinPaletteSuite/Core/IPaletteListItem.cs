using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace AddinPaletteSuite.Core;

/// <summary>
///     Interface that all palette items must implement for display and interaction
/// </summary>
public interface IPaletteListItem {
    /// <summary> Main display text (e.g., command name, view name) </summary>
    string TextPrimary { get; }

    /// <summary> Subtitle/description text (e.g., menu paths, view type) </summary>
    string TextSecondary { get; }

    /// <summary> Badge/pill text (e.g., keyboard shortcuts) </summary>
    string TextPill { get; }

    /// <summary> Full tooltip text for detailed information </summary>
    string TextInfo { get; }

    /// <summary> Item icon (optional, can be null) </summary>
    BitmapImage Icon { get; }

    /// <summary> Whether this item is currently selected in the UI </summary>
    bool IsSelected { get; set; }

    /// <summary> Search relevance score for filtering/ranking (set by search service) </summary>
    double SearchScore { get; set; }

    /// <summary> Whether this item can be executed (used for visual styling) </summary>
    bool CanExecute { get; set; }
}

public abstract partial class BaseObservableListItem : ObservableObject, INotifyPropertyChanged {
    [ObservableProperty] private bool _isSelected = false;
    [ObservableProperty] private double _searchScore;
    [ObservableProperty] private bool _canExecute = true;
}