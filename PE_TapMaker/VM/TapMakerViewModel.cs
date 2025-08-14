using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PE_TapMaker.H;
using PE_TapMaker.M;

namespace PE_TapMaker.VM
{
    public partial class TapMakerViewModel : ObservableObject
    {
        private readonly UIApplication _uiApplication;
        private readonly Dispatcher _dispatcher;
        private Window _window;

        #region Properties

        [ObservableProperty]
        private string _selectedFaceInfo = "No face selected";

        [ObservableProperty]
        private TapSize _selectedTapSize;

        [ObservableProperty]
        private ObservableCollection<TapSize> _availableTapSizes;

        private Face _selectedFace;
        private Element _selectedDuct;

        #endregion

        #region Commands

        // Face selection now happens before the UI opens, so this command is no longer needed

        [RelayCommand(CanExecute = nameof(CanCreateTap))]
        private void CreateTap()
        {
            if (_selectedFace == null || _selectedDuct == null || SelectedTapSize == null)
                return;

            try
            {
                bool success = TapMakerHelper.CreateTapOnFace(
                    _uiApplication, 
                    _selectedFace, 
                    _selectedDuct, 
                    SelectedTapSize.SizeInches
                );

                if (success)
                {
                    MessageBox.Show("Tap created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseWindow();
                }
                else
                {
                    MessageBox.Show("Failed to create tap", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating tap: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseWindow();
        }

        #endregion

        public TapMakerViewModel(UIApplication uiApplication, Dispatcher dispatcher)
        {
            _uiApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            AvailableTapSizes = new ObservableCollection<TapSize>(TapSize.GetStandardTapSizes());
            
            // Default to 6" tap
            SelectedTapSize = AvailableTapSizes.FirstOrDefault(t => t.SizeInches == 6);
        }

        public TapMakerViewModel(UIApplication uiApplication, Dispatcher dispatcher, Face selectedFace, Element selectedDuct)
            : this(uiApplication, dispatcher)
        {
            _selectedFace = selectedFace ?? throw new ArgumentNullException(nameof(selectedFace));
            _selectedDuct = selectedDuct ?? throw new ArgumentNullException(nameof(selectedDuct));
            SelectedFaceInfo = $"Selected face on duct: {selectedDuct.Name} (ID: {selectedDuct.Id})";
        }

        private bool CanCreateTap()
        {
            return _selectedFace != null && _selectedDuct != null && SelectedTapSize != null;
        }

        private void CloseWindow()
        {
            _dispatcher.Invoke(() =>
            {
                _window?.Close();
            });
        }

        public void SetWindow(Window window)
        {
            _window = window;
        }

        partial void OnSelectedTapSizeChanged(TapSize value)
        {
            CreateTapCommand.NotifyCanExecuteChanged();
        }
    }
}
