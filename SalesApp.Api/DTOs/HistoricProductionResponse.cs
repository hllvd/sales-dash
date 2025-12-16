namespace SalesApp.DTOs
{
    public class HistoricProductionResponse
    {
        public List<MonthlyProduction> MonthlyData { get; set; } = new();
        public decimal TotalProduction { get; set; }
        public int TotalContracts { get; set; }
    }
}
