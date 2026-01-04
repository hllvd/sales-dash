using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class UpdateUserMatriculaRequest
    {
        [StringLength(50)]
        [ValidateXSS]
        [ValidateSQLInjection]
        [RegularExpression(@"^[a-zA-Z0-9\-_]*$", ErrorMessage = "Matricula must be alphanumeric (hyphens and underscores allowed)")]
        public string? MatriculaNumber { get; set; }
        
        public DateTime? StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        public bool? IsActive { get; set; }
        
        public bool? IsOwner { get; set; }
    }
}
