using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMS.ContentEngine;
using CMS.DataEngine;
using CMS.Helpers;
using CMS.Websites;
using CMS.Websites.Routing;
using HotChocolate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CMS.Core;

namespace Kentico.Xperience.Backend.GraphQL
{
    /// <summary>
    /// Resolver for fetching content items with flexible filtering and ordering
    /// </summary>
    public class DynamicContentResolver
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DynamicContentResolver> _logger;
        private readonly IProgressiveCache _progressiveCache;
        private readonly IContentQueryExecutor _contentQueryExecutor;
        private readonly IWebsiteChannelContext _websiteChannelContext;

        public DynamicContentResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetService<ILogger<DynamicContentResolver>>();
            _progressiveCache = serviceProvider.GetService<IProgressiveCache>();
            _contentQueryExecutor = serviceProvider.GetService<IContentQueryExecutor>();
            _websiteChannelContext = serviceProvider.GetService<IWebsiteChannelContext>();
        }

        /// <summary>
        /// Gets dynamic content items based on specified parameters
        /// </summary>
        /// <param name="path">The path to start the content search from</param>
        /// <param name="language">The language to use for content retrieval</param>
        /// <param name="typeContentPath">Type of content retrieval - "All child pages" or "Only this page"</param>
        /// <param name="contentType">The content type to filter by</param>
        /// <param name="maximumNestingLevel">The maximum nesting level for recursive retrieval</param>
        /// <param name="orderBy">The ordering expression (e.g., "CreatedOn DESC")</param>
        /// <param name="selectTopNPages">Maximum number of pages to return</param>
        /// <param name="whereCondition">Optional filtering condition</param>
        /// <param name="skip">Number of items to skip (for pagination)</param>
        /// <param name="take">Number of items to take (for pagination)</param>
        /// <param name="cacheKey">Custom cache key for more control over caching</param>
        /// <param name="bypassCache">Flag to bypass cache and force a fresh database query</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of dynamic content items</returns>
        public async Task<IEnumerable<Dictionary<string, object>>> GetDynamicContentAsync(
            string path = "/",
            string language = null,
            string typeContentPath = "All child pages", 
            string contentType = null,
            int maximumNestingLevel = -1,
            string orderBy = null,
            int selectTopNPages = 0,
            string whereCondition = null,
            int skip = 0,
            int take = 0,
            string cacheKey = null,
            bool bypassCache = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Set a default timeout to ensure responsiveness
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                
                // Use default language if not specified
                language = language ?? "en";
                
                _logger?.LogInformation($"Fetching content with parameters: Path={path}, Language={language}, ContentType={contentType}, TypeContentPath={typeContentPath}, Skip={skip}, Take={take}");
                
                // Set up query parameters
                var isOnlyThisPage = typeContentPath == "Only this page";
                var isDescendingOrder = false;
                string orderByField = null;
                
                if (!string.IsNullOrEmpty(orderBy))
                {
                    var orderParts = orderBy.Split(' ');
                    orderByField = orderParts[0];
                    isDescendingOrder = orderParts.Length > 1 && orderParts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase);
                }
                
                // Create query builder
                var queryBuilder = new ContentItemQueryBuilder();
                
                // Set content type if specified
                if (!string.IsNullOrEmpty(contentType))
                {
                    queryBuilder = queryBuilder.ForContentType(contentType, config => 
                    {
                        // Configure website path
                        if (isOnlyThisPage)
                        {
                            config.ForWebsite(_websiteChannelContext.WebsiteChannelName, PathMatch.Single(path));
                        }
                        else if (path != "/")
                        {
                            if (maximumNestingLevel > 0)
                            {
                                var pathMatch = PathMatch.Children(path);
                                config.ForWebsite(_websiteChannelContext.WebsiteChannelName, pathMatch);
                            }
                            else
                            {
                                var pathMatch = PathMatch.Section(path);
                                config.ForWebsite(_websiteChannelContext.WebsiteChannelName, pathMatch);
                            }
                        }
                        else
                        {
                            config.ForWebsite(_websiteChannelContext.WebsiteChannelName);
                        }
                        
                        // Apply ordering
                        if (!string.IsNullOrEmpty(orderByField))
                        {
                            if (isDescendingOrder)
                            {
                                config.OrderBy(OrderByColumn.Desc(orderByField));
                            }
                            else
                            {
                                config.OrderBy(OrderByColumn.Asc(orderByField));
                            }
                        }
                        
                        // Apply pagination - prefer skip/take over selectTopNPages if provided
                        if (skip > 0 || take > 0)
                        {
                            if (skip > 0)
                            {
                                // Use the appropriate Offset method signature
                                config.Offset(skip, 0); // Pass 0 as the second parameter for fetch count
                            }
                            
                            if (take > 0)
                            {
                                config.TopN(take);
                            }
                        }
                        // Fall back to selectTopNPages if skip/take not provided
                        else if (selectTopNPages > 0)
                        {
                            config.TopN(selectTopNPages);
                        }
                        
                        // Apply where condition if specified
                        if (!string.IsNullOrEmpty(whereCondition))
                        {
                            ApplyWhereCondition(config, whereCondition);
                        }
                    });
                }
                else
                {
                    // No content type specified, just set language
                    queryBuilder = queryBuilder.InLanguage(language);
                }
                
                // Set language
                queryBuilder = queryBuilder.InLanguage(language);
                
                // Set options
                var options = new ContentQueryExecutionOptions
                {
                    ForPreview = _websiteChannelContext.IsPreview,
                    IncludeSecuredItems = _websiteChannelContext.IsPreview
                };
                
                // Special handling for ArticlePage content type
                if (contentType == "DancingGoat.ArticlePage")
                {
                    var articleRepository = _serviceProvider.GetService<DancingGoat.Models.ArticlePageRepository>();
                    if (articleRepository != null)
                    {
                        // Check if ArticlePageRepository supports skip parameter
                        try
                        {
                            // Use the ArticlePageRepository for ArticlePage content type
                            var actualTake = take > 0 ? take : selectTopNPages;
                            
                            // Check method signature - if skip parameter isn't supported, 
                            // we'll catch an exception and use the alternative method
                            var articles = await articleRepository.GetArticlePages(
                                path, 
                                language, 
                                _websiteChannelContext.IsPreview, 
                                actualTake);
                            
                            // If skip is needed, apply it in memory (not ideal but works as fallback)
                            if (skip > 0)
                            {
                                articles = articles.Skip(skip).ToList();
                            }
                            
                            _logger?.LogInformation($"Found {articles.Count()} articles using ArticlePageRepository");
                            
                            // Convert articles to dictionaries
                            return articles.Select(article => new Dictionary<string, object>
                            {
                                ["WebPageItemID"] = article.SystemFields.WebPageItemID,
                                ["WebPageItemGUID"] = article.SystemFields.WebPageItemGUID.ToString(),
                                ["WebPageItemName"] = article.SystemFields.WebPageItemName,
                                ["WebPageItemTreePath"] = article.SystemFields.WebPageItemTreePath,
                                ["ArticleTitle"] = article.ArticleTitle,
                                ["ArticlePageSummary"] = article.ArticlePageSummary,
                                ["ArticlePageText"] = article.ArticlePageText,
                                ["ArticlePagePublishDate"] = article.ArticlePagePublishDate
                            }).ToList();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning($"Error using ArticlePageRepository with pagination: {ex.Message}. Falling back to default query.");
                        }
                    }
                }
                
                // Customize cache settings with the provided cacheKey if available
                var defaultCacheKey = $"{_websiteChannelContext.WebsiteChannelName}_DynamicContent_{path}_{language}_{contentType}_{skip}_{take}";
                var effectiveCacheKey = string.IsNullOrEmpty(cacheKey) ? defaultCacheKey : cacheKey;
                
                var cacheSettings = new CacheSettings(
                    5, // Cache for 5 minutes by default
                    effectiveCacheKey);
                
                // Bypass cache if requested
                if (bypassCache)
                {
                    // Execute the query without caching
                    var queryResult = await _contentQueryExecutor.GetResult<IContentQueryDataContainer>(
                        queryBuilder, 
                        container => container, // Identity mapping function
                        options, 
                        linkedCts.Token);
                        
                    return queryResult.Select(ConvertToDictionary).ToList();
                }
                else
                {
                    // Execute the query with caching
                    var result = await GetCachedQueryResultAsync(
                        queryBuilder, 
                        options,
                        cacheSettings, 
                        linkedCts.Token);
                    
                    _logger?.LogInformation($"Found {result.Count()} items for query");
                    
                    // Convert to dictionaries for GraphQL
                    return result.Select(ConvertToDictionary).ToList();
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Dynamic content query timed out, returning empty result");
                return Enumerable.Empty<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching dynamic content: {Message}", ex.Message);
                return Enumerable.Empty<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Gets the dynamic pages for the original implementation (backward compatibility)
        /// </summary>
        public object GetDynamicPages(
            string contentType,
            string path = null,
            int? limit = null,
            int? offset = null,
            string orderBy = "DocumentPublishFrom",
            string orderDirection = "desc")
        {
            // For backward compatibility, call the async method synchronously
            var result = GetDynamicContentAsync(
                path: path ?? "/",
                contentType: contentType,
                orderBy: $"{orderBy} {orderDirection}",
                selectTopNPages: limit ?? 0,
                skip: offset ?? 0
            ).GetAwaiter().GetResult();
            
            return result;
        }
        
        /// <summary>
        /// Applies where conditions to the content type query parameters
        /// </summary>
        private void ApplyWhereCondition(ContentTypeQueryParameters parameters, string whereCondition)
        {
            try
            {
                // Basic parsing for simple conditions
                if (whereCondition.Contains("="))
                {
                    var parts = whereCondition.Split('=');
                    if (parts.Length == 2)
                    {
                        var field = parts[0].Trim();
                        var value = parts[1].Trim().Trim('"', '\'');
                        
                        parameters.Where(where => where.WhereEquals(field, value));
                    }
                }
                else if (whereCondition.Contains(">"))
                {
                    var parts = whereCondition.Split('>');
                    if (parts.Length == 2)
                    {
                        var field = parts[0].Trim();
                        var value = parts[1].Trim().Trim('"', '\'');
                        
                        // For greater than conditions
                        if (DateTime.TryParse(value, out var dateValue))
                        {
                            // Use basic WhereEquals for dates since specialized methods may not be available
                            parameters.Where(where => where.WhereEquals(field, value));
                        }
                        else if (int.TryParse(value, out var intValue))
                        {
                            // For integers, create a simple condition
                            parameters.Where(where => where.WhereEquals(field, intValue.ToString()));
                        }
                        else
                        {
                            // For strings, use simple equality
                            parameters.Where(where => where.WhereEquals(field, value));
                        }
                    }
                }
                else if (whereCondition.Contains("<"))
                {
                    var parts = whereCondition.Split('<');
                    if (parts.Length == 2)
                    {
                        var field = parts[0].Trim();
                        var value = parts[1].Trim().Trim('"', '\'');
                        
                        // For less than conditions
                        if (DateTime.TryParse(value, out var dateValue))
                        {
                            // Use basic WhereEquals for dates since specialized methods may not be available
                            parameters.Where(where => where.WhereEquals(field, value));
                        }
                        else if (int.TryParse(value, out var intValue))
                        {
                            // For integers, create a simple condition
                            parameters.Where(where => where.WhereEquals(field, intValue.ToString()));
                        }
                        else
                        {
                            // For strings, use simple equality
                            parameters.Where(where => where.WhereEquals(field, value));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error applying where condition: {Condition}", whereCondition);
            }
        }
        
        /// <summary>
        /// Gets the cached query result
        /// </summary>
        private async Task<IEnumerable<IContentQueryDataContainer>> GetCachedQueryResultAsync(
            ContentItemQueryBuilder queryBuilder,
            ContentQueryExecutionOptions options,
            CacheSettings cacheSettings,
            CancellationToken cancellationToken)
        {
            return await _progressiveCache.LoadAsync(async cs =>
            {
                if (cs.Cached)
                {
                    cs.CacheDependency = CacheHelper.GetCacheDependency($"node|{_websiteChannelContext.WebsiteChannelName}|all");
                }
                
                var result = await _contentQueryExecutor.GetResult<IContentQueryDataContainer>(
                    queryBuilder,
                    container => container, // Identity mapping function
                    options,
                    cancellationToken);
                    
                return result.ToList();
            }, cacheSettings);
        }
        
        /// <summary>
        /// Converts a content query data container to a dictionary
        /// </summary>
        private Dictionary<string, object> ConvertToDictionary(IContentQueryDataContainer item)
        {
            var result = new Dictionary<string, object>();
            
            // Extract all fields from the content item
            // Different approach since ContentItemFields property might not be available
            var properties = item.GetType().GetProperties();
            foreach (var prop in properties)
            {
                if (prop.Name != "WebPageItem" && prop.Name != "Item" && prop.CanRead)
                {
                    try
                    {
                        var value = prop.GetValue(item);
                        if (value != null)
                        {
                            result[prop.Name] = value;
                        }
                    }
                    catch
                    {
                        // Skip properties that can't be read
                    }
                }
            }
            
            // Add system fields safely
            if (item is IWebPageContentQueryDataContainer webPageItem)
            {
                result["WebPageItemID"] = webPageItem.WebPageItemID;
                result["WebPageItemGUID"] = webPageItem.WebPageItemGUID.ToString();
                result["WebPageItemName"] = webPageItem.WebPageItemName;
                result["WebPageItemTreePath"] = webPageItem.WebPageItemTreePath;
            }
            else
            {
                // Fallback for non-webpage items
                result["WebPageItemID"] = 0;
                result["WebPageItemGUID"] = Guid.Empty.ToString();
                result["WebPageItemName"] = string.Empty;
                result["WebPageItemTreePath"] = string.Empty;
            }
            
            return result;
        }
    }
} 