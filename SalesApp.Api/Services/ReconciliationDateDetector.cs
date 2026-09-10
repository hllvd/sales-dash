using System.Globalization;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using SalesApp.Models;

namespace SalesApp.Services
{
    public static class ReconciliationDateDetector
    {
        public const string FormatDayFirst = "dd/MM/yyyy";
        public const string FormatMonthFirst = "MM/dd/yyyy";

        private static readonly Regex DateRegex = new Regex(@"^(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})", RegexOptions.Compiled);

        /// <summary>
        /// Detects the date format (dd/MM/yyyy vs MM/dd/yyyy) using 4 levels of precedence:
        /// 1. Values > 12 in day or month position
        /// 2. System contract cross-reference
        /// 3. Excel cell numberformat metadata
        /// 4. Fallback: dd/MM/yyyy (Brazil)
        /// </summary>
        public static string DetectDateFormat(
            List<Dictionary<string, string>> rows,
            Stream? fileStream,
            string[] dateAliases,
            string[] contractNumAliases,
            IReadOnlyDictionary<string, Contract>? systemContractsMap,
            Func<Dictionary<string, string>, string[], string[]?, string?> getColumnValueFunc)
        {
            // 1. Tier 1: Look for impossible month numbers (> 12)
            int dayFirstVotes = 0;
            int monthFirstVotes = 0;

            var rawDateStrings = new List<(string contractNum, string rawDate)>();

            foreach (var row in rows)
            {
                var rawDate = getColumnValueFunc(row, dateAliases, null)?.Trim();
                var contractNum = getColumnValueFunc(row, contractNumAliases, null)?.Trim();

                if (string.IsNullOrWhiteSpace(rawDate))
                    continue;

                rawDateStrings.Add((contractNum ?? string.Empty, rawDate));

                var match = DateRegex.Match(rawDate);
                if (match.Success &&
                    int.TryParse(match.Groups[1].Value, out int part1) &&
                    int.TryParse(match.Groups[2].Value, out int part2))
                {
                    if (part1 > 12 && part1 <= 31 && part2 <= 12)
                    {
                        dayFirstVotes++;
                    }
                    else if (part2 > 12 && part2 <= 31 && part1 <= 12)
                    {
                        monthFirstVotes++;
                    }
                }
            }

            if (dayFirstVotes > 0 && monthFirstVotes == 0)
            {
                return FormatDayFirst;
            }
            if (monthFirstVotes > 0 && dayFirstVotes == 0)
            {
                return FormatMonthFirst;
            }
            if (dayFirstVotes > 0 || monthFirstVotes > 0)
            {
                // If mixed (rare), return the majority
                return dayFirstVotes >= monthFirstVotes ? FormatDayFirst : FormatMonthFirst;
            }

            // 2. Tier 2: Cross-reference with system contracts
            if (systemContractsMap != null && systemContractsMap.Count > 0)
            {
                int sysDayFirstMatches = 0;
                int sysMonthFirstMatches = 0;

                foreach (var (contractNum, rawDate) in rawDateStrings)
                {
                    if (!string.IsNullOrWhiteSpace(contractNum) && systemContractsMap.TryGetValue(contractNum, out var systemContract))
                    {
                        var parsedDayFirst = TryParseDateWithPattern(rawDate, FormatDayFirst);
                        var parsedMonthFirst = TryParseDateWithPattern(rawDate, FormatMonthFirst);

                        var sysDate = systemContract.SaleStartDate.Date;

                        if (parsedDayFirst.HasValue && parsedDayFirst.Value.Date == sysDate)
                        {
                            sysDayFirstMatches++;
                        }
                        if (parsedMonthFirst.HasValue && parsedMonthFirst.Value.Date == sysDate)
                        {
                            sysMonthFirstMatches++;
                        }
                    }
                }

                if (sysDayFirstMatches > sysMonthFirstMatches)
                {
                    return FormatDayFirst;
                }
                if (sysMonthFirstMatches > sysDayFirstMatches)
                {
                    return FormatMonthFirst;
                }
            }

            // 3. Tier 3: Excel cell numberformat metadata
            if (fileStream != null && fileStream.CanRead)
            {
                try
                {
                    fileStream.Position = 0;
                    ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
                    using var package = new ExcelPackage(fileStream);
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet != null && worksheet.Dimension != null)
                    {
                        // Find date column index in row 1
                        int dateCol = -1;
                        for (int c = 1; c <= worksheet.Dimension.Columns; c++)
                        {
                            var headerVal = worksheet.Cells[1, c].Value?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(headerVal))
                            {
                                var normHeader = headerVal.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
                                if (dateAliases.Any(a => normHeader.Contains(a.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", ""))))
                                {
                                    dateCol = c;
                                    break;
                                }
                            }
                        }

                        if (dateCol > 0)
                        {
                            int sampleRows = Math.Min(worksheet.Dimension.Rows, 50);
                            for (int r = 2; r <= sampleRows; r++)
                            {
                                var cell = worksheet.Cells[r, dateCol];
                                var format = cell.Style.Numberformat.Format?.ToLowerInvariant();
                                if (!string.IsNullOrWhiteSpace(format))
                                {
                                    int dIndex = format.IndexOf('d');
                                    int mIndex = format.IndexOf('m');

                                    if (dIndex >= 0 && mIndex >= 0)
                                    {
                                        if (dIndex < mIndex)
                                            return FormatDayFirst;
                                        if (mIndex < dIndex)
                                            return FormatMonthFirst;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fall through to default if stream cannot be read as EPPlus package
                }
            }

            // 4. Tier 4: Fallback Default (Brazil)
            return FormatDayFirst;
        }

        public static DateTime? TryParseDateWithPattern(string? rawValue, string pattern)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            var clean = rawValue.Trim();

            // Supported formats depending on pattern
            string[] formats = pattern == FormatDayFirst
                ? new[]
                {
                    "dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy",
                    "dd-MM-yyyy", "d-M-yyyy", "dd-M-yyyy", "d-MM-yyyy",
                    "dd.MM.yyyy", "d.M.yyyy",
                    "dd/MM/yyyy HH:mm:ss", "d/M/yyyy HH:mm:ss",
                    "dd/MM/yyyy HH:mm", "d/M/yyyy HH:mm",
                    "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"
                }
                : new[]
                {
                    "MM/dd/yyyy", "M/d/yyyy", "M/dd/yyyy", "MM/d/yyyy",
                    "MM-dd-yyyy", "M-d-yyyy", "M-dd-yyyy", "MM-d-yyyy",
                    "MM.dd.yyyy", "M.d.yyyy",
                    "MM/dd/yyyy HH:mm:ss", "M/d/yyyy HH:mm:ss",
                    "MM/dd/yyyy HH:mm", "M/d/yyyy HH:mm",
                    "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"
                };

            if (DateTime.TryParseExact(clean, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            // Fallback general parse
            if (pattern == FormatDayFirst && DateTime.TryParse(clean, new CultureInfo("pt-BR"), DateTimeStyles.None, out var dtPt))
            {
                return dtPt;
            }

            if (pattern == FormatMonthFirst && DateTime.TryParse(clean, new CultureInfo("en-US"), DateTimeStyles.None, out var dtEn))
            {
                return dtEn;
            }

            if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtInv))
            {
                return dtInv;
            }

            return null;
        }

        public static (DateTime? minDate, DateTime? maxDate, string detectedFormat) DetectDateRange(
            List<Dictionary<string, string>> rows,
            Stream? fileStream,
            string[] dateAliases,
            string[] contractNumAliases,
            IReadOnlyDictionary<string, Contract>? systemContractsMap,
            Func<Dictionary<string, string>, string[], string[]?, string?> getColumnValueFunc)
        {
            var detectedFormat = DetectDateFormat(rows, fileStream, dateAliases, contractNumAliases, systemContractsMap, getColumnValueFunc);

            DateTime? minDate = null;
            DateTime? maxDate = null;

            foreach (var row in rows)
            {
                var raw = getColumnValueFunc(row, dateAliases, null)?.Trim();
                var dt = TryParseDateWithPattern(raw, detectedFormat);
                if (dt.HasValue)
                {
                    if (!minDate.HasValue || dt.Value < minDate.Value)
                    {
                        minDate = dt.Value;
                    }
                    if (!maxDate.HasValue || dt.Value > maxDate.Value)
                    {
                        maxDate = dt.Value;
                    }
                }
            }

            return (minDate, maxDate, detectedFormat);
        }
    }
}
