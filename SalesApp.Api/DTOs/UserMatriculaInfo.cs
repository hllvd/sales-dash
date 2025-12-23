namespace SalesApp.DTOs
{
    public class UserMatriculaInfo
    {
        public int Id { get; set; }
        public string MatriculaNumber { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
