using System.Drawing;
using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SalesApp.Models;

namespace SalesApp.Utils
{
    public enum GroupStatusFilter
    {
        Active,
        Inactive,
        All
    }

    public static class GroupTableHelper
    {
        private static readonly CompareInfo PtBrCompareInfo = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

        public static readonly IReadOnlyList<string> ExportHeaders = new[]
        {
            "ID",
            "Nome",
            "Descrição",
            "Segmento",
            "Comissão",
            "Status"
        };

        public static bool TryParseStatusFilter(string? rawStatus, out GroupStatusFilter filter)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                filter = GroupStatusFilter.Active;
                return true;
            }

            switch (rawStatus.Trim().ToLowerInvariant())
            {
                case "active":
                    filter = GroupStatusFilter.Active;
                    return true;
                case "inactive":
                    filter = GroupStatusFilter.Inactive;
                    return true;
                case "all":
                    filter = GroupStatusFilter.All;
                    return true;
                default:
                    filter = GroupStatusFilter.Active;
                    return false;
            }
        }

        public static IReadOnlyList<Group> FilterAndSortGroups(
            IEnumerable<Group> groups,
            GroupStatusFilter statusFilter,
            string? search)
        {
            var hasSearch = !string.IsNullOrWhiteSpace(search);
            var query = search ?? string.Empty;

            var filtered = groups.Where(group =>
            {
                if (statusFilter == GroupStatusFilter.Active && !group.IsActive) return false;
                if (statusFilter == GroupStatusFilter.Inactive && group.IsActive) return false;

                if (hasSearch)
                {
                    var matchesName = !string.IsNullOrEmpty(group.Name) &&
                                      PtBrCompareInfo.IndexOf(group.Name, query, CompareOptions.IgnoreCase) >= 0;
                    var matchesDesc = !string.IsNullOrEmpty(group.Description) &&
                                      PtBrCompareInfo.IndexOf(group.Description, query, CompareOptions.IgnoreCase) >= 0;
                    var segmentLabel = SegmentExtensions.ToDisplayName(group.Segment) ?? string.Empty;
                    var matchesSegment = !string.IsNullOrEmpty(segmentLabel) &&
                                         PtBrCompareInfo.IndexOf(segmentLabel, query, CompareOptions.IgnoreCase) >= 0;

                    return matchesName || matchesDesc || matchesSegment;
                }

                return true;
            });

            var result = filtered.ToList();
            result.Sort(CompareGroupsBySegmentAndName);
            return result;
        }

        public static int CompareGroupsBySegmentAndName(Group a, Group b)
        {
            var aHasSegment = a.Segment.HasValue;
            var bHasSegment = b.Segment.HasValue;

            if (!aHasSegment && bHasSegment) return -1;
            if (aHasSegment && !bHasSegment) return 1;

            if (aHasSegment && bHasSegment && a.Segment!.Value != b.Segment!.Value)
            {
                return a.Segment.Value.CompareTo(b.Segment.Value);
            }

            return CompareNaturalPtBr(a.Name, b.Name);
        }

        /// <summary>
        /// Mirrors JavaScript localeCompare('pt-BR', { numeric: true, sensitivity: 'base' }).
        /// </summary>
        public static int CompareNaturalPtBr(string? a, string? b)
        {
            var strA = a ?? string.Empty;
            var strB = b ?? string.Empty;

            int i = 0;
            int j = 0;

            while (i < strA.Length && j < strB.Length)
            {
                bool isDigitA = char.IsDigit(strA[i]);
                bool isDigitB = char.IsDigit(strB[j]);

                if (isDigitA && isDigitB)
                {
                    int startA = i;
                    while (i < strA.Length && char.IsDigit(strA[i])) i++;
                    int startB = j;
                    while (j < strB.Length && char.IsDigit(strB[j])) j++;

                    var numCompare = CompareNumericChunks(
                        strA.AsSpan(startA, i - startA),
                        strB.AsSpan(startB, j - startB));
                    if (numCompare != 0) return numCompare;
                }
                else
                {
                    int startA = i;
                    while (i < strA.Length && !char.IsDigit(strA[i])) i++;
                    int startB = j;
                    while (j < strB.Length && !char.IsDigit(strB[j])) j++;

                    var textCompare = PtBrCompareInfo.Compare(
                        strA.AsSpan(startA, i - startA),
                        strB.AsSpan(startB, j - startB),
                        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
                    if (textCompare != 0) return textCompare;
                }
            }

            return (strA.Length - i).CompareTo(strB.Length - j);
        }

        private static int CompareNumericChunks(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            var trimmedA = a.TrimStart('0');
            var trimmedB = b.TrimStart('0');

            if (trimmedA.Length != trimmedB.Length)
            {
                return trimmedA.Length.CompareTo(trimmedB.Length);
            }

            for (int k = 0; k < trimmedA.Length; k++)
            {
                if (trimmedA[k] != trimmedB[k])
                {
                    return trimmedA[k].CompareTo(trimmedB[k]);
                }
            }

            return 0;
        }

        public static byte[] BuildGroupsXlsx(IReadOnlyList<Group> groups)
        {
            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Grupos");

            for (int col = 0; col < ExportHeaders.Count; col++)
            {
                var cell = worksheet.Cells[1, col + 1];
                cell.Value = ExportHeaders[col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(243, 244, 246));
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }

            for (int rowIdx = 0; rowIdx < groups.Count; rowIdx++)
            {
                var group = groups[rowIdx];
                var excelRow = rowIdx + 2;

                worksheet.Cells[excelRow, 1].Value = group.Id;
                worksheet.Cells[excelRow, 2].Value = group.Name ?? string.Empty;
                worksheet.Cells[excelRow, 3].Value = string.IsNullOrEmpty(group.Description) ? "-" : group.Description;
                worksheet.Cells[excelRow, 4].Value = SegmentExtensions.ToDisplayName(group.Segment) ?? "Não Definido";
                worksheet.Cells[excelRow, 5].Value = $"{group.Commission.ToString("0.######", CultureInfo.InvariantCulture)}%";
                worksheet.Cells[excelRow, 6].Value = group.IsActive ? "Ativo" : "Inativo";
            }

            worksheet.Cells.AutoFitColumns();
            return package.GetAsByteArray();
        }
    }
}
