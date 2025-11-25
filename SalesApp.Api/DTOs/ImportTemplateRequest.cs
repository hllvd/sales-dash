using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class ImportTemplateRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public List<string> RequiredFields { get; set; } = new();
        
        public List<string> OptionalFields { get; set; } = new();
        
        public Dictionary<string, string> DefaultMappings { get; set; } = new();
    }
}
