using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class RequestMatriculaRequest
    {
        [Required]
        [StringLength(50)]
        [ValidateXSS]
        [ValidateSQLInjection]
        [RegularExpression(@"^[a-zA-Z0-9\-_]*$", ErrorMessage = "Matricula must be alphanumeric (hyphens and underscores allowed)")]
        public string MatriculaNumber { get; set; } = string.Empty;
    }
}
