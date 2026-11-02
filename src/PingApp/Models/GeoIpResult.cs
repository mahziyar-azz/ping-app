namespace PingApp.Models
{
    public class GeoIpResult
    {
        public string Ip { get; set; } = "Unknown";
        public string Country { get; set; } = "Unknown";
        public string CountryCode { get; set; } = "un";
        public string Continent { get; set; } = "Unknown";
        public string Isp { get; set; } = "Unknown";
    }
}
