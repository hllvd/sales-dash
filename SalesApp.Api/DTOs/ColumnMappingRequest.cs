using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class ColumnMappingRequest
    {
        [Required]
        public Dictionary<string, string> Mappings { get; set; } = new();
        
        public string? SaveMappingAs { get; set; }
    }
}
