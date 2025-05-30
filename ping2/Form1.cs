﻿using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;


namespace ping2
{


    public partial class Form1 : Form
    {
        private DataGridViewForm dataGridViewForm;
        private bool load = false;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationTokenSource cancellationTokenError;
        private CancellationTokenSource canceltheloadingFLAG;


        private System.Windows.Forms.Timer pingTimer;
        private int countdown = 6; // Countdown in seconds
        private bool isTimerRunning = false;  // Track whether the timer is running
        private bool First_time = true;  // Track whether the timer is running

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


            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(useProxyCheckBox, "Use system proxy for Pinging!\nNot recommended: some URLs not allow the HTTP response time.");
        }
        private void PingTimer_Tick(object sender, EventArgs e)
        {
            // Decrease the countdown
            countdown--;

            // Update the countdownLabel text
            countdownLabel.Text = $"{countdown}";

            // If countdown reaches zero, reset and execute the ping logic
            if (countdown <= 0)
            {
                countdown = 6; // Reset countdown for the next minute
                Run_Ping(sender, e);
            }
        }
        private void Run_Ping(object sender, EventArgs e)
        {
            normal_button_Click(sender, e);
            filter_button_Click(sender, e);
            Tahrim_button_Click(sender, e);
            Check_IP_button_Click(sender, e);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            //Run_Ping(sender, e);
            //pingTimer.Start();
            //await FetchIPCountryAndFlagAsync();

            Check_IP_button_Click(sender, e);


            //20:16

        }
        private async Task<(bool Success, long RoundtripTime)> HttpPingAsync(string url)
        {
            try
            {
                // Add "http://" if the URL doesn't have a protocol
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "http://" + url;
                }

                var handler = new HttpClientHandler
                {
                    UseProxy = true, // Use system proxy since checkbox is checked
                    Proxy = null     // null means use the system default proxy
                };

                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    // Use HEAD request for a lightweight ping
                    HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                    stopwatch.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        return (true, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        return (false, 0);
                    }
                }
            }
            catch (Exception)
            {
                // Silently return failure without notification
                return (false, 0);
            }
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
                    //load = false;
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
                    string flagUrl = $"https://flagcdn.com/w40/{countryCode.ToLower()}.jpg";
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
            else if (string.IsNullOrEmpty(timezone))
            {
                return "Unknown";
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
                        canceltheloadingFLAG.Cancel();
                        Loading_Flag_label.Visible = false;
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
                    Notifier.ShowNotification($"Error pinging {url}", ex.Message);
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
            var tasks = urlLabelPairs.Select(async pair =>
            {
                if (useProxyCheckBox.Checked)
                {
                    // Use HTTP ping with system proxy when checkbox is checked
                    var (success, roundtripTime) = await HttpPingAsync(pair.Url);
                    if (success)
                    {
                        pair.Label.Text = $"Http time: {roundtripTime} ms";
                        pair.Label.ForeColor = Color.Green;
                    }
                    else
                    {
                        pair.Label.Text = "Http Failed❌";
                        pair.Label.ForeColor = Color.Red;
                    }
                }
                else
                {
                    // Use ICMP ping when checkbox is not checked
                    PingReply reply = await PingUrlAsync(pair.Url);
                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        pair.Label.Text = $"Ping time: {reply.RoundtripTime} ms";
                        pair.Label.ForeColor = Color.Green;
                    }
                    else
                    {
                        pair.Label.Text = "Ping Failed❌";
                        pair.Label.ForeColor = Color.Red;
                    }
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }

        // Helper method to detect IP addresses
        private bool IsIpAddress(string url)
        {
            return System.Net.IPAddress.TryParse(url, out _);
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

        private async void Check_IP_button_Click(object sender, EventArgs e)
        {
            PictureBox1.Image = null;
            Loading_Flag_label.Visible = true;

            // Cancel any previous loading animation and initialize a new token
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            canceltheloadingFLAG = new CancellationTokenSource();



            // Start the loading animation in parallel
            var loadingTask = UpdateLoadingText(cancellationTokenSource.Token);

            var loadingTask2 = UpdateLoadingTextFLAG(canceltheloadingFLAG.Token);

            async Task UpdateLoadingText(CancellationToken token)
            {
                try
                {
                    string[] dots = { ".", "..", "...", "...." };
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
                    MessageBox.Show($" Unknown ERROR \nthe system can't run -loading- animation ", "Error Update the -Loading- Text ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            // Fetch the IP, country, and flag
            await FetchIPCountryAndFlagAsync();
        }

        async Task UpdateLoadingTextFLAG(CancellationToken token)
        {
            try
            {
                string[] dots = { "●", "● ●", "● ● ●",};
                while (!token.IsCancellationRequested)
                {
                    foreach (string dot in dots)
                    {
                        if (token.IsCancellationRequested) break;

                        Loading_Flag_label.Text = $"{dot}";

                        await Task.Delay(300);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show($" Unknown ERROR \nthe system can't run -loading- animation ", "Error Update the -Loading- Text ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                if (First_time)
                {
                    Run_Ping(sender, e);
                    First_time = false;
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

        private void button1_Click(object sender, EventArgs e)
        {
            Run_Ping(sender, e);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/mahziyar-azz/ping-app",
                UseShellExecute = true
            });

        }
    }
}
