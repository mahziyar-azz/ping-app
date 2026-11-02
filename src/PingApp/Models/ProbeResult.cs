namespace PingApp.Models
{
    public class ProbeResult
    {
        public bool Success { get; set; }
        public long LatencyMs { get; set; }
        public int PacketLossPercent { get; set; }
        public long JitterMs { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StatusColorHex { get; set; } = "#757575";
    }
}
