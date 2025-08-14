using System.Windows;
using PE_TapMaker.VM;

namespace PE_TapMaker.V
{
    public partial class TapMakerWindow : Window
    {
        private TapMakerViewModel _viewModel => DataContext as TapMakerViewModel;

        public TapMakerWindow()
        {
            InitializeComponent();
            this.Loaded += OnLoad;
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
                throw new InvalidOperationException("TapMaker view-model is null");
        }
    }
}
