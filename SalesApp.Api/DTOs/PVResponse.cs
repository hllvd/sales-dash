namespace SalesApp.DTOs
{
    public class PVResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? MatriculaId { get; set; }
    }
}
