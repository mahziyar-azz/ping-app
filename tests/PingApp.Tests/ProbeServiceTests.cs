using System.Threading.Tasks;
using PingApp.Models;
using PingApp.Services;
using Xunit;

namespace PingApp.Tests
{
    public class ProbeServiceTests
    {
        [Theory]
        [InlineData(40, 0, "#2E7D32")]   // Optimal (< 80ms)
        [InlineData(120, 0, "#558B2F")]  // Good (80 - 160ms)
        [InlineData(220, 0, "#EF6C00")]  // Moderate (160 - 260ms)
        [InlineData(350, 0, "#E65100")]  // Degraded (260 - 400ms)
        [InlineData(500, 0, "#D32F2F")]  // Severe (> 400ms)
        [InlineData(50, 10, "#E65100")]  // Any packet loss
        [InlineData(50, 60, "#D32F2F")]  // High loss
        public void GetLatencyColorHex_ReturnsAppropriateThresholdColor(long latencyMs, int loss, string expectedHex)
        {
            string colorHex = NetworkProbeService.GetLatencyColorHex(latencyMs, loss);
            Assert.Equal(expectedHex, colorHex);
        }

        [Fact]
        public async Task ProbeEndpointAsync_ReturnsFailed_WhenTargetIsEmpty()
        {
            var result = await NetworkProbeService.ProbeEndpointAsync(string.Empty, false);
            Assert.False(result.Success);
            Assert.Equal("Invalid URL", result.Message);
        }

        [Fact]
        public void ProbeTarget_ApplyResult_UpdatesPropertiesCorrectly()
        {
            var target = new ProbeTarget("google.com", "Google", TargetCategory.Domestic);
            target.SetProbing();
            Assert.True(target.IsProbing);
            Assert.Equal("Probing...", target.StatusMessage);

            var result = new ProbeResult
            {
                Success = true,
                LatencyMs = 65,
                PacketLossPercent = 0,
                JitterMs = 3,
                Message = "Ping: 65ms (±3ms)",
                StatusColorHex = "#2E7D32"
            };

            target.ApplyResult(result);

            Assert.False(target.IsProbing);
            Assert.Equal(65, target.LatencyMs);
            Assert.Equal(0, target.PacketLossPercent);
            Assert.Equal(3, target.JitterMs);
            Assert.Equal("Ping: 65ms (±3ms)", target.StatusMessage);
            Assert.Equal("#2E7D32", target.StatusColorHex);
        }
    }
}
