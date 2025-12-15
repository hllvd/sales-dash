using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Services
{
    public interface IContractAggregationService
    {
        ContractAggregation CalculateAggregation(IEnumerable<Contract> contracts);
    }
}
