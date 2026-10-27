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
    public class DynamoDbKnowledgeBaseRepository : IKnowledgeBaseRepository
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbKnowledgeBaseRepository> _logger;

        public DynamoDbKnowledgeBaseRepository(
            IAmazonDynamoDB dynamoDb,
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbKnowledgeBaseRepository> logger)
        {
            _dynamoDb = dynamoDb;
            _tableName = !string.IsNullOrEmpty(settings.Value.HelpdeskTable)
                ? settings.Value.HelpdeskTable
                : "salesapp-helpdesk";
            _logger = logger;
        }

        public async Task SaveArticleAsync(KnowledgeBaseArticle article)
        {
            var now = DateTime.UtcNow;
            var item = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"KB#{article.ArticleId}" } },
                { "SK", new AttributeValue { S = "METADATA" } },
                { "articleId", new AttributeValue { S = article.ArticleId } },
                { "questionId", new AttributeValue { S = article.QuestionId } },
                { "category", new AttributeValue { S = article.Category } },
                { "title", new AttributeValue { S = article.Title } },
                { "slug", new AttributeValue { S = article.Slug } },
                { "summaryContent", new AttributeValue { S = article.SummaryContent } },
                { "status", new AttributeValue { S = article.Status } },
                { "createdAt", new AttributeValue { S = article.CreatedAt.ToString("O") } },
                { "updatedAt", new AttributeValue { S = now.ToString("O") } }
            };

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            });

            // Registry item under PK = KB#ARTICLES for fast querying by SK prefix
            var registryItem = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = "KB#ARTICLES" } },
                { "SK", new AttributeValue { S = $"CAT#{article.Category.ToUpperInvariant()}#SLUG#{article.Slug}" } },
                { "articleId", new AttributeValue { S = article.ArticleId } },
                { "questionId", new AttributeValue { S = article.QuestionId } },
                { "category", new AttributeValue { S = article.Category } },
                { "title", new AttributeValue { S = article.Title } },
                { "slug", new AttributeValue { S = article.Slug } },
                { "summaryContent", new AttributeValue { S = article.SummaryContent } },
                { "status", new AttributeValue { S = article.Status } },
                { "createdAt", new AttributeValue { S = article.CreatedAt.ToString("O") } },
                { "updatedAt", new AttributeValue { S = now.ToString("O") } }
            };

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = registryItem
            });
        }

        public async Task<List<KnowledgeBaseArticle>> ListArticlesAsync(string? category = null)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = "KB#ARTICLES" } }
                },
                ScanIndexForward = false
            };

            if (!string.IsNullOrEmpty(category))
            {
                request.KeyConditionExpression = "PK = :pk AND begins_with(SK, :catPrefix)";
                request.ExpressionAttributeValues[":catPrefix"] = new AttributeValue { S = $"CAT#{category.ToUpperInvariant()}#" };
            }

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                return response.Items.Select(MapToArticle).OrderByDescending(a => a.UpdatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListArticlesAsync failed");
                return new List<KnowledgeBaseArticle>();
            }
        }

        public async Task<KnowledgeBaseArticle?> GetArticleBySlugAsync(string slug)
        {
            var articles = await ListArticlesAsync();
            return articles.FirstOrDefault(a => string.Equals(a.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<KnowledgeBaseArticle?> GetArticleByIdAsync(string articleId)
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND SK = :sk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":pk", new AttributeValue { S = $"KB#{articleId}" } },
                    { ":sk", new AttributeValue { S = "METADATA" } }
                }
            };

            try
            {
                var response = await _dynamoDb.QueryAsync(request);
                var item = response.Items.FirstOrDefault();
                return item != null ? MapToArticle(item) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArticleByIdAsync failed for {ArticleId}", articleId);
                return null;
            }
        }

        private static KnowledgeBaseArticle MapToArticle(Dictionary<string, AttributeValue> item)
        {
            return new KnowledgeBaseArticle
            {
                ArticleId = item.GetValueOrDefault("articleId")?.S ?? "",
                QuestionId = item.GetValueOrDefault("questionId")?.S ?? "",
                Category = item.GetValueOrDefault("category")?.S ?? "FAQ",
                Title = item.GetValueOrDefault("title")?.S ?? "",
                Slug = item.GetValueOrDefault("slug")?.S ?? "",
                SummaryContent = item.GetValueOrDefault("summaryContent")?.S ?? "",
                Status = item.GetValueOrDefault("status")?.S ?? "published",
                CreatedAt = DateTime.TryParse(item.GetValueOrDefault("createdAt")?.S, out var ca) ? ca : DateTime.UtcNow,
                UpdatedAt = DateTime.TryParse(item.GetValueOrDefault("updatedAt")?.S, out var ua) ? ua : DateTime.UtcNow
            };
        }
    }
}
