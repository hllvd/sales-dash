namespace SalesApp.DTOs
{
    public class ContractExportRequest
    {
        public Guid? UserId { get; set; }
        public int? GroupId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? ContractNumber { get; set; }
        public bool? ShowUnassigned { get; set; }
        public string? Matricula { get; set; }
        public string? UserEmail { get; set; }
    }
}
