using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CMS.Websites;
using DancingGoat.Models;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Logging;
using Kentico.Content.Web.Mvc.Routing;

namespace Kentico.Xperience.Backend.GraphQL
{
    /// <summary>
    /// GraphQL resolver for article-related queries
    /// </summary>
    [ExtendObjectType("Query")]
    public class ArticlesResolver
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<ArticlesResolver> logger;
        private readonly IPreferredLanguageRetriever preferredLanguageRetriever;

        public ArticlesResolver(
            IServiceProvider serviceProvider, 
            ILogger<ArticlesResolver> logger,
            IPreferredLanguageRetriever preferredLanguageRetriever)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.preferredLanguageRetriever = preferredLanguageRetriever;
        }

        /// <summary>
        /// Gets all articles from the specified path
        /// </summary>
        /// <param name="path">The path in the content tree (default: "/")</param>
        /// <returns>List of article page data</returns>
        [GraphQLName("getArticles")]
        public async Task<List<ArticlePageModel>> GetArticles(string path = "/", string language = null)
        {
            try
            {
                // Get language
                string languageCode = language ?? preferredLanguageRetriever.Get();
                
                // Log that we're trying to get articles
                logger.LogInformation($"Getting articles from path: {path}, language: {languageCode}");
                
                // Get the repository from the service provider
                var repository = serviceProvider.GetRequiredService<ArticlePageRepository>();
                
                // Get articles from the repository - using more flexible search
                var articles = await repository.GetArticlePages(path, languageCode, true);
                
                if (articles == null || !articles.Any())
                {
                    logger.LogWarning($"No articles found at path: {path}, trying root path");
                    // Try with root path if no articles found
                    articles = await repository.GetArticlePages("/", languageCode, true);
                    
                    if (articles == null || !articles.Any())
                    {
                        logger.LogWarning("No articles found at root path either");
                        return new List<ArticlePageModel>();
                    }
                }
                
                logger.LogInformation($"Found {articles.Count()} articles");
                
                // Map to the GraphQL model
                return articles.Select(article => MapToArticlePageModel(article)).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error getting articles from path: {path}");
                throw; // Re-throw the exception to let GraphQL handle it
            }
        }

        /// <summary>
        /// Gets a specific article by ID
        /// </summary>
        /// <param name="id">The article ID</param>
        /// <returns>Article page data</returns>
        [GraphQLName("getArticleById")]
        public async Task<ArticlePageModel> GetArticleById(string id, string language = null)
        {
            try
            {
                if (!int.TryParse(id, out int articleId))
                {
                    logger.LogWarning($"Invalid article ID format: {id}");
                    return new ArticlePageModel();
                }

                // Get language
                string languageCode = language ?? preferredLanguageRetriever.Get();
                
                logger.LogInformation($"Getting article by ID: {articleId}, language: {languageCode}");
                
                // Get the repository from the service provider
                var repository = serviceProvider.GetRequiredService<ArticlePageRepository>();
                
                // Get the article from the repository
                var article = await repository.GetArticlePage(articleId, languageCode);
                
                if (article == null)
                {
                    logger.LogWarning($"Article with ID {articleId} not found");
                    return new ArticlePageModel();
                }
                
                logger.LogInformation($"Found article: {article.ArticleTitle}");
                
                // Map to the GraphQL model
                return MapToArticlePageModel(article);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error getting article by ID: {id}");
                throw; // Re-throw the exception to let GraphQL handle it
            }
        }
        
        private ArticlePageModel MapToArticlePageModel(ArticlePage article)
        {
            try
            {
                var url = article.GetUrl();
                
                return new ArticlePageModel
                {
                    Id = article.SystemFields.WebPageItemID.ToString(),
                    Title = article.ArticleTitle,
                    Summary = article.ArticlePageSummary,
                    Text = article.ArticlePageText,
                    PublishDate = article.ArticlePagePublishDate.ToString("yyyy-MM-dd"),
                    Teaser = article.ArticlePageTeaser?.FirstOrDefault()?.ImageFile?.Url,
                    Url = url?.RelativePath ?? $"/articles/{article.SystemFields.WebPageItemID}"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error mapping article to model");
                
                // Return partial data if mapping fails
                return new ArticlePageModel
                {
                    Id = article.SystemFields.WebPageItemID.ToString(),
                    Title = article.ArticleTitle
                };
            }
        }
    }

    /// <summary>
    /// Represents an Article Page content type
    /// </summary>
    public class ArticlePageModel
    {
        [GraphQLName("id")]
        public string Id { get; set; }

        [GraphQLName("title")]
        public string Title { get; set; }

        [GraphQLName("summary")]
        public string Summary { get; set; }

        [GraphQLName("text")]
        public string Text { get; set; }

        [GraphQLName("publishDate")]
        public string PublishDate { get; set; }

        [GraphQLName("teaser")]
        public string Teaser { get; set; }

        [GraphQLName("url")]
        public string Url { get; set; }
    }
} 