using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }
        
        private Guid _userId;
        [Required]
        public Guid UserId
        {
            get => User?.Id ?? _userId;
            set => _userId = value;
        }

        [Required]
        public int UserInternalId { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Token { get; set; } = string.Empty;
        
        public DateTime ExpiresAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsRevoked { get; set; } = false;
        
        public DateTime? RevokedAt { get; set; }
        
        // Navigation property
        public User? User { get; set; }
    }
}
