namespace SalesApp.DTOs
{
    public class PendingClaimRequest
    {
        public string ContractNumber { get; set; } = string.Empty;
        /// <summary>The UserMatriculas join-table ID (not Matriculas.Id).</summary>
        public int UserMatriculaId { get; set; }
    }

    public class PendingClaimResponse
    {
        public int Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int MatriculaId { get; set; }
        public string MatriculaNumber { get; set; } = string.Empty;
        public DateTime ClaimedAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
