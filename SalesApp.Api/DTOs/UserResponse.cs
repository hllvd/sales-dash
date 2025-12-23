namespace SalesApp.DTOs
{
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? ParentUserId { get; set; }
        public string? ParentUserName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Matricula information (primary/owner matricula)
        public int? MatriculaId { get; set; }
        public string? MatriculaNumber { get; set; }
        public bool IsMatriculaOwner { get; set; }
        
        // Active matriculas for current user
        public List<UserMatriculaInfo> ActiveMatriculas { get; set; } = new();
    }
    
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserResponse User { get; set; } = new();
    }
    
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Aggregation { get; set; }
    }
    
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}