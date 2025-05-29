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
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Set a default timeout to ensure responsiveness
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                
                // Use default language if not specified
                language = language ?? "en-US";
                
                _logger?.LogInformation($"Fetching content with parameters: Path={path}, Language={language}, ContentType={contentType}, TypeContentPath={typeContentPath}");
                
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
                        
                        // Apply limit
                        if (selectTopNPages > 0)
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
                
                // Execute the query with caching
                var cacheSettings = new CacheSettings(5, _websiteChannelContext.WebsiteChannelName, "DynamicContent", path, language, contentType);
                
                var result = await GetCachedQueryResultAsync(
                    queryBuilder, 
                    options,
                    cacheSettings, 
                    linkedCts.Token);
                
                _logger?.LogInformation($"Found {result.Count()} items for query");
                
                // Convert to dictionaries for GraphQL
                return result.Select(ConvertToDictionary).ToList();
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
                selectTopNPages: limit ?? 0
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
                        _logger?.LogWarning($"Greater than condition converted to equals: {whereCondition}");
                        var field = parts[0].Trim();
                        var value = parts[1].Trim().Trim('"', '\'');
                        parameters.Where(where => where.WhereEquals(field, value));
                    }
                }
                else if (whereCondition.Contains("<"))
                {
                    var parts = whereCondition.Split('<');
                    if (parts.Length == 2)
                    {
                        _logger?.LogWarning($"Less than condition converted to equals: {whereCondition}");
                        var field = parts[0].Trim();
                        var value = parts[1].Trim().Trim('"', '\'');
                        parameters.Where(where => where.WhereEquals(field, value));
                    }
                }
                else if (whereCondition.Contains("Contains"))
                {
                    var parts = whereCondition.Split(new[] { "Contains" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var field = parts[0].Trim();
                        var value = parts[1].Trim().Trim('"', '\'');
                        
                        parameters.Where(where => where.WhereContains(field, value));
                    }
                }
                else
                {
                    _logger?.LogWarning($"Unsupported where condition: {whereCondition}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error applying where condition: {whereCondition}");
            }
        }
        
        /// <summary>
        /// Executes a query with caching
        /// </summary>
        private async Task<IEnumerable<IContentQueryDataContainer>> GetCachedQueryResultAsync(
            ContentItemQueryBuilder queryBuilder,
            ContentQueryExecutionOptions options,
            CacheSettings cacheSettings,
            CancellationToken cancellationToken)
        {
            return await _progressiveCache.LoadAsync(async (cs, ct) =>
            {
                var result = await _contentQueryExecutor.GetResult<IContentQueryDataContainer>(
                    queryBuilder, 
                    (IContentQueryDataContainer item) => item, // Identity mapping function
                    options, 
                    ct);
                
                if (cs.Cached)
                {
                    cs.CacheDependency = CacheHelper.GetCacheDependency(new[]
                    {
                        $"{_websiteChannelContext.WebsiteChannelName}|contentitems"
                    });
                }
                
                return result;
            }, cacheSettings, cancellationToken);
        }
        
        /// <summary>
        /// Converts a content item to a dictionary for GraphQL
        /// </summary>
        private Dictionary<string, object> ConvertToDictionary(IContentQueryDataContainer item)
        {
            var result = new Dictionary<string, object>();
            
            // Add basic properties from content item data
            // This safely handles the case where ContentItemData might not be accessible
            if (item != null)
            {
                try
                {
                    // Use reflection to try to get content item data
                    var properties = item.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        if (prop.Name != "Item" && prop.CanRead)
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
                                // Ignore properties that can't be read
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Error extracting properties: {ex.Message}");
                }
            }
            
            // Add system fields for web pages
            if (item is IWebPageContentQueryDataContainer webPageItem)
            {
                result["WebPageItemID"] = webPageItem.WebPageItemID;
                result["WebPageItemGUID"] = webPageItem.WebPageItemGUID;
                result["WebPageItemName"] = webPageItem.WebPageItemName;
                result["WebPageItemOrder"] = webPageItem.WebPageItemOrder;
                result["WebPageItemParentID"] = webPageItem.WebPageItemParentID;
                result["WebPageItemTreePath"] = webPageItem.WebPageItemTreePath;
                
                // Safely try to add WebPageItemLevel if it exists
                try
                {
                    var levelProp = webPageItem.GetType().GetProperty("WebPageItemLevel");
                    if (levelProp != null)
                    {
                        var level = levelProp.GetValue(webPageItem);
                        result["WebPageItemLevel"] = level;
                    }
                }
                catch
                {
                    // Level property not available
                }
            }
            
            return result;
        }
    }
} 