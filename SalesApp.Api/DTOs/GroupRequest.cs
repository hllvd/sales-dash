using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class GroupRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [Range(0, 100)]
        public decimal Commission { get; set; } = 0;
        
        public int? Segment { get; set; }
    }
    
    public class UpdateGroupRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Range(0, 100)]
        public decimal? Commission { get; set; }
        
        public int? Segment { get; set; }
        
        public bool? IsActive { get; set; }
    }
}