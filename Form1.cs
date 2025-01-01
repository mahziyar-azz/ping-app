using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;


namespace ping2
{
   

    public partial class Form1 : Form
    {
        private DataGridViewForm dataGridViewForm;
        private bool load = false;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationTokenSource cancellationTokenError;

        private System.Windows.Forms.Timer pingTimer;
        private int countdown = 6; // Countdown in seconds
        private bool isTimerRunning = false;  // Track whether the timer is running
        private bool Firt_time = true;  // Track whether the timer is running

        DataGridView dataGridView1 = new DataGridView();


        public Form1()
        {
            InitializeComponent();

            dataGridViewForm = new DataGridViewForm();

            // Initialize and configure the timer
            pingTimer = new System.Windows.Forms.Timer();
            pingTimer.Interval = 1000; // 5 minute in milliseconds
            pingTimer.Tick += PingTimer_Tick;
            //pingTimer.Start(); // Start the timer
        }
        private void PingTimer_Tick(object sender, EventArgs e)
        {
            // Decrease the countdown
            countdown--;

            // Update the countdownLabel text
            countdownLabel.Text = $"{countdown} Seconds till next Check";

            // If countdown reaches zero, reset and execute the ping logic
            if (countdown <= 0)
            {
                countdown = 6; // Reset countdown for the next minute
                Run_Ping(sender,e);
            }
        }
        private void Run_Ping(object sender, EventArgs e)
        {
            normal_button_Click(sender, e);
            filter_button_Click(sender, e);
            Tahrim_button_Click(sender, e);
            country_button_Click(sender, e);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            //Run_Ping(sender, e);
            //pingTimer.Start();
            //await FetchIPCountryAndFlagAsync();

            country_button_Click(sender, e);

            


        }

        private int logEntryId = 1;
        private async Task FetchIPCountryAndFlagAsync()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    pingTimer.Stop();
                    Stop_the_timer.Text = "Start timer";
                    Stop_the_timer.ForeColor = Color.Green;

                    //MessageBox.Show("No internet connection. Please connect to the internet and try again.", "No connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Notifier.ShowNotification("No connection", "No internet connection. Please connect to the internet and try again.");
                    pingTimer.Stop();
                    return;
                }

