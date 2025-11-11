namespace SalesApp.DTOs
{
    public class UserHierarchyResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? ParentUserId { get; set; }
        public string? ParentUserName { get; set; }
        public int Level { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class UserTreeResponse
    {
        public List<UserHierarchyResponse> Users { get; set; } = new();
        public int TotalUsers { get; set; }
        public int MaxDepth { get; set; }
    }
}