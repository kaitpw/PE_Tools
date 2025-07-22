using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using PE_CommandPalette.VM;

namespace PE_CommandPalette.V
{
    /// <summary>
    /// Interaction logic for CommandPaletteWindow.xaml
    /// </summary>
    public partial class CommandPaletteWindow : Window
    {
        public CommandPaletteWindow()
        {
            InitializeComponent();

            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _searchTimer.Tick += (s, e) => _searchTimer.Stop();

            this.Loaded += OnLoad;
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
                throw new InvalidOperationException("CommandPalette view-model is null");

            CommandListBox.SelectionChanged += (s, e) =>
            {
                if (_viewModel.SelectedCommand != null)
                    CommandListBox.ScrollIntoView(_viewModel.SelectedCommand);
            };
        }

        #region Properties

        private bool _isClosing = false;
        private DispatcherTimer _searchTimer;
        private CommandPaletteViewModel _viewModel
        {
            get => DataContext as CommandPaletteViewModel;
        }

        #endregion


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null)
                throw new InvalidOperationException("CommandPalette view-model is null");

            if (_isClosing)
                return; // Prevent handling if already closing or no view model

            switch (e.Key)
            {
                case Key.Escape:
                    if (!string.IsNullOrEmpty(_viewModel.SearchText))
                        _viewModel.ClearSearchCommand.Execute(null);

                    if (string.IsNullOrEmpty(_viewModel.SearchText))
                        CloseWindow();

                    e.Handled = true;
                    break;

                case Key.Enter:
                    CloseWindow();

                    if (_viewModel.ExecuteSelectedCommandCommand.CanExecute(null))
                        _viewModel.ExecuteSelectedCommandCommand.ExecuteAsync(null);

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
            System.Windows.Controls.TextChangedEventArgs e
        )
        { // Debounce search to improve performance
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        /// <summary>
        /// Unified method to close the window with proper state management
        /// </summary>
        private void CloseWindow()
        {
            if (_isClosing)
                return; // Prevent multiple close attempts

            try
            {
                _isClosing = true;
                Close();
            }
            catch (InvalidOperationException) { } // Window is already closing, ignore the exception
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            _searchTimer?.Stop();
            base.OnClosing(e);
        }

        #region Hiding from Alt+Tab

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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion
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
