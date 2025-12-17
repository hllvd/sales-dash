using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class UpdateUserMatriculaRequest
    {
        [MaxLength(50)]
        public string? MatriculaNumber { get; set; }
        
        public DateTime? StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        public bool? IsActive { get; set; }
        
        public bool? IsOwner { get; set; }
    }
}
