using System.Collections.Generic;

namespace SalesApp.Models.Configuration
{
    public class ScrapeImportOptions
    {
        public Dictionary<string, string> Mappings { get; set; } = new();
    }
}
