namespace SalesApp.DTOs
{
    public class ContractUpdateSummaryResponse
    {
        public int MoreThan1Hour { get; set; }
        public int MoreThan3Hours { get; set; }
        public int MoreThan6Hours { get; set; }
        public int MoreThan1Day { get; set; }
        public int MoreThan3Days { get; set; }
        public int MoreThan1Week { get; set; }
        public int MoreThan1Month { get; set; }
        public int Total { get; set; }
    }
}
