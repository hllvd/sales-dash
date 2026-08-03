namespace SalesApp.ReportFilters.Models
{
    /// <summary>
    /// Represents a filter field that is exported to viewers for on-the-fly override.
    /// </summary>
    public class ExportedField
    {
        /// <summary>"teams" or "emails"</summary>
        public string FieldType { get; set; } = string.Empty;

        /// <summary>Custom header label presented to the viewer (e.g. "Selecione a Equipe").</summary>
        public string Label { get; set; } = string.Empty;
    }
}
