using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SalesApp.Services
{
    public enum ImportErrorType
    {
        HeaderMismatch,
        OwnershipConflict,
        StatusAnomaly,
        UnmappedColumn,
        SystemError
    }

    public class SystemErrorEntry
    {
        public string ErrorId { get; set; } = string.Empty;
        public ImportErrorType Type { get; set; }
        public string EntityType { get; set; } = string.Empty; // e.g. "Contract", "User"
        public string Description { get; set; } = string.Empty;
        public string? Details { get; set; } // JSON blob of row data or missing headers
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int? ImportSessionId { get; set; }
    }

    public interface IImportErrorService
    {
        Task LogErrorAsync(ImportErrorType type, string entityType, string description, object? details = null, int? sessionId = null);
        Task<List<SystemErrorEntry>> GetErrorsAsync(int limit = 50);
    }

    public class ImportErrorService : IImportErrorService
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<ImportErrorService> _logger;

        public ImportErrorService(IAmazonDynamoDB dynamoDb, IConfiguration configuration, ILogger<ImportErrorService> logger)
        {
            _dynamoDb = dynamoDb;
            _tableName = configuration["AWS:DynamoDbTable"] ?? "pbi_scrape_logs"; // Reuse same table for now or a different one if configured
            _logger = logger;
        }

        public async Task LogErrorAsync(ImportErrorType type, string entityType, string description, object? details = null, int? sessionId = null)
        {
            var errorId = Guid.NewGuid().ToString();
            var timestamp = DateTime.UtcNow.ToString("O");
            
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"SYSTEM#ERROR#{type}" } },
                { "SK", new AttributeValue { S = $"TIMESTAMP#{timestamp}#{errorId}" } },
                { "ErrorId", new AttributeValue { S = errorId } },
                { "Type", new AttributeValue { S = type.ToString() } },
                { "EntityType", new AttributeValue { S = entityType } },
                { "Description", new AttributeValue { S = description } },
                { "Timestamp", new AttributeValue { S = timestamp } },
                { "GSI1PK", new AttributeValue { S = "ENTITY#SYSTEM_ERROR" } },
                { "GSI1SK", new AttributeValue { S = $"TIMESTAMP#{timestamp}#{errorId}" } }
            };

            if (sessionId.HasValue)
            {
                item["ImportSessionId"] = new AttributeValue { N = sessionId.Value.ToString() };
            }

            if (details != null)
            {
                item["Details"] = new AttributeValue { S = JsonConvert.SerializeObject(details) };
            }

            try
            {
                await _dynamoDb.PutItemAsync(_tableName, item);
                _logger.LogInformation($"[ImportErrorService] Logged {type} error: {description}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to log system error to DynamoDB table '{_tableName}'");
            }
        }

        public async Task<List<SystemErrorEntry>> GetErrorsAsync(int limit = 50)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI1",
                KeyConditionExpression = "GSI1PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = "ENTITY#SYSTEM_ERROR" } }
                },
                ScanIndexForward = false, // Latest first
                Limit = limit
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(item => new SystemErrorEntry
                {
                    ErrorId = item.GetValueOrDefault("ErrorId")?.S ?? "",
                    Type = Enum.TryParse<ImportErrorType>(item.GetValueOrDefault("Type")?.S, out var t) ? t : ImportErrorType.SystemError,
                    EntityType = item.GetValueOrDefault("EntityType")?.S ?? "",
                    Description = item.GetValueOrDefault("Description")?.S ?? "",
                    Details = item.GetValueOrDefault("Details")?.S,
                    Timestamp = DateTime.TryParse(item.GetValueOrDefault("Timestamp")?.S, out var d) ? d : DateTime.MinValue,
                    ImportSessionId = int.TryParse(item.GetValueOrDefault("ImportSessionId")?.N, out var sid) ? sid : null
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to query system errors from DynamoDB table '{_tableName}'");
                return new List<SystemErrorEntry>();
            }
        }
    }
}
