namespace SalesApp.DTOs
{
    public class MonthlyProduction
    {
        public string Period { get; set; } = string.Empty; // Format: "YYYY-MM"
        public decimal TotalProduction { get; set; } // Sum of contract amounts started in this month
        public int ContractCount { get; set; } // Number of contracts started
    }
}
