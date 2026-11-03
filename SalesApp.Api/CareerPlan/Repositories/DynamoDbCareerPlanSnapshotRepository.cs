using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
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

        private const string PkLatest = "CAREER_SNAPSHOT#LATEST";

        // In-memory fallback for local environments/tests when DynamoDB is unreachable or table not configured
        private static readonly ConcurrentDictionary<string, CareerPlanConsultantSnapshotDto> _inMemoryFallback = new();

        public DynamoDbCareerPlanSnapshotRepository(
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbCareerPlanSnapshotRepository> logger,
            IAmazonDynamoDB? dynamoDb = null)
        {
            _dynamoDb = dynamoDb;
            _tableName = settings.Value.CareerPlanSnapshotsTable ?? "salesapp-career-plan-snapshots";
            _logger = logger;
        }

        public async Task<IReadOnlyList<CareerPlanConsultantSnapshotDto>> GetLatestSnapshotsAsync()
        {
            if (_dynamoDb != null && !string.IsNullOrWhiteSpace(_tableName))
            {
                try
                {
                    var request = new QueryRequest
                    {
                        TableName = _tableName,
                        KeyConditionExpression = "PK = :pk",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":pk"] = new AttributeValue { S = PkLatest }
                        }
                    };

                    var response = await _dynamoDb.QueryAsync(request);
                    if (response.Items != null && response.Items.Count > 0)
                    {
                        var list = new List<CareerPlanConsultantSnapshotDto>();
                        foreach (var item in response.Items)
                        {
                            list.Add(MapFromDynamo(item));
                        }
                        return list;
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

            // Always update in-memory fallback
            _inMemoryFallback.Clear();
            foreach (var s in snapshots)
            {
                _inMemoryFallback[s.UserId.ToString()] = s;
            }

            if (_dynamoDb == null || string.IsNullOrWhiteSpace(_tableName))
            {
                return;
            }

            try
            {
                // TTL = 60 days from now (2 months) in Unix Epoch Seconds
                var ttlSeconds = DateTimeOffset.UtcNow.AddDays(60).ToUnixTimeSeconds();

                // BatchWriteItem supports up to 25 items per request
                var writeRequests = snapshots.Select(s => new WriteRequest
                {
                    PutRequest = new PutRequest
                    {
                        Item = MapToDynamo(s, ttlSeconds)
                    }
                }).ToList();

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

                    // Retry unprocessed items if any
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

                _logger.LogInformation("Saved {Count} career plan snapshots to DynamoDB table {Table}", snapshots.Count, _tableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save career plan snapshots to DynamoDB table {Table}. Snapshots remain in memory.", _tableName);
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
