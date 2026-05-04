using System;

namespace SalesApp.Utils
{
    public static class NormalizationUtils
    {
        public static string NormalizeNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            
            var trimmed = value.Trim();
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
