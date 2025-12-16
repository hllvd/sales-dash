using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Services
{
    public class ContractAggregationService : IContractAggregationService
    {
        public ContractAggregation CalculateAggregation(List<Contract> contracts)
        {
            if (contracts == null || !contracts.Any())
            {
                return new ContractAggregation
                {
                    Total = 0,
                    TotalCancel = 0,
                    Retention = 0
                };
            }

            var total = contracts.Sum(c => c.TotalAmount);
            var totalCancel = contracts
                .Where(c => c.Status.Equals("Defaulted", StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.TotalAmount);

            // Calculate retention: Active amount / Total amount
            var totalActiveAmount = contracts
                .Where(c => c.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.TotalAmount);
            
            var retention = total > 0 ? totalActiveAmount / total : 0m;

            return new ContractAggregation
            {
                Total = total,
                TotalCancel = totalCancel,
                Retention = retention
            };
        }
    }
}
