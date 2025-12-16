namespace SalesApp.DTOs
{
    public class ContractAggregation
    {
        public decimal Total { get; set; }
        public decimal TotalCancel { get; set; }
        public decimal TotalActive { get; set; }
        public decimal TotalLate { get; set; }
        public decimal Retention { get; set; }
    }
}
