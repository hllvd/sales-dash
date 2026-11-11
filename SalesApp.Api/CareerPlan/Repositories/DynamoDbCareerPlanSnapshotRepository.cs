using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public class DynamoDbCareerPlanSnapshotRepository : ICareerPlanSnapshotRepository
    {
        private readonly IAmazonDynamoDB? _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbCareerPlanSnapshotRepository> _logger;
        private readonly bool _useMemoryOnly;

        private const string PkLatest = "CAREER_SNAPSHOT#LATEST";

        // In-memory store/cache for fast reads and E2E/local isolation when DynamoDB is bypassed or unavailable
        private static readonly ConcurrentDictionary<string, CareerPlanConsultantSnapshotDto> _inMemoryFallback = new();
        private static DateTime _lastSyncedAtUtc = DateTime.MinValue;
        private static readonly TimeSpan MemoryCacheTtl = TimeSpan.FromMinutes(5);

        public DynamoDbCareerPlanSnapshotRepository(
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbCareerPlanSnapshotRepository> logger,
            IAmazonDynamoDB? dynamoDb = null,
            IHostEnvironment? hostEnvironment = null)
        {
            _dynamoDb = dynamoDb;
            _tableName = settings.Value.DataCacheTable ?? settings.Value.CareerPlanSnapshotsTable ?? "salesapp-data-cache";
            _logger = logger;
            var envName = hostEnvironment?.EnvironmentName ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            _useMemoryOnly = string.Equals(envName, "E2E", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IReadOnlyList<CareerPlanConsultantSnapshotDto>> GetLatestSnapshotsAsync()
        {
            if (_useMemoryOnly)
            {
                return _inMemoryFallback.Values.OrderBy(c => c.UserName).ToList();
            }

            if (!_inMemoryFallback.IsEmpty && (DateTime.UtcNow - _lastSyncedAtUtc) < MemoryCacheTtl)
            {
                return _inMemoryFallback.Values.OrderBy(c => c.UserName).ToList();
            }

            if (_dynamoDb != null && !string.IsNullOrWhiteSpace(_tableName))
            {
                try
                {
                    var list = new List<CareerPlanConsultantSnapshotDto>();
                    Dictionary<string, AttributeValue>? exclusiveStartKey = null;

                    do
                    {
                        var request = new QueryRequest
                        {
                            TableName = _tableName,
                            KeyConditionExpression = "PK = :pk",
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":pk"] = new AttributeValue { S = PkLatest }
                            },
                            ExclusiveStartKey = exclusiveStartKey
                        };

                        var response = await _dynamoDb.QueryAsync(request);
                        if (response.Items != null)
                        {
                            foreach (var item in response.Items)
                            {
                                list.Add(MapFromDynamo(item));
                            }
                        }

                        exclusiveStartKey = response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0
                            ? response.LastEvaluatedKey
                            : null;
                    }
                    while (exclusiveStartKey != null);

                    if (list.Count > 0)
                    {
                        _inMemoryFallback.Clear();
                        foreach (var s in list)
                        {
                            _inMemoryFallback[s.UserId.ToString()] = s;
                        }
                        _lastSyncedAtUtc = DateTime.UtcNow;
                        return list.OrderBy(c => c.UserName).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query career plan snapshots from DynamoDB table {Table}. Falling back to memory store.", _tableName);
                }
            }

            return _inMemoryFallback.Values.OrderBy(c => c.UserName).ToList();
        }

        public async Task SaveSnapshotsAsync(IReadOnlyList<CareerPlanConsultantSnapshotDto> snapshots)
        {
            if (snapshots == null) return;

            // Always update in-memory store & cache timestamp
            _inMemoryFallback.Clear();
            foreach (var s in snapshots)
            {
                _inMemoryFallback[s.UserId.ToString()] = s;
            }
            _lastSyncedAtUtc = DateTime.UtcNow;

            if (_useMemoryOnly || _dynamoDb == null || string.IsNullOrWhiteSpace(_tableName))
            {
                return;
            }

            try
            {
                // TTL = 60 days from now (2 months) in Unix Epoch Seconds
                var ttlSeconds = DateTimeOffset.UtcNow.AddDays(60).ToUnixTimeSeconds();
                var currentSks = snapshots.Select(s => $"USER#{s.UserId}").ToHashSet(StringComparer.Ordinal);

                // BatchWriteItem supports up to 25 items per request
                var writeRequests = snapshots.Select(s => new WriteRequest
                {
                    PutRequest = new PutRequest
                    {
                        Item = MapToDynamo(s, ttlSeconds)
                    }
                }).ToList();

                // Prune any orphaned SKs previously stored under PkLatest so stale users don't accumulate
                var existingSks = await QueryExistingSortKeysAsync();
                foreach (var staleSk in existingSks)
                {
                    if (!currentSks.Contains(staleSk))
                    {
                        writeRequests.Add(new WriteRequest
                        {
                            DeleteRequest = new DeleteRequest
                            {
                                Key = new Dictionary<string, AttributeValue>
                                {
                                    ["PK"] = new AttributeValue { S = PkLatest },
                                    ["SK"] = new AttributeValue { S = staleSk }
                                }
                            }
                        });
                    }
                }

                await ExecuteBatchWritesAsync(writeRequests);

                _logger.LogInformation("Saved {Count} career plan snapshots to DynamoDB table {Table}", snapshots.Count, _tableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save career plan snapshots to DynamoDB table {Table}. Snapshots remain in memory.", _tableName);
            }
        }

        private async Task<List<string>> QueryExistingSortKeysAsync()
        {
            var keys = new List<string>();
            if (_dynamoDb == null) return keys;

            Dictionary<string, AttributeValue>? exclusiveStartKey = null;
            do
            {
                var resp = await _dynamoDb.QueryAsync(new QueryRequest
                {
                    TableName = _tableName,
                    KeyConditionExpression = "PK = :pk",
                    ProjectionExpression = "SK",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":pk"] = new AttributeValue { S = PkLatest }
                    },
                    ExclusiveStartKey = exclusiveStartKey
                });

                if (resp.Items != null)
                {
                    foreach (var item in resp.Items)
                    {
                        if (item.TryGetValue("SK", out var skVal) && !string.IsNullOrEmpty(skVal.S))
                        {
                            keys.Add(skVal.S);
                        }
                    }
                }

                exclusiveStartKey = resp.LastEvaluatedKey != null && resp.LastEvaluatedKey.Count > 0
                    ? resp.LastEvaluatedKey
                    : null;
            }
            while (exclusiveStartKey != null);

            return keys;
        }

        private async Task ExecuteBatchWritesAsync(List<WriteRequest> writeRequests)
        {
            if (_dynamoDb == null) return;

            for (int i = 0; i < writeRequests.Count; i += 25)
            {
                var batch = writeRequests.Skip(i).Take(25).ToList();
                var batchRequest = new BatchWriteItemRequest
                {
                    RequestItems = new Dictionary<string, List<WriteRequest>>
                    {
                        [_tableName] = batch
                    }
                };

                var batchResponse = await _dynamoDb.BatchWriteItemAsync(batchRequest);

                int retryCount = 0;
                while (batchResponse.UnprocessedItems != null &&
                       batchResponse.UnprocessedItems.TryGetValue(_tableName, out var unprocessed) &&
                       unprocessed.Count > 0 &&
                       retryCount < 3)
                {
                    retryCount++;
                    await Task.Delay(200 * retryCount);
                    batchResponse = await _dynamoDb.BatchWriteItemAsync(new BatchWriteItemRequest
                    {
                        RequestItems = new Dictionary<string, List<WriteRequest>>
                        {
                            [_tableName] = unprocessed
                        }
                    });
                }
            }
        }

        private static Dictionary<string, AttributeValue> MapToDynamo(CareerPlanConsultantSnapshotDto dto, long ttlSeconds)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = PkLatest },
                ["SK"] = new AttributeValue { S = $"USER#{dto.UserId}" },
                ["UserId"] = new AttributeValue { S = dto.UserId.ToString() },
                ["UserName"] = new AttributeValue { S = dto.UserName ?? string.Empty },
                ["UserEmail"] = new AttributeValue { S = dto.UserEmail ?? string.Empty },
                ["CurrentLevelName"] = new AttributeValue { S = dto.CurrentLevelName ?? string.Empty },
                ["TargetLevelName"] = new AttributeValue { S = dto.TargetLevelName ?? string.Empty },
                ["ActiveProduction12m"] = new AttributeValue { N = dto.ActiveProduction12m.ToString(CultureInfo.InvariantCulture) },
                ["ProductionProgressPct"] = new AttributeValue { N = dto.ProductionProgressPct.ToString(CultureInfo.InvariantCulture) },
                ["StrictRetention15m"] = new AttributeValue { N = dto.StrictRetention15m.ToString(CultureInfo.InvariantCulture) },
                ["CalculatedAt"] = new AttributeValue { S = dto.CalculatedAt.ToString("o") },
                ["TTL"] = new AttributeValue { N = ttlSeconds.ToString() }
            };

            if (dto.SalesGoal.HasValue)
            {
                item["SalesGoal"] = new AttributeValue { N = dto.SalesGoal.Value.ToString(CultureInfo.InvariantCulture) };
            }

            if (dto.RetentionTarget.HasValue)
            {
                item["RetentionTarget"] = new AttributeValue { N = dto.RetentionTarget.Value.ToString() };
            }

            return item;
        }

        private static CareerPlanConsultantSnapshotDto MapFromDynamo(Dictionary<string, AttributeValue> item)
        {
            Guid.TryParse(item.TryGetValue("UserId", out var uid) ? uid.S : null, out var parsedUserId);
            decimal.TryParse(item.TryGetValue("ActiveProduction12m", out var ap) ? ap.N : null, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAp);
            decimal.TryParse(item.TryGetValue("ProductionProgressPct", out var pp) ? pp.N : null, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPp);
            decimal.TryParse(item.TryGetValue("StrictRetention15m", out var sr) ? sr.N : null, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSr);

            decimal? salesGoal = null;
            if (item.TryGetValue("SalesGoal", out var sg) && decimal.TryParse(sg.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSg))
            {
                salesGoal = parsedSg;
            }

            int? retentionTarget = null;
            if (item.TryGetValue("RetentionTarget", out var rt) && int.TryParse(rt.N, out var parsedRt))
            {
                retentionTarget = parsedRt;
            }

            DateTime calculatedAt = DateTime.UtcNow;
            if (item.TryGetValue("CalculatedAt", out var ca) && DateTime.TryParse(ca.S, null, DateTimeStyles.RoundtripKind, out var parsedCa))
            {
                calculatedAt = parsedCa;
            }

            return new CareerPlanConsultantSnapshotDto
            {
                UserId = parsedUserId,
                UserName = item.TryGetValue("UserName", out var un) ? un.S : string.Empty,
                UserEmail = item.TryGetValue("UserEmail", out var ue) ? ue.S : string.Empty,
                CurrentLevelName = item.TryGetValue("CurrentLevelName", out var cl) ? cl.S : string.Empty,
                TargetLevelName = item.TryGetValue("TargetLevelName", out var tl) ? tl.S : string.Empty,
                SalesGoal = salesGoal,
                RetentionTarget = retentionTarget,
                ActiveProduction12m = parsedAp,
                ProductionProgressPct = parsedPp,
                StrictRetention15m = parsedSr,
                CalculatedAt = calculatedAt
            };
        }
    }
}
