using AddinCmdPalette.Actions;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace AddinCmdPalette.Core;

/// <summary>
///     Interaction logic for SelectablePalette.xaml
/// </summary>
public partial class SelectablePalette : Window {
    private readonly ActionBinding _actionBinding;
    private bool _isClosing;

    public SelectablePalette(
        SelectablePaletteViewModel viewModel,
        IEnumerable<PaletteAction> actions
    ) {
        this.InitializeComponent();
        this.DataContext = viewModel;

        this._actionBinding = new ActionBinding();
        this._actionBinding.RegisterRange(actions);

        this.Loaded += this.OnLoad;
        this.Deactivated += (_, _) => {
            if (!this.IsActive && !this._isClosing && !this.IsMouseOver) this.CloseWindow();
        };
        this.LostFocus += (_, _) => {
            if (!this._isClosing) this.CloseWindow();
        };
    }

    private SelectablePaletteViewModel ViewModel => this.DataContext as SelectablePaletteViewModel;

    private void OnLoad(object sender, RoutedEventArgs eventArgs) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");

        this.ItemListBox.MouseLeftButtonUp += async (_, e) => {
            // Get the clicked item from the event source
            if (e.OriginalSource is FrameworkElement source) {
                var item = source.DataContext as ISelectableItem;
                if (item == null) {
                    // Try to find the ListBoxItem parent
                    var parent = source.Parent as FrameworkElement;
                    while (parent != null && !(parent is ListBoxItem)) parent = parent.Parent as FrameworkElement;
                    if (parent is ListBoxItem listBoxItem) item = listBoxItem.DataContext as ISelectableItem;
                }

                if (item == null) return;

                // Update selection to the clicked item
                if (this.ViewModel != null) this.ViewModel.SelectedItem = item;

                var modifiers = Keyboard.Modifiers;
                try {
                    var executed = await this._actionBinding.TryExecuteAsync(
                        item,
                        MouseButton.Left,
                        modifiers
                    );

                    if (executed) {
                        this.ViewModel?.RecordUsage();
                        this.CloseWindow();
                    }
                } catch (Exception ex) {
                    this.CloseWindow();
                    MessageBox.Show(
                        ex.Message,
                        "Action Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }
        };

        this.ItemListBox.SelectionChanged += (_, _) => {
            if (this.ViewModel.SelectedItem != null)
                this.ItemListBox.ScrollIntoView(this.ViewModel.SelectedItem);
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
        this.SearchTextBox.Focus();
        this.SearchTextBox.SelectAll();
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e) {
        if (this.ViewModel == null) throw new InvalidOperationException("SelectablePalette view-model is null");
        if (this._isClosing) return;

        switch (e.Key) {
        case Key.Escape:
            this.CloseWindow();
            e.Handled = true;
            break;

        case Key.Enter:
            if (this.ViewModel.SelectedItem != null) {
                var modifiers = Keyboard.Modifiers;
                try {
                    var executed = await this._actionBinding.TryExecuteAsync(
                        this.ViewModel.SelectedItem,
                        Key.Enter,
                        modifiers
                    );

                    if (executed) {
                        this.ViewModel.RecordUsage();
                        this.CloseWindow();
                    }
                } catch (Exception ex) {
                    this.CloseWindow();
                    MessageBox.Show(
                        ex.Message,
                        "Action Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }

            e.Handled = true;
            break;

        case Key.Tab: // Prevent tab from changing focus
            e.Handled = true;
            break;
        }
    }

    private void CloseWindow() {
        try {
            if (this._isClosing) return;
            this._isClosing = true;
            this.Close();
        } catch (InvalidOperationException) { } // Window is already closing, ignore
    }

    protected override void OnClosing(CancelEventArgs e) {
        this._isClosing = true;
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