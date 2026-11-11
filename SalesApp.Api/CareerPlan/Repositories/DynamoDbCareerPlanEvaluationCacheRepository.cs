using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalesApp.DTOs;
using SalesApp.ReportFilters.Settings;

namespace SalesApp.CareerPlan.Repositories
{
    public class DynamoDbCareerPlanEvaluationCacheRepository : ICareerPlanEvaluationCacheRepository
    {
        private readonly IAmazonDynamoDB? _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbCareerPlanEvaluationCacheRepository> _logger;
        private readonly bool _useMemoryOnly;

        private const string SkLatest = "LATEST";
        private const int DefaultTtlDays = 60;

        // In-memory fallback for local environments and integration tests
        private static readonly ConcurrentDictionary<Guid, CachedCareerPlanEvaluationDto> _inMemoryFallback = new();

        public DynamoDbCareerPlanEvaluationCacheRepository(
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbCareerPlanEvaluationCacheRepository> logger,
            IAmazonDynamoDB? dynamoDb = null,
            IHostEnvironment? hostEnvironment = null)
        {
            _dynamoDb = dynamoDb;
            _tableName = settings.Value.DataCacheTable ?? settings.Value.CareerPlanSnapshotsTable ?? "salesapp-data-cache";
            _logger = logger;
            var envName = hostEnvironment?.EnvironmentName ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            _useMemoryOnly = string.Equals(envName, "E2E", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildPk(Guid userId) => $"CAREER_EVALUATION#USER#{userId}";

        public static long ComputeTtlEpoch(DateTime nowUtc, int days = DefaultTtlDays)
        {
            return new DateTimeOffset(nowUtc.AddDays(days)).ToUnixTimeSeconds();
        }

        public async Task<CachedCareerPlanEvaluationDto?> GetAsync(Guid userId)
        {
            if (!_useMemoryOnly && _dynamoDb != null && !string.IsNullOrWhiteSpace(_tableName))
            {
                try
                {
                    var request = new GetItemRequest
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue { S = BuildPk(userId) },
                            ["SK"] = new AttributeValue { S = SkLatest }
                        }
                    };

                    var response = await _dynamoDb.GetItemAsync(request);
                    if (response.IsItemSet && response.Item != null && response.Item.Count > 0)
                    {
                        var item = response.Item;
                        if (item.TryGetValue("Payload", out var payloadVal) && !string.IsNullOrWhiteSpace(payloadVal.S))
                        {
                            var eval = JsonSerializer.Deserialize<CareerPlanEvaluationResponse>(payloadVal.S, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (eval != null)
                            {
                                var calculatedAt = DateTime.UtcNow;
                                if (item.TryGetValue("CalculatedAt", out var calcVal) && DateTime.TryParse(calcVal.S, out var parsedDate))
                                {
                                    calculatedAt = parsedDate;
                                }

                                long ttl = 0;
                                if (item.TryGetValue("TTL", out var ttlVal) && long.TryParse(ttlVal.N, out var parsedTtl))
                                {
                                    ttl = parsedTtl;
                                }

                                return new CachedCareerPlanEvaluationDto
                                {
                                    UserId = userId,
                                    Evaluation = eval,
                                    CalculatedAt = calculatedAt,
                                    Ttl = ttl
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DynamoDB unavailable for CareerPlanEvaluation cache. Falling back to in-memory.");
                }
            }

            if (_inMemoryFallback.TryGetValue(userId, out var cached))
            {
                return cached;
            }

            return null;
        }

        public async Task SaveAsync(CachedCareerPlanEvaluationDto entry)
        {
            // Always update in-memory fallback
            _inMemoryFallback[entry.UserId] = entry;

            if (_useMemoryOnly || _dynamoDb == null || string.IsNullOrWhiteSpace(_tableName))
            {
                return;
            }

            try
            {
                var payloadJson = JsonSerializer.Serialize(entry.Evaluation);
                var ttl = entry.Ttl > 0 ? entry.Ttl : ComputeTtlEpoch(DateTime.UtcNow);

                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = BuildPk(entry.UserId) },
                    ["SK"] = new AttributeValue { S = SkLatest },
                    ["UserId"] = new AttributeValue { S = entry.UserId.ToString() },
                    ["Payload"] = new AttributeValue { S = payloadJson },
                    ["CalculatedAt"] = new AttributeValue { S = entry.CalculatedAt.ToString("o") },
                    ["TTL"] = new AttributeValue { N = ttl.ToString() }
                };

                await _dynamoDb.PutItemAsync(new PutItemRequest
                {
                    TableName = _tableName,
                    Item = item
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist CareerPlanEvaluation to DynamoDB {Table}. Saved in-memory fallback.", _tableName);
            }
        }
    }
}
