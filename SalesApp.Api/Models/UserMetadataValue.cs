using System;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class UserMetadataValue
    {
        public int Id { get; set; }

        public int UserInternalId { get; set; }

        public int UserMetadataFieldId { get; set; }

        [MaxLength(500)]
        public string? Value { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual UserMetadataField Field { get; set; } = null!;
    }
}
