using Avalonia.Controls;
using PingApp.ViewModels;

namespace PingApp.Views
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
        }

        public HistoryWindow(HistoryViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
