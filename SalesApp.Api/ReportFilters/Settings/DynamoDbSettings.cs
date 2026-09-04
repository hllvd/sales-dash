namespace SalesApp.ReportFilters.Settings
{
    /// <summary>
    /// Strongly-typed configuration class for DynamoDB table names.
    /// Bound from the "AWS" section in appsettings.json.
    /// </summary>
    public class DynamoDbSettings
    {
        public string ReportFiltersTable { get; set; } = string.Empty;
        public string NotificationsTable { get; set; } = string.Empty;
    }
}
