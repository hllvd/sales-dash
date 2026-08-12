using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SalesApp.Services
{
    public class ScrapeLogEntry
    {
        public string JobId { get; set; } = string.Empty;
        public string RunId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Store { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FileRelativePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class ScrapeRunSummary
    {
        public string RunId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string FinalStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int TotalJobs { get; set; }
        public int SucceededJobs { get; set; }
        public int FailedJobs { get; set; }
        public int TotalRowCount { get; set; }
        public List<string> Stores { get; set; } = new List<string>();
        public List<string> Matriculas { get; set; } = new List<string>();
    }

    public class ScrapeRunDetail
    {
        public string RunId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string FinalStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<ScrapeLogEntry> Jobs { get; set; } = new List<ScrapeLogEntry>();
    }

    public interface IScrapeDynamoLogService
    {
        Task WriteJobStatusAsync(string jobId, string userId, string status, string store, string matricula, string? runId = null, string? userEmail = null, object? additionalData = null);
        Task<List<ScrapeLogEntry>> GetJobsByUserAsync(Guid userId, int limit = 50);
        Task<List<ScrapeLogEntry>> GetAllJobsAsync(int limit = 100);
        Task<List<ScrapeRunSummary>> GetRunsByUserAsync(Guid userId, int limit = 100);
        Task<List<ScrapeRunSummary>> GetAllRunsAsync(int limit = 200);
        Task<ScrapeRunDetail?> GetRunDetailAsync(string runId, Guid? userId = null);
    }

    public class ScrapeDynamoLogService : IScrapeDynamoLogService
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<ScrapeDynamoLogService> _logger;

        public ScrapeDynamoLogService(IAmazonDynamoDB dynamoDb, IConfiguration configuration, ILogger<ScrapeDynamoLogService> logger)
        {
            _dynamoDb = dynamoDb;
            _tableName = configuration["AWS:DynamoDbTable"] ?? "pbi_scrape_logs";
            _logger = logger;
        }

        public async Task WriteJobStatusAsync(string jobId, string userId, string status, string store, string matricula, string? runId = null, string? userEmail = null, object? additionalData = null)
        {
            var timestamp = DateTime.UtcNow.ToString("O");
            
            var effectiveRunId = runId;
            var effectiveUserEmail = userEmail;

            // If updating job status and runId/userEmail were not supplied, retrieve from existing job record
            if ((string.IsNullOrEmpty(effectiveRunId) || string.IsNullOrEmpty(effectiveUserEmail)) && Guid.TryParse(userId, out var userGuid))
            {
                var existingJobs = await GetJobsByUserAsync(userGuid, 100);
                var existing = existingJobs.FirstOrDefault(j => j.JobId == jobId);
                if (existing != null)
                {
                    if (string.IsNullOrEmpty(effectiveRunId) && !string.IsNullOrEmpty(existing.RunId)) effectiveRunId = existing.RunId;
                    if (string.IsNullOrEmpty(effectiveUserEmail) && !string.IsNullOrEmpty(existing.UserEmail)) effectiveUserEmail = existing.UserEmail;
                }
            }

            effectiveRunId ??= jobId;
            effectiveUserEmail ??= string.Empty;

            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"USER#{userId}" } },
                { "SK", new AttributeValue { S = $"JOB#{timestamp}#{jobId}" } },
                { "JobId", new AttributeValue { S = jobId } },
                { "RunId", new AttributeValue { S = effectiveRunId } },
                { "UserId", new AttributeValue { S = userId } },
                { "UserEmail", new AttributeValue { S = effectiveUserEmail } },
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

            try
            {
                await _dynamoDb.PutItemAsync(_tableName, item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to write job status to DynamoDB table '{_tableName}'");
            }
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
                ScanIndexForward = false,
                Limit = limit
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(MapToEntry).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to query jobs for user {userId} from DynamoDB table '{_tableName}'. Returning empty list.");
                return new List<ScrapeLogEntry>();
            }
        }

        public async Task<List<ScrapeLogEntry>> GetAllJobsAsync(int limit = 100)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI1",
                KeyConditionExpression = "GSI1PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = "ENTITY#JOB" } }
                },
                ScanIndexForward = false,
                Limit = limit
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                var items = response.Items.Select(MapToEntry).ToList();
                if (items.Any()) return items;
                
                // Fallback to scan if GSI returns empty
                return await ScanAllJobsAsync(limit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to query all jobs via GSI1 from DynamoDB table '{_tableName}'. Falling back to Scan.");
                return await ScanAllJobsAsync(limit);
            }
        }

        private async Task<List<ScrapeLogEntry>> ScanAllJobsAsync(int limit = 200)
        {
            try
            {
                var scanRequest = new ScanRequest
                {
                    TableName = _tableName,
                    FilterExpression = "begins_with(SK, :sk)",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":sk", new AttributeValue { S = "JOB#" } }
                    }
                };
                var response = await _dynamoDb.ScanAsync(scanRequest);
                return response.Items.Select(MapToEntry).OrderByDescending(e => e.CreatedAt).Take(limit).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to scan jobs from DynamoDB table '{_tableName}'.");
                return new List<ScrapeLogEntry>();
            }
        }

        public async Task<List<ScrapeRunSummary>> GetRunsByUserAsync(Guid userId, int limit = 100)
        {
            var jobs = await GetJobsByUserAsync(userId, limit);
            return AggregateRuns(jobs);
        }

        public async Task<List<ScrapeRunSummary>> GetAllRunsAsync(int limit = 200)
        {
            var jobs = await GetAllJobsAsync(limit);
            return AggregateRuns(jobs);
        }

        public async Task<ScrapeRunDetail?> GetRunDetailAsync(string runId, Guid? userId = null)
        {
            if (string.IsNullOrEmpty(runId)) return null;

            List<ScrapeLogEntry> candidateJobs = new List<ScrapeLogEntry>();
            if (userId.HasValue)
            {
                candidateJobs = await GetJobsByUserAsync(userId.Value, 500);
            }

            var runJobs = candidateJobs.Where(j => j.RunId == runId || j.JobId == runId).OrderByDescending(j => j.CreatedAt).ToList();

            if (!runJobs.Any())
            {
                var allJobs = await GetAllJobsAsync(500);
                runJobs = allJobs.Where(j => j.RunId == runId || j.JobId == runId).OrderByDescending(j => j.CreatedAt).ToList();
            }

            if (!runJobs.Any())
            {
                runJobs = await ScanJobsByRunIdAsync(runId);
            }

            if (!runJobs.Any()) return null;

            var first = runJobs.First();
            var finalStatus = ComputeFinalStatus(runJobs);

            return new ScrapeRunDetail
            {
                RunId = runId,
                UserId = first.UserId,
                UserEmail = first.UserEmail,
                FinalStatus = finalStatus,
                CreatedAt = runJobs.Min(j => j.CreatedAt),
                Jobs = runJobs
            };
        }

        private async Task<List<ScrapeLogEntry>> ScanJobsByRunIdAsync(string runId)
        {
            try
            {
                var scanRequest = new ScanRequest
                {
                    TableName = _tableName,
                    FilterExpression = "RunId = :rid OR runId = :rid OR JobId = :rid OR jobId = :rid",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":rid", new AttributeValue { S = runId } }
                    }
                };
                var response = await _dynamoDb.ScanAsync(scanRequest);
                return response.Items.Select(MapToEntry).OrderByDescending(e => e.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to scan jobs by runId {runId} from DynamoDB table '{_tableName}'.");
                return new List<ScrapeLogEntry>();
            }
        }

        private static List<ScrapeLogEntry> DeduplicateJobs(List<ScrapeLogEntry> jobs)
        {
            return jobs
                .GroupBy(j => j.JobId)
                .Select(g => g.OrderByDescending(j => j.RowCount > 0 ? 1 : 0)
                             .ThenByDescending(j => j.CompletedAt ?? j.CreatedAt)
                             .First())
                .ToList();
        }

        private List<ScrapeRunSummary> AggregateRuns(List<ScrapeLogEntry> jobs)
        {
            // Filter out entries without a RunId per user preference
            var validJobs = jobs.Where(j => !string.IsNullOrEmpty(j.RunId)).ToList();

            var grouped = validJobs
                .GroupBy(j => j.RunId)
                .Select(g =>
                {
                    var jobList = g.ToList();
                    var deduplicated = DeduplicateJobs(jobList);
                    var first = deduplicated.First();

                    return new ScrapeRunSummary
                    {
                        RunId = g.Key,
                        UserId = first.UserId,
                        UserEmail = first.UserEmail,
                        FinalStatus = ComputeFinalStatus(deduplicated),
                        CreatedAt = deduplicated.Min(j => j.CreatedAt),
                        TotalJobs = deduplicated.Count,
                        SucceededJobs = deduplicated.Count(j => j.Status == "Succeeded"),
                        FailedJobs = deduplicated.Count(j => j.Status == "Failed"),
                        TotalRowCount = deduplicated.Sum(j => j.RowCount),
                        Stores = deduplicated.Select(j => j.Store).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList(),
                        Matriculas = deduplicated.Select(j => j.Matricula).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList()
                    };
                })
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return grouped;
        }

        private static string ComputeFinalStatus(List<ScrapeLogEntry> jobs)
        {
            if (jobs.Any(j => j.Status == "Failed")) return "Failed";
            if (jobs.Any(j => j.Status == "Running")) return "Running";
            if (jobs.Any(j => j.Status == "Pending")) return "Pending";
            if (jobs.All(j => j.Status == "Succeeded")) return "Succeeded";
            return "Unknown";
        }

        private ScrapeLogEntry MapToEntry(Dictionary<string, AttributeValue> item)
        {
            var jobId = item.GetValueOrDefault("JobId")?.S ?? item.GetValueOrDefault("jobId")?.S ?? "";
            var runId = item.GetValueOrDefault("RunId")?.S ?? item.GetValueOrDefault("runId")?.S ?? "";
            if (string.IsNullOrEmpty(runId)) runId = jobId;

            var rowCountStr = item.GetValueOrDefault("RowCount")?.N
                           ?? item.GetValueOrDefault("RowCount")?.S
                           ?? item.GetValueOrDefault("rowCount")?.N
                           ?? item.GetValueOrDefault("rowCount")?.S;

            return new ScrapeLogEntry
            {
                JobId = jobId,
                RunId = runId,
                UserId = item.GetValueOrDefault("UserId")?.S ?? item.GetValueOrDefault("userId")?.S ?? "",
                UserEmail = item.GetValueOrDefault("UserEmail")?.S ?? item.GetValueOrDefault("userEmail")?.S ?? "",
                Status = item.GetValueOrDefault("Status")?.S ?? item.GetValueOrDefault("status")?.S ?? "",
                Store = item.GetValueOrDefault("Store")?.S ?? item.GetValueOrDefault("store")?.S ?? "",
                Matricula = item.GetValueOrDefault("Matricula")?.S ?? item.GetValueOrDefault("matricula")?.S ?? "",
                RowCount = int.TryParse(rowCountStr, out var rc) ? rc : 0,
                ErrorMessage = item.GetValueOrDefault("error")?.S ?? item.GetValueOrDefault("ErrorMessage")?.S ?? item.GetValueOrDefault("errorMessage")?.S,
                FileRelativePath = item.GetValueOrDefault("fileRelativePath")?.S ?? item.GetValueOrDefault("FileRelativePath")?.S,
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("CreatedAt")?.S ?? item.GetValueOrDefault("createdAt")?.S, out var d) ? d : DateTime.MinValue,
                CompletedAt = DateTime.TryParse(item.GetValueOrDefault("CompletedAt")?.S ?? item.GetValueOrDefault("completedAt")?.S, out var cd) ? cd : (DateTime?)null
            };
        }
    }
}
