using System;

namespace SalesApp.DTOs
{
    public class MatriculaRequest
    {
        public string MatriculaNumber { get; set; } = string.Empty;
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
    }
}
