using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ping2
{
    public partial class DataGridViewForm : Form
    {
        public DataGridViewForm()
        {
            InitializeComponent();
            InitializeDataGridView();
        }

        private void InitializeDataGridView()
        {
            dataGridView1.Columns.Add("ID", "ID");
            dataGridView1.Columns.Add("Ip", "IP");
            dataGridView1.Columns.Add("Country", "Country");
            dataGridView1.Columns.Add("Time", "Time");

            dataGridView1.Columns["ID"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

        }

        // Method to add rows to the DataGridView
        public void AddRow(string id, string ip, string country, string time)
        {
            dataGridView1.Rows.Insert(0, id, ip, country, time); // Insert at the top (index 0)
        }

        private void Form2_Load(object sender, EventArgs e)
        {
        }
    }
}
