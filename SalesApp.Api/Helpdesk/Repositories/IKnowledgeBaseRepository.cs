using System.Collections.Generic;
using System.Threading.Tasks;
using SalesApp.Helpdesk.Models;

namespace SalesApp.Helpdesk.Repositories
{
    public interface IKnowledgeBaseRepository
    {
        Task SaveArticleAsync(KnowledgeBaseArticle article);
        Task<List<KnowledgeBaseArticle>> ListArticlesAsync(string? category = null);
        Task<KnowledgeBaseArticle?> GetArticleBySlugAsync(string slug);
        Task<KnowledgeBaseArticle?> GetArticleByIdAsync(string articleId);
    }
}
