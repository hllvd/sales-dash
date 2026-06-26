using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class ContractMigrationPreviewItem
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? CurrentMatriculaId { get; set; }
        public string CurrentMatriculaNumber { get; set; } = string.Empty;
        public int TargetMatriculaId { get; set; }
        public string TargetMatriculaNumber { get; set; } = string.Empty;
        public bool IsAutoSelected { get; set; }
    }

    public class ContractMigrationRequest
    {
        public List<ContractMatriculaMapping>? Mappings { get; set; }
    }

    public class ContractMatriculaMapping
    {
        [Required]
        public int ContractId { get; set; }

        [Required]
        public int TargetMatriculaId { get; set; }
    }

    public class ContractMigrationResult
    {
        public int MigratedCount { get; set; }
    }
}
