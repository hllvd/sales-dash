using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class ColumnMappingRequest
    {
        [Required]
        public Dictionary<string, string> Mappings { get; set; } = new();
        
        public string? SaveMappingAs { get; set; }
        public bool AllowAutoCreateGroups { get; set; } = false;
        public bool AllowAutoCreatePVs { get; set; } = false;
        public bool SkipMissingContractNumber { get; set; } = false;
    }
}
