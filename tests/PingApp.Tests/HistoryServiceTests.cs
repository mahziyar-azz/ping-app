using System.Threading.Tasks;
using PingApp.Services;
using Xunit;

namespace PingApp.Tests
{
    public class HistoryServiceTests
    {
        [Fact]
        public async Task AddRecordAsync_InsertsRecordAtBeginning()
        {
            var service = new HistoryService();
            await service.ClearHistoryAsync();

            await service.AddRecordAsync("1.1.1.1", "Australia");
            await service.AddRecordAsync("8.8.8.8", "United States");

            var records = service.Records;
            Assert.Equal(2, records.Count);
            Assert.Equal("8.8.8.8", records[0].Ip);
            Assert.Equal("1.1.1.1", records[1].Ip);
        }
    }
}