                string apiUrl = "http://ip-api.com/json/";
                using (HttpClient client = new HttpClient())
                {
                    string json = await client.GetStringAsync(apiUrl);
                    JObject obj = JObject.Parse(json);
                    load = false;
                    cancellationTokenSource.Cancel();
                    // Extract data
                    string ip = obj["query"]?.ToString();
                    string country = obj["country"]?.ToString();
                    string countryCode = obj["countryCode"]?.ToString();
                    string continent = obj["timezone"]?.ToString();
                    string isp = obj["isp"]?.ToString();
                    continent = ExtractContinentFromTimezone(continent);

                    // Update Labels
                    ip_label.Text = $"{ip}";
                    Country_label.Text = $"{country}";
                    continent_label.Text = $"{continent}";
                    isp_label.Text = $"{isp}";

                    // add to dataGridView1 Here :
                    
                    var currentTime = DateTime.Now.ToString("HH:mm:ss"); // Get the current hour and minute
                    dataGridViewForm.AddRow(logEntryId++.ToString(), ip, country, currentTime);

                    // Fetch and display flag
                    string flagUrl = $"https://flagcdn.com/w40/{countryCode.ToLower()}.png";
                    await LoadFlagAsync(flagUrl);
                }
            }
            catch (Exception ex)
            {
                load = false;
                cancellationTokenSource.Cancel();
                ip_label.Text = "Null";
                Country_label.Text = "Null";
                continent_label.Text = "Null";
                isp_label.Text = "Null";
                PictureBox1.Image = null;
                pingTimer.Stop();
                Stop_the_timer.Text = "Start timer";
                Stop_the_timer.ForeColor = Color.Green;
                MessageBox.Show($"Error fetching data: \n{ex.Message}", "IP Fetch issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private string ExtractContinentFromTimezone(string timezone)
        {
            // Check if the timezone contains a "/"
            if (timezone.Contains("/"))
            {
                // Split by "/" and return the first part
                return timezone.Split('/')[0];
            }
            else
            {
                // Return "Unknown" if no "/" is found
                return "Unknown";
            }
        }


        private async Task LoadFlagAsync(string flagUrl)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    byte[] imageBytes = await client.GetByteArrayAsync(flagUrl);
                    using (MemoryStream ms = new MemoryStream(imageBytes))
                    {
                        PictureBox1.Image?.Dispose();
                        PictureBox1.Image = Image.FromStream(ms);
                    }
                }
            }
            catch (Exception ex)
            {
                pingTimer.Stop();
                Stop_the_timer.Text = "Start timer";
                Stop_the_timer.ForeColor = Color.Green;
                MessageBox.Show($"Error loading flag: \n{ex.Message}", "Load Flag issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async Task<PingReply> PingUrlAsync(string url)
        {
            using (Ping ping = new Ping())
            {
                try
                {
                    // Send an asynchronous ping request to the specified URL
                    return await ping.SendPingAsync(url);
                }
                catch (Exception ex)
                {
                    pingTimer.Stop();
                    Stop_the_timer.Text = "Start timer";
                    Stop_the_timer.ForeColor = Color.Green;
                    MessageBox.Show($"'{url}' Ping failed:\n{ex.Message}", $"Error pinging {url}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
            }
        }

        private void SetLabelStatus(Label[] labels, string text, Color color)
        {
            foreach (Label label in labels)
            {
                label.Text = text;
                label.ForeColor = color;
            }
        }

        private async Task PingAndUpdateLabels((string Url, Label Label)[] urlLabelPairs)
        {
            foreach (var (url, label) in urlLabelPairs)
            {
                PingReply reply = await PingUrlAsync(url);
                if (reply != null && reply.Status == IPStatus.Success)
                {
                    label.Text = $"Ping time: {reply.RoundtripTime} ms";
                    label.ForeColor = Color.Green;
                }
                else
                {
                    label.Text = "Ping Failed❌";
                    label.ForeColor = Color.Red;
                }
            }
        }

        private async void normal_button_Click(object sender, EventArgs e)
        {
            var labels = new[] { google_res, myip_res, yandex_res, aparat_res, speedtest_res, divar_res };
            SetLabelStatus(labels, "<Pinging...>", Color.Black);

            var urlLabelPairs = new (string Url, Label Label)[]
            {
                ("whatismyipaddress.com", myip_res),
                ("google.com", google_res),                
                ("speedtest.net", speedtest_res),
                ("divar.ir", divar_res),
                ("yandex.com", yandex_res),
                ("aparat.com", aparat_res)
            };

            await PingAndUpdateLabels(urlLabelPairs);
        }

        private async void Tahrim_button_Click(object sender, EventArgs e)
        {
            var labels = new[] { openai_res, amd_res, apps_microsoft_res, microsoft_res, deepai_res, cloudflare_res, adobe_res };
            SetLabelStatus(labels, "<Pinging...>", Color.Black);

            var urlLabelPairs = new (string Url, Label Label)[]
            {
                ("openai.com", openai_res),
                ("amd.com", amd_res),
                ("apps.microsoft.com", apps_microsoft_res),
                ("microsoft.com", microsoft_res),
                ("deepai.org", deepai_res),
                ("cloudflare.com", cloudflare_res),
                ("adobe.com", adobe_res)
            };

            await PingAndUpdateLabels(urlLabelPairs);
        }

        private async void filter_button_Click(object sender, EventArgs e)
        {
            var labels = new[] { usa_res, cnn_res, bbc_res, youtube_res, reddit_res, instagram_res, telegram_res, p_res };
            SetLabelStatus(labels, "<Pinging...>", Color.Black);

            var urlLabelPairs = new (string Url, Label Label)[]
            {
                ("4.4.4.4", usa_res),
                ("edition.cnn.com", cnn_res),
                ("bbc.com", bbc_res),
                ("youtube.com", youtube_res),
                ("reddit.com", reddit_res),
                ("instagram.com", instagram_res),
                ("telegram.org", telegram_res),
                ("pornhub.com", p_res) // Changed the NSFW site
                
            };

            await PingAndUpdateLabels(urlLabelPairs);
        }

        private async void country_button_Click(object sender, EventArgs e)
        {
            // Cancel any previous loading animation and initialize a new token
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            PictureBox1.Image = Properties.Resources.loading2;
            load = true;
            // Start the loading animation in parallel
            var loadingTask = UpdateLoadingText(cancellationTokenSource.Token);

            // Fetch the IP, country, and flag
            await FetchIPCountryAndFlagAsync();
        }



        private async Task UpdateLoadingText(CancellationToken token)
        {
            try
            {
                string[] dots = { "", ".", "..", "..." };
                while (!token.IsCancellationRequested)
                {
                    foreach (string dot in dots)
                    {
                        if (token.IsCancellationRequested) break;

                        ip_label.Text = $"Loading{dot}";
                        Country_label.Text = $"Loading{dot}";
                        continent_label.Text = $"Loading{dot}";
                        isp_label.Text = $"Loading{dot}";

                        await Task.Delay(300);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected exception when task is canceled
            }
        }


        private void Stop_the_timer_Click(object sender, EventArgs e)
        {
            if (isTimerRunning)
            {
                // Stop the timer
                pingTimer.Stop();
                Stop_the_timer.Text = "Start timer";
                Stop_the_timer.ForeColor = Color.Green;

                isTimerRunning = false;  // Update the flag
            }
            else
            {
                if (Firt_time)
                {
                    Run_Ping(sender, e);
                    Firt_time = false;
                }
                // Start the timer
                pingTimer.Start();
                Stop_the_timer.Text = "Stop timer";
                Stop_the_timer.ForeColor = Color.Red;

                isTimerRunning = true;  // Update the flag
            }
        }
        public static class Notifier
        {
            public static void ShowNotification(string title, string text, int duration = 3000)
            {
                using (NotifyIcon notifyIcon = new NotifyIcon())
                {
                    notifyIcon.Visible = true;
                    notifyIcon.Icon = Properties.Resources.icon_ping_app; // Custom icon path
                    notifyIcon.BalloonTipTitle = title;
                    notifyIcon.BalloonTipText = text;
                    notifyIcon.ShowBalloonTip(duration);

                    // Optional: To keep the notification visible until a key is pressed (for debugging purposes)
                    // Console.WriteLine("Notification shown. Press any key to exit...");
                    // Console.ReadKey();

                    // No need to dispose explicitly as using statement takes care of it
                }
            }
        }

        private void btnShowGridView_Click(object sender, EventArgs e)
        {
            // Check if the dataGridViewForm is already visible
            if (dataGridViewForm.Visible)
            {

                // If it is visible, hide it
                dataGridViewForm.Hide();
                btnShowGridView.ForeColor = Color.Black;
                btnShowGridView.Text = "Show History";
            }
            else
            {
                // If it is not visible, show it
                dataGridViewForm.Show();
                btnShowGridView.ForeColor = Color.Red;
                btnShowGridView.Text = "Hide History";
            }
        }

    }
}
