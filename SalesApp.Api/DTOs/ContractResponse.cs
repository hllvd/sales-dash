namespace SalesApp.DTOs
{
    public class ContractResponse
    {
        public int Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? ContractType { get; set; }
        public int? Quota { get; set; }
    }
}