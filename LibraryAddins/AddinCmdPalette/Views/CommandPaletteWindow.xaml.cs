using AddinCmdPalette.ViewModels;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Visibility = System.Windows.Visibility;

namespace AddinCmdPalette.Views;

/// <summary>
///     Interaction logic for CommandPaletteWindow.xaml
/// </summary>
public partial class CommandPaletteWindow : Window {
    private readonly DispatcherTimer _searchTimer;
    private bool _isClosing;

    public CommandPaletteWindow() {
        this.InitializeComponent();

        this._searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        this._searchTimer.Tick += (s, e) => this._searchTimer.Stop();

        this.Loaded += this.OnLoad;
        this.Deactivated += (_, _) => {
            if (!this.IsActive && !this._isClosing && !this.IsMouseOver) this.CloseWindow();
        };
        this.LostFocus += (_, _) => {
            if (!this.IsActive && !this._isClosing) this.CloseWindow();
        };
    }

    private CommandPaletteViewModel ViewModel => this.DataContext as CommandPaletteViewModel;

    private void OnLoad(object sender, RoutedEventArgs eventArgs) {
        if (this.ViewModel == null)
            throw new InvalidOperationException("CommandPalette view-model is null");

        this.CommandListBox.MouseLeftButtonUp += (_, _) => {
            if (this.ViewModel?.SelectedCommand != null &&
                this.ViewModel.ExecuteSelectedCommandCommand.CanExecute(null)) {
                this.CloseWindow();
                this.ViewModel.ExecuteSelectedCommandCommand.ExecuteAsync(null);
            }
        };
        this.CommandListBox.SelectionChanged += (_, _) => {
            if (this.ViewModel.SelectedCommand != null)
                this.CommandListBox.ScrollIntoView(this.ViewModel.SelectedCommand);
        };
    }


    private void Window_Loaded(object sender, RoutedEventArgs e) {
        this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e) {
        // Prevent key handling if window already closing or no view model
        if (this.ViewModel == null) throw new InvalidOperationException("CommandPalette view-model is null");
        if (this._isClosing) return;

        switch (e.Key) {
        case Key.Escape:
            this.CloseWindow();
            e.Handled = true;
            break;

        case Key.Enter:
            this.CloseWindow();
            if (this.ViewModel.ExecuteSelectedCommandCommand.CanExecute(null))
                _ = this.ViewModel.ExecuteSelectedCommandCommand.ExecuteAsync(null);
            e.Handled = true;
            break;

        case Key.Tab: // Prevent tab from changing focus
            e.Handled = true;
            break;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        this._searchTimer.Stop(); // Debounce search to improve performance
        this._searchTimer.Start();
    }


    private void CloseWindow() {
        try {
            if (this._isClosing) return; // Prevent multiple close attempts
            this._isClosing = true;
            this.Close();
        } catch (InvalidOperationException) { } // Window is already closing, ignore the exception
    }

    protected override void OnClosing(CancelEventArgs e) {
        this._isClosing = true;
        this._searchTimer?.Stop();
        base.OnClosing(e);
    }

    #region Hiding from Alt+Tab

    protected override void OnSourceInitialized(EventArgs e) {
        base.OnSourceInitialized(e);
        // Remove window from Alt+Tab
        var helper = new WindowInteropHelper(this);
        _ = SetWindowLong(
            helper.Handle,
            GWL_EXSTYLE,
            GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW
        );
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    #endregion
}

/// <summary> Coerce value to a display state </summary>
public class VisibilityConverter : IValueConverter {
    public static readonly VisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch {
            bool boolValue => boolValue
                ? Visibility.Visible
                : Visibility.Collapsed,
            int intValue => intValue > 0
                ? Visibility.Visible
                : Visibility.Collapsed,
            string stringValue => !string.IsNullOrWhiteSpace(stringValue)
                ? Visibility.Visible
                : Visibility.Collapsed,
            _ => Visibility.Collapsed
        };

    public object ConvertBack(object _, Type __, object ___, CultureInfo ____) =>
        throw new NotImplementedException();
}