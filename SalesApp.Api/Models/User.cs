using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        
        public int RoleId { get; set; } = 3; // Default to user role
        
        // Navigation property
        public virtual Role? Role { get; set; } // "user", "admin", or "superadmin"
        
        public Guid? ParentUserId { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Computed property for hierarchy level
        public int Level { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public int? ImportSessionId { get; set; } // Tracks if this user was created via import
        
        // Navigation properties
        public User? ParentUser { get; set; }
        public ICollection<User> ChildUsers { get; set; } = new List<User>();
        public virtual ICollection<UserMatricula> UserMatriculas { get; set; } = new List<UserMatricula>();
        public ImportSession? ImportSession { get; set; }
    }
}