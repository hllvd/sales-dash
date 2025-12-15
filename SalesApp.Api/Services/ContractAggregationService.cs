using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Services
{
    public class ContractAggregationService : IContractAggregationService
    {
        public ContractAggregation CalculateAggregation(IEnumerable<Contract> contracts)
        {
            var contractList = contracts.ToList();
            var totalCount = contractList.Count;
            
            // Count defaulted contracts
            var defaultedCount = contractList.Count(c => 
                c.Status.Equals("Defaulted", StringComparison.OrdinalIgnoreCase));
            
            // Count non-defaulted contracts (Active, Late1, Late2, Late3)
            var nonDefaultedCount = totalCount - defaultedCount;
            
            // Calculate retention rate (non-defaulted / total)
            var retention = totalCount > 0 ? (decimal)nonDefaultedCount / totalCount : 0m;
            
            return new ContractAggregation
            {
                Total = contractList.Sum(c => c.TotalAmount),
                TotalCancel = contractList
                    .Where(c => c.Status.Equals("Defaulted", StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.TotalAmount),
                Retention = retention
            };
        }
    }
}
