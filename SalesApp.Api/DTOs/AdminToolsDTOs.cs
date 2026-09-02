using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class AdminMigrateContractsRequest
    {
        [Required]
        [EmailAddress]
        public string FromEmail { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string ToEmail { get; set; } = string.Empty;

        public bool MigrateMatricula { get; set; } = true;
    }

    public class AdminMigrateContractsResult
    {
        public int ContractsMigrated { get; set; }
        public int MatriculasMigrated { get; set; }
        public string FromUser { get; set; } = string.Empty;
        public string ToUser { get; set; } = string.Empty;
    }

    public class AdminUserSearchItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> Matriculas { get; set; } = new List<string>();
    }
}
