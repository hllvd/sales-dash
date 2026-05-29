using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesApp.DTOs
{
    // ── Level DTOs ──────────────────────────────────────────────────────────────

    public class CreateClassificationLevelRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? Prize { get; set; }

        public decimal? SalesGoal { get; set; }
    }

    public class UpdateClassificationLevelRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? Prize { get; set; }

        public decimal? SalesGoal { get; set; }
    }

    public class ClassificationLevelResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Prize { get; set; }
        public decimal? SalesGoal { get; set; }
        public int ActiveUsersCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // ── User Classification Assignment DTOs ──────────────────────────────────────

    public class AssignUserLevelRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public int LevelId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }

    public class UpdateUserClassificationDatesRequest
    {
        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }

    public class UserClassificationResponse
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int UserInternalId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int LevelId { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public string? LevelDescription { get; set; }
        public string? LevelPrize { get; set; }
        public decimal? LevelSalesGoal { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
