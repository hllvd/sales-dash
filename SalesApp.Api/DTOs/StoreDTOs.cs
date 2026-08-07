using System;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    public class CreateStoreRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(2)]
        public string State { get; set; } = string.Empty;
    }

    public class UpdateStoreRequest
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(2)]
        public string? State { get; set; }

        public bool? IsActive { get; set; }
    }

    public class StoreResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
