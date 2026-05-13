using System.Collections.Generic;

namespace SalesApp.Services
{
    public interface IWizardHeaderValidator
    {
        HeaderValidationResult Validate(IEnumerable<string> columns);
    }

    public class HeaderValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> MissingHeaders { get; set; } = new List<string>();
        public List<string> ExpectedHeaders { get; set; } = new List<string>();
    }
}
