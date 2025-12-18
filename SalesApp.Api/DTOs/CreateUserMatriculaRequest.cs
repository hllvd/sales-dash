using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class CreateUserMatriculaRequest
    {
        public Guid? UserId { get; set; }
        
        [EmailAddress]
        public string? UserEmail { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string MatriculaNumber { get; set; } = string.Empty;
        
        [Required]
        public DateTime StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        public bool IsOwner { get; set; } = false;
    }
}
