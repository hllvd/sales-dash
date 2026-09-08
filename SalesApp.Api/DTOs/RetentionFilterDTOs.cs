using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class RetentionFilterStatsDto
    {
        public int TotalRowsModelA { get; set; }
        public int TotalContractsModelB { get; set; }
        public int MatchedRowsModelC { get; set; }
        public int RemovedRows { get; set; }
        public double RetentionRate { get; set; }
    }

    public class RetentionFilterProcessResponse
    {
        public RetentionFilterStatsDto Stats { get; set; } = new();
        public List<string> MatchedContracts { get; set; } = new();
        public List<Dictionary<string, string>> SampleRows { get; set; } = new();
        public List<string> Headers { get; set; } = new();
    }

    public class RetentionFilterExportResult
    {
        public byte[] FileBytes { get; set; } = [];
        public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public string FileName { get; set; } = "modelo_retencao_filtrado.xlsx";
        public RetentionFilterStatsDto Stats { get; set; } = new();
    }
}
