using System.Collections.Generic;

namespace SalesApp.Models.Configuration
{
    public class ContractStatusOptions
    {
        /// <summary>
        /// Mappings from canonical status (key) to list of alias strings (values)
        /// </summary>
        public Dictionary<string, List<string>> Mappings { get; set; } = new();
    }
}
