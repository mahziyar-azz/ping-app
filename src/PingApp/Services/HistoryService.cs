using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PingApp.Models;

namespace PingApp.Services
{
    public class HistoryService
    {
        private static readonly string StorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IranianInternetMonitor"
        );

        private static readonly string HistoryFilePath = Path.Combine(StorageDirectory, "ip_history.json");

        private readonly List<IpHistoryRecord> _records = new();

        public IReadOnlyList<IpHistoryRecord> Records => _records.AsReadOnly();

        public async Task<List<IpHistoryRecord>> LoadHistoryAsync()
        {
            _records.Clear();
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    string json = await File.ReadAllTextAsync(HistoryFilePath);
                    var loaded = JsonSerializer.Deserialize<List<IpHistoryRecord>>(json);
                    if (loaded != null)
                    {
                        _records.AddRange(loaded.Take(100));
                    }
                }
            }
            catch
            {
                // Ignore filesystem read errors gracefully
            }

            return _records.ToList();
        }

        public async Task AddRecordAsync(string ip, string country)
        {
            int nextId = 1;
            foreach (var rec in _records)
            {
                if (int.TryParse(rec.Id, out int id) && id >= nextId)
                {
                    nextId = id + 1;
                }
            }

            var newRecord = new IpHistoryRecord
            {
                Id = nextId.ToString(),
                Ip = ip,
                Country = country,
                Time = DateTime.Now.ToString("HH:mm:ss")
            };

            _records.Insert(0, newRecord);

            if (_records.Count > 100)
            {
                _records.RemoveRange(100, _records.Count - 100);
            }

            await SaveHistoryAsync();
        }

        public async Task ClearHistoryAsync()
        {
            _records.Clear();
            await SaveHistoryAsync();
        }

        private async Task SaveHistoryAsync()
        {
            try
            {
                if (!Directory.Exists(StorageDirectory))
                {
                    Directory.CreateDirectory(StorageDirectory);
                }

                string json = JsonSerializer.Serialize(_records, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(HistoryFilePath, json);
            }
            catch
            {
                // Ignore filesystem write errors gracefully
            }
        }
    }
}
