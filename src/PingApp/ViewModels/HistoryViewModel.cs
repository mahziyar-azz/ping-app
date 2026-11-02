using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingApp.Models;
using PingApp.Services;

namespace PingApp.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly HistoryService _historyService;

        public ObservableCollection<IpHistoryRecord> DisplayRecords { get; } = new();

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        public HistoryViewModel(HistoryService historyService)
        {
            _historyService = historyService;
        }

        partial void OnSearchFilterChanged(string value)
        {
            ApplyFilter();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                await _historyService.LoadHistoryAsync();
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            DisplayRecords.Clear();
            var query = SearchFilter.Trim().ToLowerInvariant();

            var filtered = string.IsNullOrEmpty(query)
                ? _historyService.Records
                : _historyService.Records.Where(r =>
                    r.Ip.ToLowerInvariant().Contains(query) ||
                    r.Country.ToLowerInvariant().Contains(query) ||
                    r.Time.Contains(query));

            foreach (var rec in filtered)
            {
                DisplayRecords.Add(rec);
            }
        }

        [RelayCommand]
        public async Task ClearHistoryAsync()
        {
            await _historyService.ClearHistoryAsync();
            DisplayRecords.Clear();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            await LoadAsync();
        }
    }
}
