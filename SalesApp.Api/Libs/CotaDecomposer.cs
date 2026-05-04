using System.Linq;
using SalesApp.Utils;

namespace SalesApp.Libs
{
    public class CotaInfo
    {
        public string? Group { get; set; }
        public string? Matricula { get; set; }
        public string? Customer { get; set; }
        public string? Contract { get; set; }
        public bool IsFromConcatenatedString { get; set; }
    }

    public static class CotaDecomposer
    {
        /// <summary>
        /// Decomposes a concatenated Cota string into its constituent parts
        /// (e.g. '012173;4103;0;MARIO;1100326334' -> Group='012173', Matricula='4103', Customer='MARIO', Contract='1100326334')
        /// </summary>
        public static CotaInfo Decompose(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new CotaInfo { IsFromConcatenatedString = false };
            }

            if (rawValue.Contains(";"))
            {
                var parts = rawValue.Split(';');
                if (parts.Length >= 5)
                {
                    return new CotaInfo
                    {
                        Group = NormalizationUtils.NormalizeNumber(parts[0]),
                        Matricula = NormalizationUtils.NormalizeNumber(parts[1]),
                        Customer = parts[3].Trim(),
                        Contract = NormalizationUtils.NormalizeNumber(parts[^1]),
                        IsFromConcatenatedString = true
                    };
                }
                
                // Fallback for strings with semicolons but less than 5 parts
                return new CotaInfo
                {
                    Contract = NormalizationUtils.NormalizeNumber(parts.LastOrDefault()),
                    IsFromConcatenatedString = true
                };
            }

            // Standard contract number
            return new CotaInfo
            {
                Contract = NormalizationUtils.NormalizeNumber(rawValue),
                IsFromConcatenatedString = false
            };
        }
    }
}
