using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SalesApp.ReportFilters.Models;
using SalesApp.ReportFilters.Settings;

namespace SalesApp.ReportFilters.Repositories
{
    /// <summary>
    /// DynamoDB implementation of IReportFilterRepository.
    ///
    /// Table design:
    ///   PK  = "REP#"
    ///   SK  = "#U-{userGuid}#REP-{filterId}"
    ///
    /// GSI: scope-createdAt-index
    ///   GSI PK = scope ("private" | "shared")
    ///   GSI SK = createdAt (ISO 8601)
    ///
    /// The table name is never hardcoded — it is read from IOptions&lt;DynamoDbSettings&gt;.
    /// </summary>
    public class DynamoDbReportFilterRepository : IReportFilterRepository
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbReportFilterRepository> _logger;

        private const string PkValue = "REP#";
        private const string GsiName = "scope-createdAt-index";

        public DynamoDbReportFilterRepository(
            IAmazonDynamoDB dynamoDb,
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbReportFilterRepository> logger)
        {
            _dynamoDb = dynamoDb;
            _tableName = settings.Value.ReportFiltersTable;
            _logger = logger;
        }

        // ── Read operations ───────────────────────────────────────────────────

        public async Task<List<ReportFilter>> ListForUserAsync(string userId)
        {
            // 1. Fetch caller's private reports via PK + SK prefix
            var privateReports = await QueryBySkPrefixAsync($"#U-{userId}#REP-");

            // 2. Fetch all shared reports via GSI (scope-createdAt-index)
            var sharedReports = await QuerySharedViaGsiAsync();

            // Merge — deduplicate by filterId in case the caller owns shared reports
            var seen = new HashSet<string>();
            var result = new List<ReportFilter>();

            foreach (var r in privateReports.Concat(sharedReports))
            {
                if (seen.Add(r.FilterId))
                    result.Add(r);
            }

            return result.OrderByDescending(r => r.CreatedAt).ToList();
        }

