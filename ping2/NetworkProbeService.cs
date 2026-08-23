using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ping2
{
    public class ProbeResult
    {
        public bool Success { get; set; }
        public long LatencyMs { get; set; }
        public int PacketLossPercent { get; set; }
        public long JitterMs { get; set; }
        public string Message { get; set; }
        public Color StatusColor { get; set; }
    }

    public class GeoIpResult
    {
        public string Ip { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Continent { get; set; }
        public string Isp { get; set; }
    }

    public static class NetworkProbeService
    {
        private static readonly HttpClientHandler _handler;
        private static readonly HttpClient _httpClient;
        public const int SingleTimeoutMs = 1200; // Fast timeout per packet burst
        public const int PingCount = 3;          // 3 packets per test for Loss & Jitter measurement

        static NetworkProbeService()
        {
            _handler = new HttpClientHandler
            {
                UseProxy = true,
                Proxy = null,
                ServerCertificateCustomValidationCallback = delegate { return true; }
            };

            _httpClient = new HttpClient(_handler)
            {
                Timeout = TimeSpan.FromMilliseconds(SingleTimeoutMs)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static Color GetLatencyColor(long latencyMs, int packetLoss = 0)
        {
            // If there's packet loss, degrade color quality immediately
            if (packetLoss >= 50)
            {
                return Color.FromArgb(211, 47, 47); // Red
            }
            if (packetLoss > 0)
            {
                return Color.FromArgb(230, 81, 0); // Deep Orange
            }

            if (latencyMs <= 80)
            {
                return Color.FromArgb(46, 125, 50); // Dark Green
            }
            if (latencyMs <= 160)
            {
                return Color.FromArgb(104, 159, 56); // Lime Green
            }
            if (latencyMs <= 260)
            {
                return Color.FromArgb(245, 124, 0); // Amber / Orange
            }
            if (latencyMs <= 400)
            {
                return Color.FromArgb(230, 81, 0); // Deep Orange
            }

            return Color.FromArgb(211, 47, 47); // Red
        }

        public static async Task<ProbeResult> ProbeEndpointAsync(string target, bool useProxyOrHttp)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return new ProbeResult
                {
                    Success = false,
                    LatencyMs = 0,
                    Message = "Invalid URL",
                    StatusColor = Color.FromArgb(183, 28, 28)
                };
            }

            if (useProxyOrHttp)
            {
                return await ProbeViaHttpBurstAsync(target);
            }
            else
            {
                return await ProbeViaIcmpBurstAsync(target);
            }
        }

        public static async Task<ProbeResult> ProbeViaHttpBurstAsync(string url)
        {
            string formattedUrl = url.StartsWith("http://") || url.StartsWith("https://") ? url : "https://" + url;
            List<long> latencies = new List<long>();
            int failedCount = 0;
            int lastBlockedStatusCode = 0;

            for (int i = 0; i < PingCount; i++)
            {
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, formattedUrl))
                    {
                        using (HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            stopwatch.Stop();
                            int statusCode = (int)response.StatusCode;

                            if (response.IsSuccessStatusCode || statusCode == 301 || statusCode == 302 || statusCode == 307 || statusCode == 308)
                            {
                                latencies.Add(stopwatch.ElapsedMilliseconds);
                            }
                            else if (statusCode == 403 || statusCode == 451)
                            {
                                lastBlockedStatusCode = statusCode;
                                failedCount++;
                            }
                            else
                            {
                                failedCount++;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    failedCount++;
                }

                if (i < PingCount - 1)
                {
                    await Task.Delay(50); // Small interval between requests
                }
            }

            if (lastBlockedStatusCode > 0 && latencies.Count == 0)
            {
                return new ProbeResult
                {
                    Success = false,
                    LatencyMs = 0,
                    PacketLossPercent = 100,
                    Message = string.Format("Blocked/Tahrim ({0})\u274C", lastBlockedStatusCode),
                    StatusColor = Color.FromArgb(211, 47, 47)
                };
            }

            int packetLoss = (failedCount * 100) / PingCount;

            if (latencies.Count == 0)
            {
                return new ProbeResult
                {
                    Success = false,
                    LatencyMs = 0,
                    PacketLossPercent = 100,
                    Message = "Http Failed (100% Loss)\u274C",
                    StatusColor = Color.FromArgb(183, 28, 28)
                };
            }

            long avgLatency = (long)latencies.Average();
            long jitter = latencies.Count > 1 ? (latencies.Max() - latencies.Min()) : 0;

            string message = packetLoss > 0
                ? string.Format("Http: {0}ms ({1}% Loss)\u26A0", avgLatency, packetLoss)
                : string.Format("Http: {0}ms (\u00B1{1}ms)", avgLatency, jitter);

            return new ProbeResult
            {
                Success = true,
                LatencyMs = avgLatency,
                PacketLossPercent = packetLoss,
                JitterMs = jitter,
                Message = message,
                StatusColor = GetLatencyColor(avgLatency, packetLoss)
            };
        }

        public static async Task<ProbeResult> ProbeViaIcmpBurstAsync(string hostOrIp)
        {
            List<long> latencies = new List<long>();
            int failedCount = 0;

            using (Ping ping = new Ping())
            {
                for (int i = 0; i < PingCount; i++)
                {
                    try
                    {
                        PingReply reply = await ping.SendPingAsync(hostOrIp, SingleTimeoutMs);
                        if (reply != null && reply.Status == IPStatus.Success)
                        {
                            latencies.Add(reply.RoundtripTime);
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                    catch (Exception)
                    {
                        failedCount++;
                    }

                    if (i < PingCount - 1)
                    {
                        await Task.Delay(50);
                    }
                }
            }

            int packetLoss = (failedCount * 100) / PingCount;

            if (latencies.Count == 0)
            {
                return new ProbeResult
                {
                    Success = false,
                    LatencyMs = 0,
                    PacketLossPercent = 100,
                    Message = "Ping Failed (100% Loss)\u274C",
                    StatusColor = Color.FromArgb(183, 28, 28)
                };
            }

            long avgLatency = (long)latencies.Average();
            long jitter = latencies.Count > 1 ? (latencies.Max() - latencies.Min()) : 0;

            string message = packetLoss > 0
                ? string.Format("Ping: {0}ms ({1}% Loss)\u26A0", avgLatency, packetLoss)
                : string.Format("Ping: {0}ms (\u00B1{1}ms)", avgLatency, jitter);

            return new ProbeResult
            {
                Success = true,
                LatencyMs = avgLatency,
                PacketLossPercent = packetLoss,
                JitterMs = jitter,
                Message = message,
                StatusColor = GetLatencyColor(avgLatency, packetLoss)
            };
        }

        public static async Task<GeoIpResult> FetchGeoIpAsync()
        {
            // Fallback Provider 1: ip-api.com
            try
            {
                using (var cts = new CancellationTokenSource(3000))
                {
                    var response = await _httpClient.GetAsync("http://ip-api.com/json/", cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject obj = JObject.Parse(json);
                        if (obj["status"] != null && obj["status"].ToString() == "success")
                        {
                            string timezone = obj["timezone"] != null ? obj["timezone"].ToString() : "";
                            return new GeoIpResult
                            {
                                Ip = obj["query"] != null ? obj["query"].ToString() : "Unknown",
                                Country = obj["country"] != null ? obj["country"].ToString() : "Unknown",
                                CountryCode = obj["countryCode"] != null ? obj["countryCode"].ToString() : "un",
                                Continent = timezone.Contains("/") ? timezone.Split('/')[0] : "Unknown",
                                Isp = obj["isp"] != null ? obj["isp"].ToString() : "Unknown"
                            };
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            // Fallback Provider 2: ipwho.is (HTTPS, Highly Reliable)
            try
            {
                using (var cts = new CancellationTokenSource(3000))
                {
                    var response = await _httpClient.GetAsync("https://ipwho.is/", cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject obj = JObject.Parse(json);
                        if (obj["success"] != null && obj["success"].ToObject<bool>())
                        {
                            string ispName = "Unknown";
                            if (obj["connection"] != null && obj["connection"]["isp"] != null)
                            {
                                ispName = obj["connection"]["isp"].ToString();
                            }

                            return new GeoIpResult
                            {
                                Ip = obj["ip"] != null ? obj["ip"].ToString() : "Unknown",
                                Country = obj["country"] != null ? obj["country"].ToString() : "Unknown",
                                CountryCode = obj["country_code"] != null ? obj["country_code"].ToString() : "un",
                                Continent = obj["continent"] != null ? obj["continent"].ToString() : "Unknown",
                                Isp = ispName
                            };
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            // Fallback Provider 3: freeipapi.com (HTTPS)
            try
            {
                using (var cts = new CancellationTokenSource(3000))
                {
                    var response = await _httpClient.GetAsync("https://freeipapi.com/api/json", cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject obj = JObject.Parse(json);

                        string ispName = "Unknown";
                        if (obj["asnOrganization"] != null)
                        {
                            ispName = obj["asnOrganization"].ToString();
                        }

                        return new GeoIpResult
                        {
                            Ip = obj["ipAddress"] != null ? obj["ipAddress"].ToString() : "Unknown",
                            Country = obj["countryName"] != null ? obj["countryName"].ToString() : "Unknown",
                            CountryCode = obj["countryCode"] != null ? obj["countryCode"].ToString() : "un",
                            Continent = obj["continent"] != null ? obj["continent"].ToString() : "Unknown",
                            Isp = ispName
                        };
                    }
                }
            }
            catch (Exception)
            {
            }

            // Fallback Provider 4: ip.guide (HTTPS)
            try
            {
                using (var cts = new CancellationTokenSource(3000))
                {
                    var response = await _httpClient.GetAsync("https://ip.guide/", cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject obj = JObject.Parse(json);

                        string ip = obj["ip"] != null ? obj["ip"].ToString() : "Unknown";
                        string country = "Unknown";
                        string countryCode = "un";
                        string continent = "Unknown";
                        string isp = "Unknown";

                        if (obj["location"] != null)
                        {
                            var loc = obj["location"];
                            if (loc["country"] != null) country = loc["country"].ToString();
                            if (loc["timezone"] != null)
                            {
                                string tz = loc["timezone"].ToString();
                                continent = tz.Contains("/") ? tz.Split('/')[0] : "Unknown";
                            }
                        }

                        if (obj["network"] != null && obj["network"]["autonomous_system"] != null)
                        {
                            var asInfo = obj["network"]["autonomous_system"];
                            if (asInfo["name"] != null) isp = asInfo["name"].ToString();
                            if (asInfo["country"] != null) countryCode = asInfo["country"].ToString();
                        }

                        return new GeoIpResult
                        {
                            Ip = ip,
                            Country = country,
                            CountryCode = countryCode,
                            Continent = continent,
                            Isp = isp
                        };
                    }
                }
            }
            catch (Exception)
            {
            }

            throw new Exception("All GeoIP services failed to respond.");
        }

        public static async Task<Image> FetchFlagAsync(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return null;
            }

            string code = countryCode.Trim().ToLower();

            // 1. Try to load from Embedded Assembly Resources (100% offline & fast)
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = string.Format("ping2.Resources.flags.{0}.png", code);
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            ms.Position = 0;
                            return Image.FromStream(ms);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            // 2. Try to load from local file system (bin/Resources/flags or project Resources/flags)
            try
            {
                string[] searchPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "flags", code + ".png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Resources", "flags", code + ".png")
                };

                foreach (string path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        byte[] fileBytes = File.ReadAllBytes(path);
                        using (MemoryStream ms = new MemoryStream(fileBytes))
                        {
                            return Image.FromStream(ms);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            // 3. Fallback: Download from FlagCDN if not present locally
            try
            {
                string flagUrl = string.Format("https://flagcdn.com/w40/{0}.png", code);
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(flagUrl);
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    return Image.FromStream(ms);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
