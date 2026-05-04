using System;

namespace SalesApp.DTOs
{
    public class MatriculaResponse
    {
        public int Id { get; set; }
        public string MatriculaNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
    }
}
