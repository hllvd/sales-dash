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

        /// <summary>Retention target as a percentage (0–100).</summary>
        [Range(0, 100)]
        public int? Retention { get; set; }

        /// <summary>FK to the next classification level in the chain (optional).</summary>
        public int? NextLevelId { get; set; }

        public int? MinimumDirect1LevelId { get; set; }
        public int? MinimumDirect1MinCount { get; set; }
        public int? MinimumDirect2LevelId { get; set; }
        public int? MinimumDirect2MinCount { get; set; }

        public bool ClearNextLevel { get; set; } = false;
        public bool ClearMinimumDirect1 { get; set; } = false;
        public bool ClearMinimumDirect2 { get; set; } = false;
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

        [Range(0, 100)]
        public int? Retention { get; set; }

        public int? NextLevelId { get; set; }

        /// <summary>Set to true to explicitly clear the NextLevel link (set to null).</summary>
        public bool ClearNextLevel { get; set; } = false;

        public int? MinimumDirect1LevelId { get; set; }
        public bool ClearMinimumDirect1 { get; set; } = false;
        public int? MinimumDirect1MinCount { get; set; }

        public int? MinimumDirect2LevelId { get; set; }
        public bool ClearMinimumDirect2 { get; set; } = false;
        public int? MinimumDirect2MinCount { get; set; }
    }

    public class ClassificationLevelResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Prize { get; set; }
        public decimal? SalesGoal { get; set; }
        public int? Retention { get; set; }

        public int? NextLevelId { get; set; }
        public string? NextLevelName { get; set; }

        public int? MinimumDirect1LevelId { get; set; }
        public string? MinimumDirect1LevelName { get; set; }
        public int? MinimumDirect1MinCount { get; set; }

        public int? MinimumDirect2LevelId { get; set; }
        public string? MinimumDirect2LevelName { get; set; }
        public int? MinimumDirect2MinCount { get; set; }

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
