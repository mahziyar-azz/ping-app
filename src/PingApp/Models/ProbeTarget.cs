using CommunityToolkit.Mvvm.ComponentModel;

namespace PingApp.Models
{
    public enum TargetCategory
    {
        Domestic,
        Tahrim,
        Filtered
    }

    public partial class ProbeTarget : ObservableObject
    {
        public string HostOrUrl { get; }
        public string DisplayTitle { get; }
        public TargetCategory Category { get; }

        [ObservableProperty]
        private string _statusMessage = "Idle";

        [ObservableProperty]
        private string _statusColorHex = "#888888";

        [ObservableProperty]
        private bool _isProbing;

        [ObservableProperty]
        private long _latencyMs;

        [ObservableProperty]
        private long _jitterMs;

        [ObservableProperty]
        private int _packetLossPercent;

        public ProbeTarget(string hostOrUrl, string displayTitle, TargetCategory category)
        {
            HostOrUrl = hostOrUrl;
            DisplayTitle = displayTitle;
            Category = category;
        }

        public void SetProbing()
        {
            IsProbing = true;
            StatusMessage = "Probing...";
            StatusColorHex = "#1E88E5";
        }

        public void ApplyResult(ProbeResult result)
        {
            IsProbing = false;
            LatencyMs = result.LatencyMs;
            JitterMs = result.JitterMs;
            PacketLossPercent = result.PacketLossPercent;
            StatusMessage = result.Message;
            StatusColorHex = result.StatusColorHex;
        }
    }
}
