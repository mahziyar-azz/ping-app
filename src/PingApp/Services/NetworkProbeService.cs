using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PingApp.Models;

namespace PingApp.Services
{
    public static class NetworkProbeService
    {
        private static readonly HttpClientHandler _handler;
        private static readonly HttpClient _httpClient;
        public const int SingleTimeoutMs = 1200;
        public const int PingCount = 3;

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

        public static string GetLatencyColorHex(long latencyMs, int packetLoss = 0)
        {
            if (packetLoss >= 50)
            {
                return "#D32F2F"; // Red
            }
            if (packetLoss > 0)
            {
                return "#E65100"; // Deep Orange
            }

            if (latencyMs <= 80)
            {
                return "#2E7D32"; // Dark Green
            }
            if (latencyMs <= 160)
            {
                return "#558B2F"; // Olive/Forest Green
            }
            if (latencyMs <= 260)
            {
                return "#EF6C00"; // Amber
            }
            if (latencyMs <= 400)
            {
                return "#E65100"; // Deep Orange
            }

            return "#D32F2F"; // Red
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
                    StatusColorHex = "#B71C1C"
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
            var latencies = new List<long>();
            int failedCount = 0;
            int lastBlockedStatusCode = 0;

            for (int i = 0; i < PingCount; i++)
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    using var request = new HttpRequestMessage(HttpMethod.Head, formattedUrl);
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    stopwatch.Stop();

                    int statusCode = (int)response.StatusCode;
                    if (response.IsSuccessStatusCode || statusCode is 301 or 302 or 307 or 308)
                    {
                        latencies.Add(stopwatch.ElapsedMilliseconds);
                    }
                    else if (statusCode is 403 or 451)
                    {
                        lastBlockedStatusCode = statusCode;
                        failedCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch
                {
                    failedCount++;
                }

                if (i < PingCount - 1)
                {
                    await Task.Delay(50);
                }
            }

            if (lastBlockedStatusCode > 0 && latencies.Count == 0)
            {
                return new ProbeResult
                {
                    Success = false,
                    LatencyMs = 0,
                    PacketLossPercent = 100,
                    Message = $"Blocked/Tahrim ({lastBlockedStatusCode})",
                    StatusColorHex = "#D32F2F"
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
                    Message = "HTTP Failed (100% Loss)",
                    StatusColorHex = "#B71C1C"
                };
            }

            long avgLatency = (long)latencies.Average();
            long jitter = latencies.Count > 1 ? (latencies.Max() - latencies.Min()) : 0;

            string message = packetLoss > 0
                ? $"HTTP: {avgLatency}ms ({packetLoss}% Loss)"
                : $"HTTP: {avgLatency}ms (±{jitter}ms)";

            return new ProbeResult
            {
                Success = true,
                LatencyMs = avgLatency,
                PacketLossPercent = packetLoss,
                JitterMs = jitter,
                Message = message,
                StatusColorHex = GetLatencyColorHex(avgLatency, packetLoss)
            };
        }

