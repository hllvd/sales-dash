namespace SalesApp.DTOs
{
    public class ContractUpdateSummaryResponse
    {
        public int LessThan1Hour { get; set; }
        public int LessThan3Hours { get; set; }
        public int LessThan6Hours { get; set; }
        public int LessThan1Day { get; set; }
        public int LessThan3Days { get; set; }
        public int LessThan1Week { get; set; }
        public int OlderThan1Week { get; set; }
        public int Total { get; set; }
    }
}
