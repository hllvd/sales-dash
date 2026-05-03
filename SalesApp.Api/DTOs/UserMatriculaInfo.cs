namespace SalesApp.DTOs
{
    public class UserMatriculaInfo
    {
        public int Id { get; set; }           // UserMatriculas join-table PK
        public int MatriculaId { get; set; }  // Matriculas table PK
        public string MatriculaNumber { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public string Status { get; set; } = "active";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
