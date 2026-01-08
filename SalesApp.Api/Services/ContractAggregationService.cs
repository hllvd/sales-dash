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

            // ✅ Single-pass aggregation instead of 4 separate iterations
            var aggregation = contracts.Aggregate(
                new { Total = 0m, Cancel = 0m, Active = 0m, Late = 0m },
                (acc, c) =>
                {
                    var total = acc.Total + c.TotalAmount;
                    var cancel = acc.Cancel;
                    var active = acc.Active;
                    var late = acc.Late;
                    
                    if (c.Status.Equals("Defaulted", StringComparison.OrdinalIgnoreCase))
                    {
                        cancel += c.TotalAmount;
                    }
                    else
                    {
                        active += c.TotalAmount;
                        
                        if (c.Status.Equals("Late1", StringComparison.OrdinalIgnoreCase) ||
                            c.Status.Equals("Late2", StringComparison.OrdinalIgnoreCase) ||
                            c.Status.Equals("Late3", StringComparison.OrdinalIgnoreCase))
                        {
                            late += c.TotalAmount;
                        }
                    }
                    
                    return new { Total = total, Cancel = cancel, Active = active, Late = late };
                });
            
            var retention = aggregation.Total > 0 ? aggregation.Active / aggregation.Total : 0m;

            return new ContractAggregation
            {
                Total = aggregation.Total,
                TotalCancel = aggregation.Cancel,
                TotalActive = aggregation.Active,
                TotalLate = aggregation.Late,
                Retention = retention
            };
        }
    }
}
