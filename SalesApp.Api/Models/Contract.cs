using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class Contract
    {
        public int Id { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        public decimal TotalAmount { get; set; }
        
        [Required]
        public Guid GroupId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "active"; // "active", "delinquent", "paid_off"
        
        public DateTime SaleStartDate { get; set; } = DateTime.UtcNow;
        
        public DateTime? SaleEndDate { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public User User { get; set; } = null!;
        public Group Group { get; set; } = null!;
    }
}