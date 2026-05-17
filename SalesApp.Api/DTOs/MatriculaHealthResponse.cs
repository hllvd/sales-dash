using System;

namespace SalesApp.DTOs
{
    public class MatriculaHealthResponse
    {
        public string Matricula { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
        public int ContractCount { get; set; }
        public string Status { get; set; } = "Healthy";
    }
}
