using System.ComponentModel.DataAnnotations;

namespace SalesApp.Models
{
    public class ImportRow
    {
        public int Id { get; set; }

        [Required]
        public int ImportSessionId { get; set; }

        [Required]
        public int RowIndex { get; set; } // To preserve original file order

        [Required]
        public string RowData { get; set; } = string.Empty; // JSON dictionary of the row

        // Navigation property
        public virtual ImportSession ImportSession { get; set; } = null!;
    }
}
