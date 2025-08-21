using PE_Addin_CommandPalette.VM;
using PE_Addin_CommandPalette.M;
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

namespace PE_Addin_CommandPalette.V;

/// <summary>
///     Interaction logic for CommandPaletteWindow.xaml
/// </summary>
public partial class CommandPaletteWindow : Window {
    private bool _isClosing;
    private readonly DispatcherTimer _searchTimer;
    private CommandPaletteViewModel _viewModel => this.DataContext as CommandPaletteViewModel;

    public CommandPaletteWindow() {
        this.InitializeComponent();

        this._searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        this._searchTimer.Tick += (s, e) => this._searchTimer.Stop();

        this.Loaded += this.OnLoad;
        this.Deactivated += this.OnWindowUnfocus;
        this.LostFocus += this.OnWindowUnfocus;
    }

    private void OnLoad(object sender, RoutedEventArgs e) {
        if (this._viewModel == null)
            throw new InvalidOperationException("CommandPalette view-model is null");

        this.CommandListBox.SelectionChanged += (s, e) => {
            if (this._viewModel.SelectedCommand != null)
                this.CommandListBox.ScrollIntoView(this._viewModel.SelectedCommand);
        };

        // Enable single-click-to-run functionality
        this.CommandListBox.MouseLeftButtonUp += this.OnCommandListBoxMouseLeftButtonUp;
    }

    /// <summary> Enable click-outside-to-close functionality </summary>
    private void OnWindowUnfocus(object sender, EventArgs e) {
        if (!this.IsActive && !this._isClosing && !this.IsMouseOver) {
            this.CloseWindow();
        }
    }

    /// <summary> Enable single-click-to-run on command list items </summary>
    private void OnCommandListBoxMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (this._viewModel?.SelectedCommand != null && 
            this._viewModel.ExecuteSelectedCommandCommand.CanExecute(null)) {
            this.CloseWindow();
            _ = this._viewModel.ExecuteSelectedCommandCommand.ExecuteAsync(null);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
        this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e) {
        if (this._viewModel == null)
            throw new InvalidOperationException("CommandPalette view-model is null");

        if (this._isClosing)
            return; // Prevent handling if already closing or no view model

        switch (e.Key) {
        case Key.Escape:
            if (!string.IsNullOrEmpty(this._viewModel.SearchText)) this._viewModel.ClearSearchCommand.Execute(null);

            if (string.IsNullOrEmpty(this._viewModel.SearchText)) this.CloseWindow();

            e.Handled = true;
            break;

        case Key.Enter:
            this.CloseWindow();

            if (this._viewModel.ExecuteSelectedCommandCommand.CanExecute(null))
                _ = this._viewModel.ExecuteSelectedCommandCommand.ExecuteAsync(null);

            e.Handled = true;
            break;

        case Key.Tab:
            // Prevent tab from changing focus
            e.Handled = true;
            break;
        }
    }

    private void SearchTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e
    ) {
        // Debounce search to improve performance
        this._searchTimer.Stop();
        this._searchTimer.Start();
    }

    /// <summary>
    ///     Unified method to close the window with proper state management
    /// </summary>
    private void CloseWindow() {
        if (this._isClosing)
            return; // Prevent multiple close attempts

        try {
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

#region Value Converters

/// <summary>
///     Converter for showing usage count only when > 0, or strings when not empty
/// </summary>
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

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) =>
        throw new NotImplementedException();
}

#endregion