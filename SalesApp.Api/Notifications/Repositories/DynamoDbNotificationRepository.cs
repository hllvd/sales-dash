using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SalesApp.Notifications.Models;
using SalesApp.ReportFilters.Settings;
using System.Text;

namespace SalesApp.Notifications.Repositories
{
    public class DynamoDbNotificationRepository : INotificationRepository
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbNotificationRepository> _logger;

        private const string Gsi1UnreadName = "GSI1-unread";
        private const string Gsi2RequesterName = "GSI2-requester";

        public DynamoDbNotificationRepository(
            IAmazonDynamoDB dynamoDb,
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbNotificationRepository> logger)
        {
            _dynamoDb = dynamoDb;
            _tableName = !string.IsNullOrWhiteSpace(settings.Value.NotificationsTable)
                ? settings.Value.NotificationsTable
                : "salesapp-notifications";
            _logger = logger;
        }

        private static string BuildUserPk(string userId) => $"USER#{userId}";

        #region Notifications

        public async Task<(List<NotificationItem> items, string? nextCursor)> GetRecentAsync(string userId, int limit = 20, string? cursor = null)
        {
            var pk = BuildUserPk(userId);
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = pk } },
                    { ":skPrefix", new AttributeValue { S = "NOTIF#" } }
                },
                ScanIndexForward = false, // Newest first (ULID is sortable)
                Limit = limit
            };

            if (!string.IsNullOrEmpty(cursor))
            {
                try
                {
                    var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                    var startKey = JsonConvert.DeserializeObject<Dictionary<string, string>>(decodedJson);
                    if (startKey != null)
                    {
                        request.ExclusiveStartKey = startKey.ToDictionary(k => k.Key, v => new AttributeValue { S = v.Value });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode cursor: {Cursor}", cursor);
                }
            }

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                var items = response.Items.Select(MapToNotificationItem).ToList();

                string? nextCursor = null;
                if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
                {
                    var dict = response.LastEvaluatedKey.ToDictionary(k => k.Key, v => v.Value.S ?? v.Value.N ?? "");
                    var json = JsonConvert.SerializeObject(dict);
                    nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                }

                return (items, nextCursor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB Query recent notifications failed for userId={UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            // Sparse GSI query: only unread items have GSI1PK = UNREAD#<userId>
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = Gsi1UnreadName,
                KeyConditionExpression = "GSI1PK = :gsi1pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":gsi1pk", new AttributeValue { S = $"UNREAD#{userId}" } }
                },
                Select = Select.COUNT
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GetUnreadCount failed for userId={UserId}", userId);
                return 0;
            }
        }

        public async Task<NotificationItem?> GetNotificationAsync(string userId, string notifSk)
        {
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = BuildUserPk(userId) } },
                    { "SK", new AttributeValue { S = notifSk } }
                }
            };

            try
            {
                var response = await _dynamoDb.GetItemAsync(request);
                if (response.Item != null && response.Item.Count > 0)
                {
                    return MapToNotificationItem(response.Item);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GetNotificationAsync failed for userId={UserId}, sk={SK}", userId, notifSk);
                throw;
            }
        }

        public async Task CreateNotificationAsync(NotificationItem item)
        {
            var itemMap = ToNotificationAttributeMap(item);
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = itemMap,
                ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
            };

            try
            {
                await _dynamoDb.PutItemAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB PutItem notification failed for userId={UserId}", item.UserId);
                throw;
            }
        }

        public async Task MarkNotificationReadAsync(string userId, string notifSk)
        {
            // Atomically set unread = false and REMOVE GSI1PK, GSI1SK (sparse index eviction)
            var request = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = BuildUserPk(userId) } },
                    { "SK", new AttributeValue { S = notifSk } }
                },
                UpdateExpression = "SET unread = :falseVal REMOVE GSI1PK, GSI1SK",
                ConditionExpression = "attribute_exists(PK) AND attribute_exists(SK)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":falseVal", new AttributeValue { BOOL = false } }
                }
            };

            try
            {
                await _dynamoDb.UpdateItemAsync(request);
            }
            catch (ConditionalCheckFailedException)
            {
                _logger.LogWarning("MarkNotificationReadAsync condition failed. Item not found: {UserId}, {SK}", userId, notifSk);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB MarkNotificationReadAsync failed for userId={UserId}, sk={SK}", userId, notifSk);
                throw;
            }
        }

        public async Task MarkAllReadAsync(string userId)
        {
            // Query all unread via sparse GSI
            var queryRequest = new QueryRequest
            {
                TableName = _tableName,
                IndexName = Gsi1UnreadName,
                KeyConditionExpression = "GSI1PK = :gsi1pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":gsi1pk", new AttributeValue { S = $"UNREAD#{userId}" } }
                },
                Limit = 100
            };

            try
            {
                var queryResponse = await _dynamoDb.QueryAsync(queryRequest);
                if (queryResponse.Items.Count == 0) return;

                foreach (var item in queryResponse.Items)
                {
                    var notifSk = item.GetValueOrDefault("SK")?.S;
                    if (!string.IsNullOrEmpty(notifSk))
                    {
                        await MarkNotificationReadAsync(userId, notifSk);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB MarkAllReadAsync failed for userId={UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Requests

        public async Task CreateRequestWithNotificationAsync(DomainRequest request, NotificationItem notification)
        {
            // Atomically write both the domain request and linked notification item in a single TransactWriteItems
            var requestItemMap = ToDomainRequestAttributeMap(request);
            var notifItemMap = ToNotificationAttributeMap(notification);

            var transactRequest = new TransactWriteItemsRequest
            {
                TransactItems = new List<TransactWriteItem>
                {
                    new()
                    {
                        Put = new Put
                        {
                            TableName = _tableName,
                            Item = requestItemMap,
                            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
                        }
                    },
                    new()
                    {
                        Put = new Put
                        {
                            TableName = _tableName,
                            Item = notifItemMap,
                            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
                        }
                    }
                }
            };

            try
            {
                await _dynamoDb.TransactWriteItemsAsync(transactRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB TransactWriteItems for Request & Notification failed");
                throw;
            }
        }

        public async Task<DomainRequest?> GetRequestAsync(string userId, string requestSk)
        {
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = BuildUserPk(userId) } },
                    { "SK", new AttributeValue { S = requestSk } }
                }
            };

            try
            {
                var response = await _dynamoDb.GetItemAsync(request);
                if (response.Item != null && response.Item.Count > 0)
                {
                    return MapToDomainRequest(response.Item);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GetRequestAsync failed for userId={UserId}, sk={SK}", userId, requestSk);
                throw;
            }
        }

        public async Task<List<DomainRequest>> GetPendingRequestsAsync(string userId)
        {
            var pk = BuildUserPk(userId);
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
                FilterExpression = "#status = :pending",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "status" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = pk } },
                    { ":skPrefix", new AttributeValue { S = "REQUEST#" } },
                    { ":pending", new AttributeValue { S = RequestStatus.PENDING.ToString() } }
                },
                ScanIndexForward = false
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(MapToDomainRequest).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GetPendingRequestsAsync failed for userId={UserId}", userId);
                throw;
            }
        }

        public async Task<List<DomainRequest>> GetSentRequestsAsync(string requesterUserId)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = Gsi2RequesterName,
                KeyConditionExpression = "GSI2PK = :gsi2pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":gsi2pk", new AttributeValue { S = $"REQUESTER#{requesterUserId}" } }
                },
                ScanIndexForward = false
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(MapToDomainRequest).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GetSentRequestsAsync failed for requester={Requester}", requesterUserId);
                throw;
            }
        }

        public async Task<bool> ResolveRequestTransactAsync(string userId, string requestSk, string newStatus, string resolvedBy, string? relatedNotifSk = null)
        {
            var nowIso = DateTime.UtcNow.ToString("O");
            var userPk = BuildUserPk(userId);

            var transactItems = new List<TransactWriteItem>
            {
                // 1. Update Domain Request status conditionally (must be PENDING)
                new()
                {
                    Update = new Update
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "PK", new AttributeValue { S = userPk } },
                            { "SK", new AttributeValue { S = requestSk } }
                        },
                        UpdateExpression = "SET #status = :newStatus, resolvedAt = :now, resolvedBy = :by",
                        ConditionExpression = "#status = :pendingStatus",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            { "#status", "status" }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            { ":newStatus", new AttributeValue { S = newStatus } },
                            { ":pendingStatus", new AttributeValue { S = RequestStatus.PENDING.ToString() } },
                            { ":now", new AttributeValue { S = nowIso } },
                            { ":by", new AttributeValue { S = resolvedBy } }
                        }
                    }
                }
            };

            // 2. If there's an associated notification, mark it read & evict from sparse GSI
            if (!string.IsNullOrEmpty(relatedNotifSk))
            {
                transactItems.Add(new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "PK", new AttributeValue { S = userPk } },
                            { "SK", new AttributeValue { S = relatedNotifSk } }
                        },
                        UpdateExpression = "SET unread = :falseVal REMOVE GSI1PK, GSI1SK",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            { ":falseVal", new AttributeValue { BOOL = false } }
                        }
                    }
                });
            }

            try
            {
                await _dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
                {
                    TransactItems = transactItems
                });
                return true;
            }
            catch (TransactionCanceledException ex)
            {
                _logger.LogWarning("ResolveRequestTransactAsync condition failed (likely already accepted/declined): {Msg}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResolveRequestTransactAsync failed unexpectedly for {UserId}, {SK}", userId, requestSk);
                throw;
            }
        }

        #endregion

        #region Preferences & Devices

        public async Task<NotificationPrefs> GetPrefsAsync(string userId)
        {
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = BuildUserPk(userId) } },
                    { "SK", new AttributeValue { S = "PREFS" } }
                }
            };

            try
            {
                var response = await _dynamoDb.GetItemAsync(request);
                if (response.Item != null && response.Item.Count > 0)
                {
                    var categoriesJson = response.Item.GetValueOrDefault("enabledCategories")?.S ?? "{}";
                    return new NotificationPrefs
                    {
                        PK = BuildUserPk(userId),
                        SK = "PREFS",
                        UserId = userId,
                        EnabledCategories = JsonConvert.DeserializeObject<Dictionary<string, bool>>(categoriesJson) ?? new(),
                        AllowPush = response.Item.GetValueOrDefault("allowPush")?.BOOL ?? true,
                        AllowToast = response.Item.GetValueOrDefault("allowToast")?.BOOL ?? true,
                        UpdatedAt = DateTime.TryParse(response.Item.GetValueOrDefault("updatedAt")?.S, out var dt) ? dt : DateTime.UtcNow
                    };
                }

                return new NotificationPrefs { PK = BuildUserPk(userId), UserId = userId };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB GetPrefsAsync failed for userId={UserId}", userId);
                return new NotificationPrefs { PK = BuildUserPk(userId), UserId = userId };
            }
        }

        public async Task UpsertPrefsAsync(NotificationPrefs prefs)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = BuildUserPk(prefs.UserId) } },
                { "SK", new AttributeValue { S = "PREFS" } },
                { "userId", new AttributeValue { S = prefs.UserId } },
                { "enabledCategories", new AttributeValue { S = JsonConvert.SerializeObject(prefs.EnabledCategories) } },
                { "allowPush", new AttributeValue { BOOL = prefs.AllowPush } },
                { "allowToast", new AttributeValue { BOOL = prefs.AllowToast } },
                { "updatedAt", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
            };

            try
            {
                await _dynamoDb.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB UpsertPrefsAsync failed for userId={UserId}", prefs.UserId);
                throw;
            }
        }

        public async Task UpsertDeviceTokenAsync(DeviceTokenItem deviceToken)
        {
            var sk = $"DEVICE#{deviceToken.Platform}#{deviceToken.Token}";
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = BuildUserPk(deviceToken.UserId) } },
                { "SK", new AttributeValue { S = sk } },
                { "userId", new AttributeValue { S = deviceToken.UserId } },
                { "platform", new AttributeValue { S = deviceToken.Platform } },
                { "token", new AttributeValue { S = deviceToken.Token } },
                { "updatedAt", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
            };

            try
            {
                await _dynamoDb.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DynamoDB UpsertDeviceTokenAsync failed for userId={UserId}", deviceToken.UserId);
                throw;
            }
        }

        #endregion

        #region Mapping Helpers

        private static Dictionary<string, AttributeValue> ToNotificationAttributeMap(NotificationItem item)
        {
            var map = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = item.PK } },
                { "SK", new AttributeValue { S = item.SK } },
                { "userId", new AttributeValue { S = item.UserId } },
                { "type", new AttributeValue { S = item.Type } },
                { "category", new AttributeValue { S = item.Category } },
                { "priority", new AttributeValue { S = item.Priority } },
                { "title", new AttributeValue { S = item.Title } },
                { "message", new AttributeValue { S = item.Message } },
                { "animation", new AttributeValue { S = item.Animation ?? AnimationKeys.NONE } },
                { "actions", new AttributeValue { S = JsonConvert.SerializeObject(item.Actions) } },
                { "unread", new AttributeValue { BOOL = item.Unread } },
                { "createdAt", new AttributeValue { S = item.CreatedAt.ToString("O") } }
            };

            if (!string.IsNullOrEmpty(item.RelatedPK))
                map["relatedPK"] = new AttributeValue { S = item.RelatedPK };

            if (!string.IsNullOrEmpty(item.RelatedSK))
                map["relatedSK"] = new AttributeValue { S = item.RelatedSK };

            if (item.Ttl.HasValue)
                map["ttl"] = new AttributeValue { N = item.Ttl.Value.ToString() };

            // Sparse GSI unread index keys: ONLY included when unread is true
            if (item.Unread)
            {
                map["GSI1PK"] = new AttributeValue { S = $"UNREAD#{item.UserId}" };
                map["GSI1SK"] = new AttributeValue { S = item.SK };
            }

            return map;
        }

        private static NotificationItem MapToNotificationItem(Dictionary<string, AttributeValue> item)
        {
            var actionsJson = item.GetValueOrDefault("actions")?.S ?? "[]";
            var actions = JsonConvert.DeserializeObject<List<NotificationAction>>(actionsJson) ?? new();

            return new NotificationItem
            {
                PK = item.GetValueOrDefault("PK")?.S ?? string.Empty,
                SK = item.GetValueOrDefault("SK")?.S ?? string.Empty,
                UserId = item.GetValueOrDefault("userId")?.S ?? string.Empty,
                Type = item.GetValueOrDefault("type")?.S ?? string.Empty,
                Category = item.GetValueOrDefault("category")?.S ?? NotificationCategory.CONQUISTAS.ToString(),
                Priority = item.GetValueOrDefault("priority")?.S ?? NotificationPriority.NORMAL.ToString(),
                Title = item.GetValueOrDefault("title")?.S ?? string.Empty,
                Message = item.GetValueOrDefault("message")?.S ?? string.Empty,
                Animation = item.GetValueOrDefault("animation")?.S ?? AnimationKeys.NONE,
                Actions = actions,
                RelatedPK = item.GetValueOrDefault("relatedPK")?.S,
                RelatedSK = item.GetValueOrDefault("relatedSK")?.S,
                Unread = item.GetValueOrDefault("unread")?.BOOL ?? false,
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var ca) ? ca : DateTime.UtcNow,
                Ttl = long.TryParse(item.GetValueOrDefault("ttl")?.N, out var ttl) ? ttl : null
            };
        }

        private static Dictionary<string, AttributeValue> ToDomainRequestAttributeMap(DomainRequest request)
        {
            var map = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = request.PK } },
                { "SK", new AttributeValue { S = request.SK } },
                { "recipientUserId", new AttributeValue { S = request.RecipientUserId } },
                { "requesterUserId", new AttributeValue { S = request.RequesterUserId } },
                { "requestType", new AttributeValue { S = request.RequestType } },
                { "status", new AttributeValue { S = request.Status } },
                { "payloadJson", new AttributeValue { S = request.PayloadJson } },
                { "createdAt", new AttributeValue { S = request.CreatedAt.ToString("O") } },
                // GSI2 reverse requester lookup
                { "GSI2PK", new AttributeValue { S = $"REQUESTER#{request.RequesterUserId}" } },
                { "GSI2SK", new AttributeValue { S = request.SK } }
            };

            if (!string.IsNullOrEmpty(request.RelatedNotifSK))
                map["relatedNotifSK"] = new AttributeValue { S = request.RelatedNotifSK };

            if (request.ResolvedAt.HasValue)
                map["resolvedAt"] = new AttributeValue { S = request.ResolvedAt.Value.ToString("O") };

            if (!string.IsNullOrEmpty(request.ResolvedBy))
                map["resolvedBy"] = new AttributeValue { S = request.ResolvedBy };

            if (request.Ttl.HasValue)
                map["ttl"] = new AttributeValue { N = request.Ttl.Value.ToString() };

            return map;
        }

        private static DomainRequest MapToDomainRequest(Dictionary<string, AttributeValue> item)
        {
            return new DomainRequest
            {
                PK = item.GetValueOrDefault("PK")?.S ?? string.Empty,
                SK = item.GetValueOrDefault("SK")?.S ?? string.Empty,
                RecipientUserId = item.GetValueOrDefault("recipientUserId")?.S ?? string.Empty,
                RequesterUserId = item.GetValueOrDefault("requesterUserId")?.S ?? string.Empty,
                RequestType = item.GetValueOrDefault("requestType")?.S ?? string.Empty,
                Status = item.GetValueOrDefault("status")?.S ?? RequestStatus.PENDING.ToString(),
                PayloadJson = item.GetValueOrDefault("payloadJson")?.S ?? "{}",
                RelatedNotifSK = item.GetValueOrDefault("relatedNotifSK")?.S,
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var ca) ? ca : DateTime.UtcNow,
                ResolvedAt = DateTime.TryParse(item.GetValueOrDefault("resolvedAt")?.S, out var ra) ? ra : null,
                ResolvedBy = item.GetValueOrDefault("resolvedBy")?.S,
                Ttl = long.TryParse(item.GetValueOrDefault("ttl")?.N, out var ttl) ? ttl : null
            };
        }

        #endregion
    }
}
