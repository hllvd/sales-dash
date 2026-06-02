using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SalesApp.ReportFilters.Settings;
using SalesApp.ReportViews.Models;

namespace SalesApp.ReportViews.Repositories
{
    public class DynamoDbReportViewRepository : IReportViewRepository
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbReportViewRepository> _logger;

        private const string PkValue = "VIEW#";
        private const string GsiName = "scope-createdAt-index";

        public DynamoDbReportViewRepository(
            IAmazonDynamoDB dynamoDb,
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbReportViewRepository> logger)
        {
            _dynamoDb = dynamoDb;
            _tableName = settings.Value.ReportFiltersTable; // Reuse the same report filters table
            _logger = logger;
        }

        public async Task<List<ReportView>> ListForUserAsync(string userId)
        {
            // 1. Fetch caller's private views
            var privateViews = await QueryBySkPrefixAsync($"#U-{userId}#VIEW-");

            // 2. Fetch all shared views via GSI
            var sharedViews = await QuerySharedViaGsiAsync();

            // Merge and deduplicate by ViewId
            var seen = new HashSet<string>();
            var result = new List<ReportView>();

            foreach (var v in privateViews.Concat(sharedViews))
            {
                if (seen.Add(v.ViewId))
                    result.Add(v);
            }

            return result.OrderByDescending(v => v.CreatedAt).ToList();
        }

        public async Task<ReportView?> GetByIdAsync(string userId, string viewId)
        {
            var sk = BuildSk(userId, viewId);
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
                _logger.LogError(ex, "DynamoDB GetItem failed for viewId={ViewId}", viewId);
                throw;
            }

            return null;
        }

        public async Task CreateAsync(ReportView view)
        {
            var item = ToAttributeMap(view);
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
                _logger.LogError(ex, "DynamoDB PutItem failed for viewId={ViewId}", view.ViewId);
                throw;
            }
        }

        public async Task UpdateAsync(ReportView view)
        {
            // DynamoDB PutItem replaces the item completely, same as Create
            await CreateAsync(view);
        }

        public async Task DeleteAsync(string userId, string viewId)
        {
            var sk = BuildSk(userId, viewId);
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
                _logger.LogError(ex, "DynamoDB DeleteItem failed for viewId={ViewId}", viewId);
                throw;
            }
        }

        // ── DynamoDB Query Helpers ───────────────────────────────────────────

        private async Task<List<ReportView>> QueryBySkPrefixAsync(string skPrefix)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = PkValue } },
                    { ":skPrefix", new AttributeValue { S = skPrefix } }
                }
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

        private async Task<List<ReportView>> QuerySharedViaGsiAsync()
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = GsiName,
                KeyConditionExpression = "#scope = :shared",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
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
                // GSI results only contain keys and index attributes if they are filtered by projected types,
                // but since our ProjectionType is ALL, it contains all fields. We just filter out non-view items
                // by checking PK.
                return response.Items
                    .Where(item => item.GetValueOrDefault("PK")?.S == PkValue)
                    .Select(MapToModel)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GSI query for shared views failed");
                throw;
            }
        }

        // ── Serialization & Mapping Helpers ──────────────────────────────────

        private static string BuildSk(string userId, string viewId) => $"#U-{userId}#VIEW-{viewId}";

        private static Dictionary<string, AttributeValue> ToAttributeMap(ReportView v)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK",          new AttributeValue { S = PkValue } },
                { "SK",          new AttributeValue { S = BuildSk(v.UserId, v.ViewId) } },
                { "userId",      new AttributeValue { S = v.UserId } },
                { "viewId",      new AttributeValue { S = v.ViewId } },
                { "name",        new AttributeValue { S = v.Name } },
                { "scope",       new AttributeValue { S = v.Scope } },
                { "rows",        new AttributeValue { S = JsonConvert.SerializeObject(v.Rows) } },
                { "createdAt",     new AttributeValue { S = v.CreatedAt.ToString("O") } },
                { "updatedAt",     new AttributeValue { S = v.UpdatedAt.ToString("O") } }
            };

            if (v.AllowedTeamIds != null)
                item["allowedTeamIds"] = new AttributeValue { S = JsonConvert.SerializeObject(v.AllowedTeamIds) };
            if (v.AllowedRoles != null)
                item["allowedRoles"] = new AttributeValue { S = JsonConvert.SerializeObject(v.AllowedRoles) };

            if (!string.IsNullOrEmpty(v.Description))
                item["description"] = new AttributeValue { S = v.Description };

            return item;
        }

        private static ReportView MapToModel(Dictionary<string, AttributeValue> item)
        {
            var rowsJson = item.GetValueOrDefault("rows")?.S ?? "[]";

            return new ReportView
            {
                PK        = item.GetValueOrDefault("PK")?.S ?? PkValue,
                SK        = item.GetValueOrDefault("SK")?.S ?? string.Empty,
                UserId    = item.GetValueOrDefault("userId")?.S ?? string.Empty,
                ViewId    = item.GetValueOrDefault("viewId")?.S ?? string.Empty,
                Name      = item.GetValueOrDefault("name")?.S ?? string.Empty,
                Description = item.GetValueOrDefault("description")?.S,
                Scope     = item.GetValueOrDefault("scope")?.S ?? string.Empty,
                Rows      = JsonConvert.DeserializeObject<List<ViewRow>>(rowsJson) ?? new List<ViewRow>(),
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var ca) ? ca : DateTime.UtcNow,
                UpdatedAt = DateTime.TryParse(item.GetValueOrDefault("updatedAt")?.S, out var ua) ? ua : DateTime.UtcNow,
                AllowedTeamIds = item.ContainsKey("allowedTeamIds") 
                    ? JsonConvert.DeserializeObject<List<int>>(item["allowedTeamIds"].S) 
                    : new List<int>(),
                AllowedRoles = item.ContainsKey("allowedRoles") 
                    ? JsonConvert.DeserializeObject<List<string>>(item["allowedRoles"].S) 
                    : new List<string>()
            };
        }
    }
}