        public async Task<ReportFilter?> GetByIdAsync(string userId, string filterId)
        {
            // First try exact owner key
            var sk = BuildSk(userId, filterId);
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = PkValue } },
                    { "SK", new AttributeValue { S = sk } }
                }
            };

            try
            {
                var response = await _dynamoDb.GetItemAsync(request);
                if (response.Item.Count > 0)
                    return MapToModel(response.Item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GetItem failed for filterId={FilterId}", filterId);
                throw;
            }

            return null;
        }

        // ── Write operations ──────────────────────────────────────────────────

        public async Task CreateAsync(ReportFilter filter)
        {
            var item = ToAttributeMap(filter);
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = item,
                // Guard against accidental overwrite
                ConditionExpression = "attribute_not_exists(PK)"
            };

            try
            {
                await _dynamoDb.PutItemAsync(request);
            }
            catch (ConditionalCheckFailedException)
            {
                throw new InvalidOperationException($"Report filter '{filter.FilterId}' already exists.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB PutItem failed for filterId={FilterId}", filter.FilterId);
                throw;
            }
        }

        public async Task UpdateAsync(ReportFilter filter)
        {
            var item = ToAttributeMap(filter);
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            };

            try
            {
                await _dynamoDb.PutItemAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB PutItem (update) failed for filterId={FilterId}", filter.FilterId);
                throw;
            }
        }

        public async Task DeleteAsync(string userId, string filterId)
        {
            var sk = BuildSk(userId, filterId);
            var request = new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = PkValue } },
                    { "SK", new AttributeValue { S = sk } }
                }
            };

            try
            {
                await _dynamoDb.DeleteItemAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB DeleteItem failed for filterId={FilterId}", filterId);
                throw;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static string BuildSk(string userId, string filterId) =>
            $"#U-{userId}#REP-{filterId}";

        private async Task<List<ReportFilter>> QueryBySkPrefixAsync(string skPrefix)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = PkValue } },
                    { ":sk", new AttributeValue { S = skPrefix } }
                },
                ScanIndexForward = false
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(MapToModel).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB Query by SK prefix failed for prefix={SkPrefix}", skPrefix);
                throw;
            }
        }

        private async Task<List<ReportFilter>> QuerySharedViaGsiAsync()
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = GsiName,
                KeyConditionExpression = "#scope = :shared",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    // "scope" is a reserved word in DynamoDB expression syntax
                    { "#scope", "scope" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":shared", new AttributeValue { S = "shared" } }
                },
                ScanIndexForward = false
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(MapToModel).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GSI query for shared reports failed");
                throw;
            }
        }

        // ── Serialization ─────────────────────────────────────────────────────

        private static Dictionary<string, AttributeValue> ToAttributeMap(ReportFilter f)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK",          new AttributeValue { S = PkValue } },
                { "SK",          new AttributeValue { S = BuildSk(f.UserId, f.FilterId) } },
                { "userId",      new AttributeValue { S = f.UserId } },
                { "filterId",    new AttributeValue { S = f.FilterId } },
                { "name",        new AttributeValue { S = f.Name } },
                { "scope",       new AttributeValue { S = f.Scope } },
                { "filterConfig",  new AttributeValue { S = JsonConvert.SerializeObject(f.FilterConfig) } },
                { "outputColumns", new AttributeValue { S = JsonConvert.SerializeObject(f.OutputColumns) } },
                { "groupByEmail",  new AttributeValue { BOOL = f.GroupByEmail } },
                { "groupByTeam",   new AttributeValue { BOOL = f.GroupByTeam } },
                { "orderByField",  new AttributeValue { S = f.OrderByField ?? "" } },
                { "orderByDirection", new AttributeValue { S = f.OrderByDirection ?? "" } },
                { "createdAt",     new AttributeValue { S = f.CreatedAt.ToString("O") } },
                { "updatedAt",     new AttributeValue { S = f.UpdatedAt.ToString("O") } }
            };

            if (!string.IsNullOrEmpty(f.Description))
                item["description"] = new AttributeValue { S = f.Description };

            return item;
        }

        private static ReportFilter MapToModel(Dictionary<string, AttributeValue> item)
        {
            var filterConfigJson = item.GetValueOrDefault("filterConfig")?.S ?? "{}";
            var outputColumnsJson = item.GetValueOrDefault("outputColumns")?.S ?? "[]";

            return new ReportFilter
            {
                PK        = item.GetValueOrDefault("PK")?.S ?? PkValue,
                SK        = item.GetValueOrDefault("SK")?.S ?? string.Empty,
                UserId    = item.GetValueOrDefault("userId")?.S ?? string.Empty,
                FilterId  = item.GetValueOrDefault("filterId")?.S ?? string.Empty,
                Name      = item.GetValueOrDefault("name")?.S ?? string.Empty,
                Description = item.GetValueOrDefault("description")?.S,
                Scope     = item.GetValueOrDefault("scope")?.S ?? string.Empty,
                FilterConfig  = JsonConvert.DeserializeObject<FilterConfig>(filterConfigJson) ?? new FilterConfig(),
                OutputColumns = JsonConvert.DeserializeObject<List<OutputColumn>>(outputColumnsJson) ?? new List<OutputColumn>(),
                GroupByEmail  = item.GetValueOrDefault("groupByEmail")?.BOOL ?? false,
                GroupByTeam   = item.GetValueOrDefault("groupByTeam")?.BOOL ?? false,
                OrderByField  = item.GetValueOrDefault("orderByField")?.S,
                OrderByDirection = item.GetValueOrDefault("orderByDirection")?.S,
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var ca) ? ca : DateTime.UtcNow,
                UpdatedAt = DateTime.TryParse(item.GetValueOrDefault("updatedAt")?.S, out var ua) ? ua : DateTime.UtcNow
            };
        }
    }
}
