using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class BulkAssignMatriculasRequest
    {
        [Required]
        public List<MatriculaAssignment> Assignments { get; set; } = new();
    }
    
    public class MatriculaAssignment
    {
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string MatriculaNumber { get; set; } = string.Empty;
        
        [Required]
        public DateTime StartDate { get; set; }
    }
}
