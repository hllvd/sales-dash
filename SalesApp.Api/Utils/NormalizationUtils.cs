using System;

namespace SalesApp.Utils
{
    public static class NormalizationUtils
    {
        public static string NormalizeNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            
            var trimmed = value.Trim();

            // Ignore placeholder strings representing absent data
            if (trimmed == "-" || trimmed == "--" || trimmed == "---" ||
                trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("sem matricula", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("sem matrícula", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            
            // Remove leading zeros
            var normalized = trimmed.TrimStart('0');
            
            // If it was all zeros, return "0"
            if (normalized.Length == 0 && trimmed.Length > 0)
                return "0";
                
            return normalized;
        }

        public static int? NormalizeInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (int.TryParse(NormalizeNumber(value), out var result))
                return result;
            return null;
        }
    }
}
