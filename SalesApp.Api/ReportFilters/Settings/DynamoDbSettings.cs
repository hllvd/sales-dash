namespace SalesApp.ReportFilters.Settings
{
    /// <summary>
    /// Strongly-typed configuration class for DynamoDB table names.
    /// Bound from the "DynamoDb" section in appsettings.json.
    /// </summary>
    public class DynamoDbSettings
    {
        public string ReportFiltersTable { get; set; } = string.Empty;
    }
}
