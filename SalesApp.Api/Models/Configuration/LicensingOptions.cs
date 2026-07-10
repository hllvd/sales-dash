using System.Collections.Generic;

namespace SalesApp.Models.Configuration
{
    public class LicensingOptions
    {
        public int DefaultMinimumActiveDays { get; set; } = 15;
        public List<string> ExcludedEmails { get; set; } = new List<string>();
        public List<PriceTier> PriceTiers { get; set; } = new List<PriceTier>();
    }

    public class PriceTier
    {
        public int From { get; set; }
        public int? To { get; set; } // null represents unbounded upper limit
        public decimal PricePerUser { get; set; }
    }
}
