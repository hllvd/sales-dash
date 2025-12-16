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
                    TotalActive = 0,
                    TotalLate = 0,
                    Retention = 0
                };
            }

            var total = contracts.Sum(c => c.TotalAmount);
            var totalCancel = contracts
                .Where(c => c.Status.Equals("Defaulted", StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.TotalAmount);

            // Calculate total active: All contracts except Defaulted (includes Active, Late1, Late2, Late3)
            var totalActiveAmount = contracts
                .Where(c => !c.Status.Equals("Defaulted", StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.TotalAmount);
            
            // Calculate total late (Late1, Late2, Late3)
            var totalLateAmount = contracts
                .Where(c => c.Status.Equals("Late1", StringComparison.OrdinalIgnoreCase) ||
                           c.Status.Equals("Late2", StringComparison.OrdinalIgnoreCase) ||
                           c.Status.Equals("Late3", StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.TotalAmount);
            
            var retention = total > 0 ? totalActiveAmount / total : 0m;

            return new ContractAggregation
            {
                Total = total,
                TotalCancel = totalCancel,
                TotalActive = totalActiveAmount,
                TotalLate = totalLateAmount,
                Retention = retention
            };
        }
    }
}
