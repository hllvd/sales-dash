namespace SalesApp.DTOs
{
    public class BulkCreateMatriculaRequest
    {
        public List<CreateUserMatriculaRequest> Matriculas { get; set; } = new();
    }
}
