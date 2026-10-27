using System;
using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class AdminCheckContractsRequest
    {
        public List<string> ContractNumbers { get; set; } = new();
    }

    public class AdminContractCheckItemDto
    {
        public string ContractNumber { get; set; } = string.Empty;
        public bool ExistsInSystem { get; set; }
        public bool IsDeleted { get; set; }
        public string? AssignedTo { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? SaleStartDate { get; set; }
    }

    public class AdminCheckContractsResponse
    {
        public List<AdminContractCheckItemDto> Results { get; set; } = new();
        public List<string> DuplicatesInList { get; set; } = new();
        public int TotalInput { get; set; }
        public int UniqueInput { get; set; }
        public int FoundCount { get; set; }
        public int NotFoundCount { get; set; }
        public int DeletedCount { get; set; }
    }
}
