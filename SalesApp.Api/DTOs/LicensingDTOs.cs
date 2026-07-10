using System;
using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class LicensingReportResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int MinimumActiveDays { get; set; }
        public int TotalLicensedUsers { get; set; }   // users >= minimum days
        public int TotalUsersConsidered { get; set; } // all non-excluded users
        public decimal PricePerUser { get; set; }      // price of the matched tier
        public decimal TotalCost { get; set; }         // TotalLicensedUsers * PricePerUser
        public List<PriceTierDto> PriceTiers { get; set; } = new List<PriceTierDto>();
        public List<UserLicenseDetailDto> Users { get; set; } = new List<UserLicenseDetailDto>();
    }

    public class PriceTierDto
    {
        public int From { get; set; }
        public int? To { get; set; }
        public decimal PricePerUser { get; set; }
        public bool IsCurrentTier { get; set; }
    }

    public class UserLicenseDetailDto
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ActiveDaysInMonth { get; set; }
        public bool IsLicensed { get; set; }   // ActiveDaysInMonth >= MinimumActiveDays
    }
}
