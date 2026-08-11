using System;
using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class ReconciledContractItemDto
    {
        public string ContractNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? UserIdentifier { get; set; }
        public string? SystemUserName { get; set; }
        public DateTime? Date { get; set; }
        public string Source { get; set; } = string.Empty; // "XLSX" | "System"
    }

    public class AmountMismatchItemDto
    {
        public string ContractNumber { get; set; } = string.Empty;
        public decimal SystemAmount { get; set; }
        public decimal XlsxAmount { get; set; }
        public decimal Difference => Math.Abs(SystemAmount - XlsxAmount);
        public string? UserIdentifier { get; set; }
        public string? SystemUserName { get; set; }
        public DateTime? SaleStartDate { get; set; }
    }

    public class ReconciliationCategorySummaryDto
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ContractReconciliationResultDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Guid? TargetUserId { get; set; }
        public string? TargetUserName { get; set; }

        public ReconciliationCategorySummaryDto MissingInSystemSummary { get; set; } = new();
        public ReconciliationCategorySummaryDto MissingInImportSummary { get; set; } = new();
        public ReconciliationCategorySummaryDto AmountMismatchSummary { get; set; } = new();
        public ReconciliationCategorySummaryDto UnassignedUserSummary { get; set; } = new();

        public List<ReconciledContractItemDto> MissingInSystem { get; set; } = new();
        public List<ReconciledContractItemDto> MissingInImport { get; set; } = new();
        public List<AmountMismatchItemDto> AmountMismatches { get; set; } = new();
        public List<ReconciledContractItemDto> UnassignedUserContracts { get; set; } = new();
    }
}
