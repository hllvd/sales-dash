using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class UserMetadataField
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Label { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? GroupLabel { get; set; }

        [Required]
        [MaxLength(50)]
        public string FieldType { get; set; } = "text"; // "text" or "dropdown"

        public string? DropdownOptions { get; set; } // JSON serialized array of options e.g. ["Option A", "Option B"]

        public int DisplayOrder { get; set; } = 0;

        public bool IsRequired { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<UserMetadataValue> Values { get; set; } = new List<UserMetadataValue>();
    }
}
