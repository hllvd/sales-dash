using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class PagedContractResponse
    {
        public List<ContractResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public ContractAggregation? Aggregation { get; set; }
    }
}
