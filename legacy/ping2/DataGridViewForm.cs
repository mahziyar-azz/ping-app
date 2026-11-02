using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ping2
{
    public partial class DataGridViewForm : Form
    {
        public class IpHistoryRecord
        {
            public string Id { get; set; }
            public string Ip { get; set; }
            public string Country { get; set; }
            public string Time { get; set; }
        }

        private static readonly string HistoryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IranianInternetMonitor"
        );
        private static readonly string HistoryFilePath = Path.Combine(HistoryDirectory, "ip_history.json");

        private List<IpHistoryRecord> _records = new List<IpHistoryRecord>();

        public DataGridViewForm()
        {
            InitializeComponent();
            InitializeDataGridView();
            LoadHistory();
        }

        private void InitializeDataGridView()
        {
            dataGridView1.Columns.Add("ID", "ID");
            dataGridView1.Columns.Add("Ip", "IP");
            dataGridView1.Columns.Add("Country", "Country");
            dataGridView1.Columns.Add("Time", "Time");

            dataGridView1.Columns["ID"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dataGridView1.Columns["Ip"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView1.Columns["Country"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dataGridView1.Columns["Time"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        }

        public void AddRow(string id, string ip, string country, string time)
        {
            var record = new IpHistoryRecord
            {
                Id = id,
                Ip = ip,
                Country = country,
                Time = time
            };

            _records.Insert(0, record);

            // Keep only the last 100 records
            if (_records.Count > 100)
            {
                _records = _records.Take(100).ToList();
            }

            dataGridView1.Rows.Insert(0, id, ip, country, time);

            while (dataGridView1.Rows.Count > 100)
            {
                dataGridView1.Rows.RemoveAt(dataGridView1.Rows.Count - 1);
            }

            SaveHistory();
        }

        public int GetNextLogEntryId()
        {
            if (_records.Count == 0) return 1;
            int maxId = 0;
            foreach (var rec in _records)
            {
                int current;
                if (int.TryParse(rec.Id, out current))
                {
                    if (current > maxId) maxId = current;
                }
            }
            return maxId + 1;
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    var loaded = JsonConvert.DeserializeObject<List<IpHistoryRecord>>(json);
                    if (loaded != null)
                    {
                        _records = loaded.Take(100).ToList();
                        foreach (var rec in _records)
                        {
                            dataGridView1.Rows.Add(rec.Id, rec.Ip, rec.Country, rec.Time);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void SaveHistory()
        {
            try
            {
                if (!Directory.Exists(HistoryDirectory))
                {
                    Directory.CreateDirectory(HistoryDirectory);
                }

                string json = JsonConvert.SerializeObject(_records, Formatting.Indented);
                File.WriteAllText(HistoryFilePath, json);
            }
            catch (Exception)
            {
            }
        }

        private void Form2_Load(object sender, EventArgs e)
        {
        }
    }
}
