using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class PVRequest
    {
        [Required]
        public int Id { get; set; }
        
        [Required]
        [MinLength(1)]
        public string Name { get; set; } = string.Empty;
    }
}
