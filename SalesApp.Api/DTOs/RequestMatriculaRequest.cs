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
        [ValidAlphanumeric]
        public string MatriculaNumber { get; set; } = string.Empty;
    }
}
