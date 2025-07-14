using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PE_CommandPalette.ViewModels;
using System.Globalization;
using System.Windows.Data;

namespace PE_CommandPalette.Views
{
    /// <summary>
    /// Interaction logic for CommandPaletteWindow.xaml
    /// </summary>
    public partial class CommandPaletteWindow : Window
    {
        private CommandPaletteViewModel _viewModel;
        private DispatcherTimer _searchTimer;

        public CommandPaletteWindow(UIApplication uiApplication)
        {
            InitializeComponent();
            
            _viewModel = new CommandPaletteViewModel(uiApplication);
            DataContext = _viewModel;

            // Initialize search debounce timer
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _searchTimer.Tick += SearchTimer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus the search box when window loads
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (string.IsNullOrEmpty(_viewModel.SearchText))
                    {
                        // Close window if search is empty
                        Close();
                    }
                    else
                    {
                        // Clear search if there's text
                        _viewModel.ClearSearchCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;

                case Key.Enter:
                    // Execute selected command and close window
                    if (_viewModel.ExecuteCommand.CanExecute(null))
                    {
                        _viewModel.ExecuteCommand.Execute(null);
                        Close();
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    // Move selection down
                    _viewModel.MoveDownCommand.Execute(null);
                    EnsureSelectedItemVisible();
                    e.Handled = true;
                    break;

                case Key.Up:
                    // Move selection up
                    _viewModel.MoveUpCommand.Execute(null);
                    EnsureSelectedItemVisible();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Prevent tab from changing focus
                    e.Handled = true;
                    break;
            }
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Debounce search to improve performance
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            // The actual filtering is handled by the ViewModel through data binding
            EnsureSelectedItemVisible();
        }

        private void CommandListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Execute command on double-click
            if (_viewModel.ExecuteCommand.CanExecute(null))
            {
                _viewModel.ExecuteCommand.Execute(null);
                Close();
            }
        }

        private void EnsureSelectedItemVisible()
        {
            if (_viewModel.SelectedCommand != null)
            {
                CommandListBox.ScrollIntoView(_viewModel.SelectedCommand);
            }
        }

        protected override void OnDeactivated(EventArgs e)
        {
            // Close window when it loses focus (like VS Code command palette)
            base.OnDeactivated(e);
            Close();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Remove window from Alt+Tab
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, 
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
        }

        #region Win32 API for hiding from Alt+Tab

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
        public static readonly BooleanToVisibilityConverter Instance = new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is System.Windows.Visibility visibility && visibility == System.Windows.Visibility.Visible;
        }
    }

    /// <summary>
    /// Converter for showing usage count only when > 0
    /// </summary>
    public class VisibilityConverter : IValueConverter
    {
        public static readonly VisibilityConverter Instance = new VisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}