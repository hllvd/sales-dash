using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class UserMappingItem
    {
        [Required]
        public string SourceName { get; set; } = string.Empty;
        
        [Required]
        public string SourceSurname { get; set; } = string.Empty;
        
        [Required]
        public string Action { get; set; } = string.Empty; // "map" or "create"
        
        public Guid? TargetUserId { get; set; }
        
        public string? NewUserEmail { get; set; }
    }
    
    public class UserMappingRequest
    {
        [Required]
        public string UploadId { get; set; } = string.Empty;
        
        [Required]
        public List<UserMappingItem> UserMappings { get; set; } = new();
    }
}
