namespace SalesApp.DTOs
{
    public class UserStatsResponse
    {
        public int PendingContractsCount { get; set; }
        public decimal TotalProduction { get; set; }
        public decimal TotalRetention { get; set; }
        public decimal StrictRetention { get; set; }
    }
}
