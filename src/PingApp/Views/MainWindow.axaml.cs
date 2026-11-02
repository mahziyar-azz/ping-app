using Avalonia.Controls;
using PingApp.ViewModels;

namespace PingApp.Views
{
    public partial class MainWindow : Window
    {
        private HistoryWindow? _historyWindow;
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _viewModel.RequestShowHistory += OnRequestShowHistory;

            Opened += async (_, _) =>
            {
                await _viewModel.InitializeAsync();
            };
        }

        private void OnRequestShowHistory()
        {
            if (_historyWindow == null || !_historyWindow.IsVisible)
            {
                _historyWindow = new HistoryWindow(_viewModel.HistoryVm);
                _historyWindow.Closed += (_, _) => _historyWindow = null;
                _historyWindow.Show(this);
            }
            else
            {
                _historyWindow.Activate();
            }
        }
    }
}
