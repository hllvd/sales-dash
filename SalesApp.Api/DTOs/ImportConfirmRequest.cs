using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class ImportConfirmRequest
    {
        [Required]
        public string UploadId { get; set; } = string.Empty;
    }
}
