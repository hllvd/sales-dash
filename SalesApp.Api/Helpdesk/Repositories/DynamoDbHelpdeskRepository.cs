using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalesApp.Helpdesk.Models;
using SalesApp.ReportFilters.Settings;

namespace SalesApp.Helpdesk.Repositories
{
    public class DynamoDbHelpdeskRepository : IHelpdeskRepository
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbHelpdeskRepository> _logger;

        private const string GsiName = "GSI1";

        public DynamoDbHelpdeskRepository(
            IAmazonDynamoDB dynamoDb,
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbHelpdeskRepository> logger)
        {
            _dynamoDb = dynamoDb;
            _tableName = !string.IsNullOrEmpty(settings.Value.HelpdeskTable)
                ? settings.Value.HelpdeskTable
                : "salesapp-helpdesk";
            _logger = logger;
        }

        public async Task CreateQuestionAsync(HelpdeskQuestion question)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"FAQ#{question.QuestionId}" } },
                { "SK", new AttributeValue { S = "METADATA" } },
                { "GSI1PK", new AttributeValue { S = $"USER#{question.UserId}" } },
                { "GSI1SK", new AttributeValue { S = $"STATUS#{question.Status}#{question.CreatedAt:O}" } },
                { "questionId", new AttributeValue { S = question.QuestionId } },
                { "userId", new AttributeValue { S = question.UserId } },
                { "userEmail", new AttributeValue { S = question.UserEmail } },
                { "userName", new AttributeValue { S = question.UserName } },
                { "title", new AttributeValue { S = question.Title } },
                { "currentContent", new AttributeValue { S = question.CurrentContent } },
                { "sourcePageUrl", new AttributeValue { S = question.SourcePageUrl ?? "" } },
                { "status", new AttributeValue { S = question.Status } },
                { "createdAt", new AttributeValue { S = question.CreatedAt.ToString("O") } },
                { "updatedAt", new AttributeValue { S = question.UpdatedAt.ToString("O") } },
                { "version", new AttributeValue { N = "1" } }
            };

            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            };

            await _dynamoDb.PutItemAsync(request);

            // Also maintain registry item under PK = SUPERADMIN#FAQS to allow query without dynamodb:Scan
            var superAdminItem = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = "SUPERADMIN#FAQS" } },
                { "SK", new AttributeValue { S = $"FAQ#{question.QuestionId}" } },
                { "questionId", new AttributeValue { S = question.QuestionId } },
                { "userId", new AttributeValue { S = question.UserId } },
                { "userEmail", new AttributeValue { S = question.UserEmail } },
                { "userName", new AttributeValue { S = question.UserName } },
                { "title", new AttributeValue { S = question.Title } },
                { "currentContent", new AttributeValue { S = question.CurrentContent } },
                { "sourcePageUrl", new AttributeValue { S = question.SourcePageUrl ?? "" } },
                { "status", new AttributeValue { S = question.Status } },
                { "createdAt", new AttributeValue { S = question.CreatedAt.ToString("O") } },
                { "updatedAt", new AttributeValue { S = question.UpdatedAt.ToString("O") } }
            };

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = superAdminItem
            });
        }

        public async Task<HelpdeskQuestion?> GetQuestionByIdAsync(string questionId)
        {
            var pk = $"FAQ#{questionId}";
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = pk } }
                }
            };

            var response = await _dynamoDb.QueryAsync(request);
            if (response.Items == null || response.Items.Count == 0)
                return null;

            var metadataItem = response.Items.FirstOrDefault(i => i.GetValueOrDefault("SK")?.S == "METADATA");
            if (metadataItem == null)
                return null;

            var question = MapMetadataToModel(metadataItem);

            foreach (var item in response.Items)
            {
                var sk = item.GetValueOrDefault("SK")?.S ?? "";
                if (sk.StartsWith("MSG#"))
                {
                    question.Messages.Add(new HelpdeskMessage
                    {
                        MessageId = item.GetValueOrDefault("messageId")?.S ?? "",
                        QuestionId = question.QuestionId,
                        SenderUserId = item.GetValueOrDefault("senderUserId")?.S ?? "",
                        SenderName = item.GetValueOrDefault("senderName")?.S ?? "",
                        SenderRole = item.GetValueOrDefault("senderRole")?.S ?? "",
                        Content = item.GetValueOrDefault("content")?.S ?? "",
                        CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var ca) ? ca : DateTime.UtcNow
                    });
                }
                else if (sk.StartsWith("HIST#"))
                {
                    question.EditHistory.Add(new HelpdeskEditHistory
                    {
                        Version = int.TryParse(item.GetValueOrDefault("version")?.N, out var v) ? v : 1,
                        PreviousContent = item.GetValueOrDefault("previousContent")?.S ?? "",
                        EditedAt = DateTime.TryParse(item.GetValueOrDefault("editedAt")?.S, out var ea) ? ea : DateTime.UtcNow
                    });
                }
            }

            question.Messages = question.Messages.OrderBy(m => m.CreatedAt).ToList();
            question.EditHistory = question.EditHistory.OrderBy(h => h.Version).ToList();

            return question;
        }

        public async Task UpdateQuestionContentAsync(string questionId, string newContent, string previousContent, int version)
        {
            var now = DateTime.UtcNow;
            var historyItem = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"FAQ#{questionId}" } },
                { "SK", new AttributeValue { S = $"HIST#{now:O}#V{version}" } },
                { "version", new AttributeValue { N = version.ToString() } },
                { "previousContent", new AttributeValue { S = previousContent } },
                { "editedAt", new AttributeValue { S = now.ToString("O") } }
            };

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = historyItem
            });

            var updateRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"FAQ#{questionId}" } },
                    { "SK", new AttributeValue { S = "METADATA" } }
                },
                UpdateExpression = "SET currentContent = :newContent, updatedAt = :updatedAt, version = version + :inc",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":newContent", new AttributeValue { S = newContent } },
                    { ":updatedAt", new AttributeValue { S = now.ToString("O") } },
                    { ":inc", new AttributeValue { N = "1" } }
                }
            };

            await _dynamoDb.UpdateItemAsync(updateRequest);
        }

        public async Task UpdateStatusAsync(string questionId, string newStatus)
        {
            var now = DateTime.UtcNow;
            var q = await GetQuestionByIdAsync(questionId);
            if (q == null) return;

            var updateRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"FAQ#{questionId}" } },
                    { "SK", new AttributeValue { S = "METADATA" } }
                },
                UpdateExpression = "SET #st = :newStatus, GSI1SK = :gsi1sk, globalGsiSk = :globalGsiSk, updatedAt = :updatedAt",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#st", "status" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":newStatus", new AttributeValue { S = newStatus } },
                    { ":gsi1sk", new AttributeValue { S = $"STATUS#{newStatus}#{q.CreatedAt:O}" } },
                    { ":globalGsiSk", new AttributeValue { S = $"STATUS#{newStatus}#{now:O}" } },
                    { ":updatedAt", new AttributeValue { S = now.ToString("O") } }
                }
            };

            await _dynamoDb.UpdateItemAsync(updateRequest);

            // Update status on SUPERADMIN#FAQS registry item
            try
            {
                var updateRegistry = new UpdateItemRequest
                {
                    TableName = _tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "PK", new AttributeValue { S = "SUPERADMIN#FAQS" } },
                        { "SK", new AttributeValue { S = $"FAQ#{questionId}" } }
                    },
                    UpdateExpression = "SET #st = :newStatus, updatedAt = :updatedAt",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#st", "status" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":newStatus", new AttributeValue { S = newStatus } },
                        { ":updatedAt", new AttributeValue { S = now.ToString("O") } }
                    }
                };
                await _dynamoDb.UpdateItemAsync(updateRegistry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update status on SUPERADMIN#FAQS item for {QuestionId}", questionId);
            }
        }

        public async Task AddMessageAsync(string questionId, HelpdeskMessage message)
        {
            var now = DateTime.UtcNow;
            var messageItem = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"FAQ#{questionId}" } },
                { "SK", new AttributeValue { S = $"MSG#{now:O}#{message.MessageId}" } },
                { "messageId", new AttributeValue { S = message.MessageId } },
                { "senderUserId", new AttributeValue { S = message.SenderUserId } },
                { "senderName", new AttributeValue { S = message.SenderName } },
                { "senderRole", new AttributeValue { S = message.SenderRole } },
                { "content", new AttributeValue { S = message.Content } },
                { "createdAt", new AttributeValue { S = now.ToString("O") } }
            };

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = messageItem
            });

            var updateMetadata = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"FAQ#{questionId}" } },
                    { "SK", new AttributeValue { S = "METADATA" } }
                },
                UpdateExpression = "SET updatedAt = :updatedAt",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":updatedAt", new AttributeValue { S = now.ToString("O") } }
                }
            };

            await _dynamoDb.UpdateItemAsync(updateMetadata);
        }

        public async Task<List<HelpdeskQuestion>> ListByUserAsync(string userId)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = GsiName,
                KeyConditionExpression = "GSI1PK = :gsi1pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":gsi1pk", new AttributeValue { S = $"USER#{userId}" } }
                },
                ScanIndexForward = false
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(MapMetadataToModel).OrderByDescending(q => q.UpdatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListByUserAsync GSI query failed for user {UserId}", userId);
                return new List<HelpdeskQuestion>();
            }
        }

        public async Task<List<HelpdeskQuestion>> ListAllForSuperAdminAsync(string? statusFilter = null)
        {
            var queryRequest = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk",
                ScanIndexForward = false,
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = "SUPERADMIN#FAQS" } }
                }
            };

            if (!string.IsNullOrEmpty(statusFilter))
            {
                queryRequest.FilterExpression = "#st = :status";
                queryRequest.ExpressionAttributeNames = new Dictionary<string, string> { { "#st", "status" } };
                queryRequest.ExpressionAttributeValues[":status"] = new AttributeValue { S = statusFilter };
            }

            try
            {
                var response = await _dynamoDb.QueryAsync(queryRequest);
                return response.Items.Select(MapMetadataToModel).OrderByDescending(q => q.UpdatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListAllForSuperAdminAsync query failed");
                return new List<HelpdeskQuestion>();
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId, bool isSuperAdmin)
        {
            if (isSuperAdmin)
            {
                var query = new QueryRequest
                {
                    TableName = _tableName,
                    KeyConditionExpression = "PK = :pk",
                    FilterExpression = "#st = :naoLido OR #st = :pendente",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#st", "status" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":pk", new AttributeValue { S = "SUPERADMIN#FAQS" } },
                        { ":naoLido", new AttributeValue { S = "Não lido" } },
                        { ":pendente", new AttributeValue { S = "Pendente" } }
                    },
                    Select = Select.COUNT
                };

                try
                {
                    var res = await _dynamoDb.QueryAsync(query);
                    return res.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GetUnreadCountAsync for superadmin query failed");
                    return 0;
                }
            }
            else
            {
                var query = new QueryRequest
                {
                    TableName = _tableName,
                    IndexName = GsiName,
                    KeyConditionExpression = "GSI1PK = :gsi1pk",
                    FilterExpression = "#st = :naoLido OR #st = :pendente",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        { "#st", "status" }
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":gsi1pk", new AttributeValue { S = $"USER#{userId}" } },
                        { ":naoLido", new AttributeValue { S = "Não lido" } },
                        { ":pendente", new AttributeValue { S = "Pendente" } }
                    },
                    Select = Select.COUNT
                };

                try
                {
                    var res = await _dynamoDb.QueryAsync(query);
                    return res.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GetUnreadCountAsync for user {UserId} failed", userId);
                    return 0;
                }
            }
        }

        private static HelpdeskQuestion MapMetadataToModel(Dictionary<string, AttributeValue> item)
        {
            return new HelpdeskQuestion
            {
                QuestionId = item.GetValueOrDefault("questionId")?.S ?? "",
                UserId = item.GetValueOrDefault("userId")?.S ?? "",
                UserEmail = item.GetValueOrDefault("userEmail")?.S ?? "",
                UserName = item.GetValueOrDefault("userName")?.S ?? "",
                Title = item.GetValueOrDefault("title")?.S ?? "",
                CurrentContent = item.GetValueOrDefault("currentContent")?.S ?? "",
                SourcePageUrl = item.GetValueOrDefault("sourcePageUrl")?.S ?? "",
                Status = item.GetValueOrDefault("status")?.S ?? "Não lido",
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var ca) ? ca : DateTime.UtcNow,
                UpdatedAt = DateTime.TryParse(item.GetValueOrDefault("updatedAt")?.S, out var ua) ? ua : DateTime.UtcNow
            };
        }
    }
}
