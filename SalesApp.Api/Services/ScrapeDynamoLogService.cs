using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;

namespace SalesApp.Services
{
    public class ScrapeLogEntry
    {
        public string JobId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Store { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FileRelativePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public interface IScrapeDynamoLogService
    {
        Task WriteJobStatusAsync(string jobId, string userId, string status, string store, string matricula, object? additionalData = null);
        Task<List<ScrapeLogEntry>> GetJobsByUserAsync(Guid userId, int limit = 50);
        Task<List<ScrapeLogEntry>> GetAllJobsAsync(int limit = 100);
    }

    public class ScrapeDynamoLogService : IScrapeDynamoLogService
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;

        public ScrapeDynamoLogService(IAmazonDynamoDB dynamoDb, IConfiguration configuration)
        {
            _dynamoDb = dynamoDb;
            _tableName = configuration["AWS:DynamoDbTable"] ?? "pbi_scrape_logs";
        }

        public async Task WriteJobStatusAsync(string jobId, string userId, string status, string store, string matricula, object? additionalData = null)
        {
            var timestamp = DateTime.UtcNow.ToString("O");
            
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"USER#{userId}" } },
                { "SK", new AttributeValue { S = $"JOB#{timestamp}#{jobId}" } },
                { "JobId", new AttributeValue { S = jobId } },
                { "UserId", new AttributeValue { S = userId } },
                { "Status", new AttributeValue { S = status } },
                { "Store", new AttributeValue { S = store } },
                { "Matricula", new AttributeValue { S = matricula } },
                { "CreatedAt", new AttributeValue { S = timestamp } }
            };

            // Global Secondary Index entry for SuperAdmin listing
            item.Add("GSI1PK", new AttributeValue { S = "ENTITY#JOB" });
            item.Add("GSI1SK", new AttributeValue { S = $"JOB#{timestamp}#{jobId}" });

            if (additionalData != null)
            {
                var json = JsonConvert.SerializeObject(additionalData);
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        if (kvp.Value == null) continue;
                        item[kvp.Key] = new AttributeValue { S = kvp.Value.ToString() };
                    }
                }
            }

            await _dynamoDb.PutItemAsync(_tableName, item);
        }

        public async Task<List<ScrapeLogEntry>> GetJobsByUserAsync(Guid userId, int limit = 50)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"USER#{userId}" } },
                    { ":sk", new AttributeValue { S = "JOB#" } }
                },
                ScanIndexForward = false, // Latest first
                Limit = limit
            };

            var response = await _dynamoDb.QueryAsync(request);
            return response.Items.Select(MapToEntry).ToList();
        }

        public async Task<List<ScrapeLogEntry>> GetAllJobsAsync(int limit = 100)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI1", // Plan assumed a GSI for global listing
                KeyConditionExpression = "GSI1PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = "ENTITY#JOB" } }
                },
                ScanIndexForward = false,
                Limit = limit
            };

            var response = await _dynamoDb.QueryAsync(request);
            return response.Items.Select(MapToEntry).ToList();
        }

        private ScrapeLogEntry MapToEntry(Dictionary<string, AttributeValue> item)
        {
            return new ScrapeLogEntry
            {
                JobId = item.GetValueOrDefault("JobId")?.S ?? "",
                UserId = item.GetValueOrDefault("UserId")?.S ?? "",
                Status = item.GetValueOrDefault("Status")?.S ?? "",
                Store = item.GetValueOrDefault("Store")?.S ?? "",
                Matricula = item.GetValueOrDefault("Matricula")?.S ?? "",
                RowCount = int.TryParse(item.GetValueOrDefault("RowCount")?.S, out var rc) ? rc : 0,
                ErrorMessage = item.GetValueOrDefault("error")?.S ?? item.GetValueOrDefault("ErrorMessage")?.S,
                FileRelativePath = item.GetValueOrDefault("fileRelativePath")?.S ?? item.GetValueOrDefault("FileRelativePath")?.S,
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S, out var d) ? d : DateTime.MinValue,
                CompletedAt = DateTime.TryParse(item.GetValueOrDefault("CompletedAt")?.S, out var cd) ? cd : (DateTime?)null
            };
        }
    }
}
