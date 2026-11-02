using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ping2
{
    public partial class Form1 : Form
    {
        public class UrlLabelTarget
        {
            public string Url { get; set; }
            public Label Label { get; set; }

            public UrlLabelTarget(string url, Label label)
            {
                Url = url;
                Label = label;
            }
        }
        private DataGridViewForm dataGridViewForm;

        private System.Windows.Forms.Timer pingTimer;
        private int countdown = 6;
        private bool isTimerRunning = false;
        private bool First_time = true;

        public Form1()
        {
            InitializeComponent();

            dataGridViewForm = new DataGridViewForm();
            logEntryId = dataGridViewForm.GetNextLogEntryId();

            pingTimer = new System.Windows.Forms.Timer();
            pingTimer.Interval = 1000;
            pingTimer.Tick += PingTimer_Tick;

            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(useProxyCheckBox, "Use system proxy for Pinging!\nNot recommended: some URLs not allow the HTTP response time.");
        }

        private void PingTimer_Tick(object sender, EventArgs e)
        {
            countdown--;
            countdownLabel.Text = countdown.ToString();

            if (countdown <= 0)
            {
                countdown = 6;
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

        private void Form1_Load(object sender, EventArgs e)
        {
            Check_IP_button_Click(sender, e);
        }

        private int logEntryId = 1;
        private CancellationTokenSource ipLoadingCts;
        private CancellationTokenSource flagLoadingCts;

        private async Task FetchIPCountryAndFlagAsync()
        {
            // Cancel any running animations / requests
            StopAllLoading();

            ipLoadingCts = new CancellationTokenSource();
            flagLoadingCts = new CancellationTokenSource();

            CancellationToken ipToken = ipLoadingCts.Token;
            CancellationToken flagToken = flagLoadingCts.Token;

            PictureBox1.Image = null;
            Loading_Flag_label.Visible = true;

            // Start loading animation tasks in parallel
            Task anim1 = UpdateLoadingText(ipToken);
            Task anim2 = UpdateLoadingTextFLAG(flagToken);

            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    StopAllLoading();
                    Notifier.ShowNotification("No connection", "No internet connection detected.");
                    return;
                }

                GeoIpResult geo = await NetworkProbeService.FetchGeoIpAsync();

                if (ipToken.IsCancellationRequested)
                {
                    return;
                }

                // STOP text animation immediately BEFORE updating labels with the real IP details
                StopIpTextAnimation();

                // Update Labels with fetched data
                ip_label.Text = geo.Ip;
                Country_label.Text = geo.Country;
                continent_label.Text = geo.Continent;
                isp_label.Text = geo.Isp;

                // Add to history
                string currentTime = DateTime.Now.ToString("HH:mm:ss");
                dataGridViewForm.AddRow(logEntryId++.ToString(), geo.Ip, geo.Country, currentTime);

                // Fetch and display flag
                await LoadFlagAsync(geo.CountryCode, flagToken);
            }
            catch (Exception ex)
            {
                StopAllLoading();
                ip_label.Text = "Null";
                Country_label.Text = "Null";
                continent_label.Text = "Null";
                isp_label.Text = "Null";
                PictureBox1.Image = null;
                Notifier.ShowNotification("IP Fetch Issue", ex.Message);
            }
        }

        private async Task LoadFlagAsync(string countryCode, CancellationToken token)
        {
            try
            {
                Image flagImage = await NetworkProbeService.FetchFlagAsync(countryCode);
                
                if (token.IsCancellationRequested)
                {
                    if (flagImage != null)
                    {
                        flagImage.Dispose();
                    }
                    return;
                }

                StopFlagAnimation();

                if (PictureBox1.Image != null)
                {
                    PictureBox1.Image.Dispose();
                }
                PictureBox1.Image = flagImage;
            }
            catch (Exception)
            {
                StopFlagAnimation();
                PictureBox1.Image = null;
            }
        }

        private void StopIpTextAnimation()
        {
            if (ipLoadingCts != null && !ipLoadingCts.IsCancellationRequested)
            {
                ipLoadingCts.Cancel();
            }
        }

        private void StopFlagAnimation()
        {
            if (flagLoadingCts != null && !flagLoadingCts.IsCancellationRequested)
            {
                flagLoadingCts.Cancel();
            }
            Loading_Flag_label.Visible = false;
        }

        private void StopAllLoading()
        {
            StopIpTextAnimation();
            StopFlagAnimation();
        }

        private void SetLabelStatus(Label[] labels, string text, Color color)
        {
            foreach (Label label in labels)
            {
                label.Text = text;
                label.ForeColor = color;
            }
        }

        private async Task PingAndUpdateLabels(UrlLabelTarget[] targets)
        {
            bool useHttp = useProxyCheckBox.Checked;

            var tasks = targets.Select(async target =>
            {
                ProbeResult result = await NetworkProbeService.ProbeEndpointAsync(target.Url, useHttp);
                target.Label.Text = result.Message;
                target.Label.ForeColor = result.StatusColor;
            }).ToArray();

            await Task.WhenAll(tasks);
        }

        private void normal_button_Click(object sender, EventArgs e)
        {
            Label[] labels = new[] { google_res, myip_res, yandex_res, aparat_res, speedtest_res, divar_res };
            SetLabelStatus(labels, "<Pinging...>", Color.Black);

            UrlLabelTarget[] targets = new[]
            {
                new UrlLabelTarget("whatismyipaddress.com", myip_res),
                new UrlLabelTarget("google.com", google_res),
                new UrlLabelTarget("speedtest.net", speedtest_res),
                new UrlLabelTarget("divar.ir", divar_res),
                new UrlLabelTarget("yandex.com", yandex_res),
                new UrlLabelTarget("aparat.com", aparat_res)
            };

            Task ignored = PingAndUpdateLabels(targets);
        }

        private void Tahrim_button_Click(object sender, EventArgs e)
        {
            Label[] labels = new[] { openai_res, amd_res, apps_microsoft_res, microsoft_res, deepai_res, cloudflare_res, adobe_res };
            SetLabelStatus(labels, "<Pinging...>", Color.Black);

            UrlLabelTarget[] targets = new[]
            {
                new UrlLabelTarget("openai.com", openai_res),
                new UrlLabelTarget("amd.com", amd_res),
                new UrlLabelTarget("apps.microsoft.com", apps_microsoft_res),
                new UrlLabelTarget("microsoft.com", microsoft_res),
                new UrlLabelTarget("claude.ai", deepai_res),
                new UrlLabelTarget("cloudflare.com", cloudflare_res),
                new UrlLabelTarget("adobe.com", adobe_res)
            };

            Task ignored = PingAndUpdateLabels(targets);
        }

        private void filter_button_Click(object sender, EventArgs e)
        {
            Label[] labels = new[] { usa_res, cnn_res, bbc_res, youtube_res, reddit_res, instagram_res, telegram_res, p_res };
            SetLabelStatus(labels, "<Pinging...>", Color.Black);

            UrlLabelTarget[] targets = new[]
            {
                new UrlLabelTarget("4.4.4.4", usa_res),
                new UrlLabelTarget("edition.cnn.com", cnn_res),
                new UrlLabelTarget("bbc.com", bbc_res),
                new UrlLabelTarget("youtube.com", youtube_res),
                new UrlLabelTarget("reddit.com", reddit_res),
                new UrlLabelTarget("instagram.com", instagram_res),
                new UrlLabelTarget("telegram.org", telegram_res),
                new UrlLabelTarget("pornhub.com", p_res)
            };

            Task ignored = PingAndUpdateLabels(targets);
        }

        private async void Check_IP_button_Click(object sender, EventArgs e)
        {
            await FetchIPCountryAndFlagAsync();
        }

        private async Task UpdateLoadingText(CancellationToken token)
        {
            try
            {
                string[] dots = new[] { "", ".", "..", "..." };
                while (!token.IsCancellationRequested)
                {
                    foreach (string dot in dots)
                    {
                        if (token.IsCancellationRequested) return;

                        ip_label.Text = "Loading" + dot;
                        Country_label.Text = "Loading" + dot;
                        continent_label.Text = "Loading" + dot;
                        isp_label.Text = "Loading" + dot;

                        await Task.Delay(300, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task UpdateLoadingTextFLAG(CancellationToken token)
        {
            try
            {
                string[] dots = new[] { "\u25CF", "\u25CF \u25CF", "\u25CF \u25CF \u25CF" };
                while (!token.IsCancellationRequested)
                {
                    foreach (string dot in dots)
                    {
                        if (token.IsCancellationRequested) return;

                        Loading_Flag_label.Text = dot;
                        await Task.Delay(300, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void Stop_the_timer_Click(object sender, EventArgs e)
        {
            if (isTimerRunning)
            {
                pingTimer.Stop();
                Stop_the_timer.Text = "Start timer";
                Stop_the_timer.ForeColor = Color.Green;
                isTimerRunning = false;
            }
            else
            {
                if (First_time)
                {
                    Run_Ping(sender, e);
                    First_time = false;
                }
                pingTimer.Start();
                Stop_the_timer.Text = "Stop timer";
                Stop_the_timer.ForeColor = Color.Red;
                isTimerRunning = true;
            }
        }

        public static class Notifier
        {
            public static void ShowNotification(string title, string text, int duration = 3000)
            {
                using (NotifyIcon notifyIcon = new NotifyIcon())
                {
                    notifyIcon.Visible = true;
                    notifyIcon.Icon = Properties.Resources.icon_ping_app;
                    notifyIcon.BalloonTipTitle = title;
                    notifyIcon.BalloonTipText = text;
                    notifyIcon.ShowBalloonTip(duration);
                }
            }
        }

        private void btnShowGridView_Click(object sender, EventArgs e)
        {
            if (dataGridViewForm.Visible)
            {
                dataGridViewForm.Hide();
                btnShowGridView.ForeColor = Color.Black;
                btnShowGridView.Text = "Show History";
            }
            else
            {
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
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/mahziyar-azz/ping-app",
                UseShellExecute = true
            });
        }
    }
}
