namespace SalesApp.DTOs
{
    public class UserLookupByMatriculaResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int MatriculaId { get; set; }
        public string MatriculaNumber { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
    }
}
