using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class TeamMatriculaHealthResponse
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int TotalMatriculas { get; set; }
        public string WorstStatus { get; set; } = "Healthy";
        public List<MatriculaHealthResponse> Matriculas { get; set; } = new();
    }
}
