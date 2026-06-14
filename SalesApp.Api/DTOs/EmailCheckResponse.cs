namespace SalesApp.DTOs
{
    public class EmailCheckResponse
    {
        public bool Exists { get; set; }
        public string? ContactPhone { get; set; }
    }

    public class ParentAutocompleteResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
