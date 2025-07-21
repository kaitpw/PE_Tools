using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using PE_CommandPalette.ViewModels;

namespace PE_CommandPalette.Views
{
    /// <summary>
    /// Interaction logic for CommandPaletteWindow.xaml
    /// </summary>
    public partial class CommandPaletteWindow : Window
    {
        private CommandPaletteViewModel _viewModel;
        private DispatcherTimer _searchTimer;
        private bool _isClosing = false;

        public CommandPaletteWindow(UIApplication uiapp)
        {
            InitializeComponent();

            _viewModel = new CommandPaletteViewModel(uiapp);
            DataContext = _viewModel;

            // Subscribe to command execution completion event
            _viewModel.CommandExecutionCompleted += OnCommandExecutionCompleted;

            // Initialize search debounce timer
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _searchTimer.Tick += (s, e) => _searchTimer.Stop();

            // Subscribe to selection changed to scroll selected item into view
            this.Loaded += (s, e) =>
            {
                CommandListBox.SelectionChanged += CommandListBox_SelectionChanged;
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus the search box when window loads
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isClosing)
                return; // Prevent handling if already closing

            switch (e.Key)
            {
                case Key.Escape:
                    HandleEscapeKey();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    HandleChooseCommand();
                    e.Handled = true;
                    break;

                case Key.Down:
                    // Move selection down
                    _viewModel.MoveSelectionDownCommand.Execute(null);
                    EnsureSelectedItemVisible();
                    e.Handled = true;
                    break;

                case Key.Up:
                    // Move selection up
                    _viewModel.MoveSelectionUpCommand.Execute(null);
                    EnsureSelectedItemVisible();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Prevent tab from changing focus
                    e.Handled = true;
                    break;
            }
        }

        private void HandleEscapeKey()
        {
            if (string.IsNullOrEmpty(_viewModel.SearchText))
            {
                // Close window if search is empty
                CloseWindow();
            }
            else
            {
                // Clear search if there's text
                _viewModel.ClearSearchCommand.Execute(null);
            }
        }

        private async void HandleChooseCommand()
        {
            if (_viewModel.ExecuteSelectedCommandCommand.CanExecute(null))
            {
                await _viewModel.ExecuteSelectedCommandCommand.ExecuteAsync(null);
            }
        }

        private void SearchTextBox_TextChanged(
            object sender,
            System.Windows.Controls.TextChangedEventArgs e
        )
        {
            // Debounce search to improve performance
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void EnsureSelectedItemVisible()
        {
            if (_viewModel.SelectedCommand != null)
            {
                CommandListBox.ScrollIntoView(_viewModel.SelectedCommand);
            }
        }

        /// <summary>
        /// Unified method to close the window with proper state management
        /// </summary>
        private void CloseWindow()
        {
            if (_isClosing)
                return; // Prevent multiple close attempts

            _isClosing = true;

            try
            {
                Close();
            }
            catch (InvalidOperationException)
            {
                // Window is already closing, ignore the exception
            }
        }

        /// <summary>
        /// Handles command execution completion
        /// </summary>
        private void OnCommandExecutionCompleted(
            object sender,
            CommandExecutionCompletedEventArgs e
        )
        {
            // Close window after command execution completes
            CloseWindow();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            // Attempt (does not work) to close window when it loses focus (like VS Code command palette)
            if (!_isClosing)
            {
                base.OnDeactivated(e);
                CloseWindow();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Mark as closing to prevent further operations
            _isClosing = true;

            // Unsubscribe from events
            if (_viewModel != null)
            {
                _viewModel.CommandExecutionCompleted -= OnCommandExecutionCompleted;
            }

            // Stop the search timer
            _searchTimer?.Stop();

            base.OnClosing(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Remove window from Alt+Tab
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            SetWindowLong(
                helper.Handle,
                GWL_EXSTYLE,
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW
            );
        }

        #region Win32 API for hiding from Alt+Tab

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

        private void HandleChooseCommand(object sender, MouseButtonEventArgs e)
        {
            // Only activate on double-click of a list item
            if (e.ClickCount == 2 && CommandListBox.SelectedItem != null)
            {
                HandleChooseCommand();
            }
        }

        private void CommandListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            EnsureSelectedItemVisible();
        }
    }

    #region Value Converters

    /// <summary>
    /// Converter for boolean to visibility
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance =
            new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            return value is System.Windows.Visibility visibility
                && visibility == System.Windows.Visibility.Visible;
        }
    }

    /// <summary>
    /// Converter for showing usage count only when > 0, or strings when not empty
    /// </summary>
    public class VisibilityConverter : IValueConverter
    {
        public static readonly VisibilityConverter Instance = new VisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
            else if (value is string stringValue)
            {
                return !string.IsNullOrWhiteSpace(stringValue)
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