        public static async Task<ProbeResult> ProbeViaIcmpBurstAsync(string hostOrIp)
        {
            var latencies = new List<long>();
            int failedCount = 0;

            using (var ping = new Ping())
            {
                for (int i = 0; i < PingCount; i++)
                {
                    try
                    {
                        var reply = await ping.SendPingAsync(hostOrIp, SingleTimeoutMs);
                        if (reply != null && reply.Status == IPStatus.Success)
                        {
                            latencies.Add(reply.RoundtripTime);
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                    catch
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
                    Message = "Ping Failed (100% Loss)",
                    StatusColorHex = "#B71C1C"
                };
            }

            long avgLatency = (long)latencies.Average();
            long jitter = latencies.Count > 1 ? (latencies.Max() - latencies.Min()) : 0;

            string message = packetLoss > 0
                ? $"Ping: {avgLatency}ms ({packetLoss}% Loss)"
                : $"Ping: {avgLatency}ms (±{jitter}ms)";

            return new ProbeResult
            {
                Success = true,
                LatencyMs = avgLatency,
                PacketLossPercent = packetLoss,
                JitterMs = jitter,
                Message = message,
                StatusColorHex = GetLatencyColorHex(avgLatency, packetLoss)
            };
        }

        public static async Task<GeoIpResult> FetchGeoIpAsync()
        {
            // Provider 1: ip-api.com
            try
            {
                using var cts = new CancellationTokenSource(3000);
                var response = await _httpClient.GetAsync("http://ip-api.com/json/", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                    {
                        string timezone = root.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? "" : "";
                        return new GeoIpResult
                        {
                            Ip = root.TryGetProperty("query", out var q) ? q.GetString() ?? "Unknown" : "Unknown",
                            Country = root.TryGetProperty("country", out var c) ? c.GetString() ?? "Unknown" : "Unknown",
                            CountryCode = root.TryGetProperty("countryCode", out var cc) ? cc.GetString() ?? "un" : "un",
                            Continent = timezone.Contains('/') ? timezone.Split('/')[0] : "Unknown",
                            Isp = root.TryGetProperty("isp", out var isp) ? isp.GetString() ?? "Unknown" : "Unknown"
                        };
                    }
                }
            }
            catch { }

            // Provider 2: ipwho.is
            try
            {
                using var cts = new CancellationTokenSource(3000);
                var response = await _httpClient.GetAsync("https://ipwho.is/", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("success", out var succ) && succ.GetBoolean())
                    {
                        string ispName = "Unknown";
                        if (root.TryGetProperty("connection", out var conn) && conn.TryGetProperty("isp", out var isp))
                        {
                            ispName = isp.GetString() ?? "Unknown";
                        }

                        return new GeoIpResult
                        {
                            Ip = root.TryGetProperty("ip", out var ip) ? ip.GetString() ?? "Unknown" : "Unknown",
                            Country = root.TryGetProperty("country", out var c) ? c.GetString() ?? "Unknown" : "Unknown",
                            CountryCode = root.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? "un" : "un",
                            Continent = root.TryGetProperty("continent", out var cont) ? cont.GetString() ?? "Unknown" : "Unknown",
                            Isp = ispName
                        };
                    }
                }
            }
            catch { }

            // Provider 3: freeipapi.com
            try
            {
                using var cts = new CancellationTokenSource(3000);
                var response = await _httpClient.GetAsync("https://freeipapi.com/api/json", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    string ispName = root.TryGetProperty("asnOrganization", out var asn) ? asn.GetString() ?? "Unknown" : "Unknown";

                    return new GeoIpResult
                    {
                        Ip = root.TryGetProperty("ipAddress", out var ip) ? ip.GetString() ?? "Unknown" : "Unknown",
                        Country = root.TryGetProperty("countryName", out var c) ? c.GetString() ?? "Unknown" : "Unknown",
                        CountryCode = root.TryGetProperty("countryCode", out var cc) ? cc.GetString() ?? "un" : "un",
                        Continent = root.TryGetProperty("continent", out var cont) ? cont.GetString() ?? "Unknown" : "Unknown",
                        Isp = ispName
                    };
                }
            }
            catch { }

            // Provider 4: ip.guide
            try
            {
                using var cts = new CancellationTokenSource(3000);
                var response = await _httpClient.GetAsync("https://ip.guide/", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string ip = root.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() ?? "Unknown" : "Unknown";
                    string country = "Unknown";
                    string countryCode = "un";
                    string continent = "Unknown";
                    string isp = "Unknown";

                    if (root.TryGetProperty("location", out var loc))
                    {
                        if (loc.TryGetProperty("country", out var cProp)) country = cProp.GetString() ?? "Unknown";
                        if (loc.TryGetProperty("timezone", out var tzProp))
                        {
                            string tz = tzProp.GetString() ?? "";
                            continent = tz.Contains('/') ? tz.Split('/')[0] : "Unknown";
                        }
                    }

                    if (root.TryGetProperty("network", out var net) && net.TryGetProperty("autonomous_system", out var asInfo))
                    {
                        if (asInfo.TryGetProperty("name", out var nProp)) isp = nProp.GetString() ?? "Unknown";
                        if (asInfo.TryGetProperty("country", out var ccProp)) countryCode = ccProp.GetString() ?? "un";
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
            catch { }

            throw new InvalidOperationException("All GeoIP services failed to respond.");
        }

        public static async Task<Bitmap?> FetchFlagAsync(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return null;
            }

            string code = countryCode.Trim().ToLowerInvariant();

            // 1. Try to load from embedded Avalonia resource
            try
            {
                var uri = new Uri($"avares://PingApp/Assets/flags/{code}.png");
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }
            }
            catch { }

            // 2. Try to load from local file system
            try
            {
                string[] searchPaths =
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "flags", $"{code}.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "flags", $"{code}.png")
                };

                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        return new Bitmap(path);
                    }
                }
            }
            catch { }

            // 3. Fallback: Download from FlagCDN
            try
            {
                string flagUrl = $"https://flagcdn.com/w40/{code}.png";
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(flagUrl);
                using var ms = new MemoryStream(imageBytes);
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }
    }
}
