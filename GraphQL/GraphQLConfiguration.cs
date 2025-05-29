using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using DancingGoat.Models;
using CMS.Websites.Routing;
using CMS.ContentEngine;
using CMS.Helpers;
using CMS.Websites;
using Kentico.Content.Web.Mvc.Routing;
using CMS.Core;

namespace Kentico.Xperience.Backend.GraphQL
{
    /// <summary>
    /// Provides GraphQL configuration for the application.
    /// </summary>
    public static class GraphQLConfiguration
    {
        /// <summary>
        /// Registers GraphQL services including the dynamic content resolver.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection with GraphQL services registered.</returns>
        public static IServiceCollection AddCustomGraphQLServices(this IServiceCollection services)
        {
            // Register content repositories
            services.AddSingleton<ArticlePageRepository>();
            
            // Register the GraphQL resolvers
            services.AddScoped<DynamicContentResolver>();
            services.AddScoped<ArticlesResolver>();
            services.AddScoped<PageContentResolver>();

            // Register the GraphQL schema
            services.AddGraphQLServer()
                .AddQueryType(d => d.Name("Query"))
                .AddTypeExtension<DynamicContentResolverExtension>()
                .AddTypeExtension<ArticlesResolver>()
                .AddTypeExtension<PageContentResolver>();

            return services;
        }
    }

    /// <summary>
    /// Extension type for registering the dynamic content resolver with the GraphQL schema.
    /// </summary>
    [ExtendObjectType("Query")]
    public class DynamicContentResolverExtension
    {
        /// <summary>
        /// Adds the dynamicPages field to the Query type.
        /// </summary>
        /// <param name="resolver">The dynamic content resolver.</param>
        /// <param name="contentType">The content type/document type to query.</param>
        /// <param name="path">Optional path in the content tree.</param>
        /// <param name="limit">Maximum number of items to return.</param>
        /// <param name="offset">Number of items to skip.</param>
        /// <param name="orderBy">Field to order by.</param>
        /// <param name="orderDirection">Order direction ("asc" or "desc").</param>
        /// <returns>The result of the dynamic content query.</returns>
        public object DynamicPages(
            [Service] DynamicContentResolver resolver,
            string contentType,
            string path = null,
            int? limit = null,
            int? offset = null,
            string orderBy = "DocumentPublishFrom",
            string orderDirection = "desc")
        {
            return resolver.GetDynamicPages(contentType, path, limit, offset, orderBy, orderDirection);
        }

        /// <summary>
        /// Gets dynamic content with flexible parameters (new method for frontend compatibility)
        /// </summary>
        /// <param name="resolver">The dynamic content resolver.</param>
        /// <param name="path">Optional path in the content tree.</param>
        /// <param name="language">The language to use for content retrieval.</param>
        /// <param name="typeContentPath">Type of content retrieval - "All child pages" or "Only this page".</param>
        /// <param name="contentType">The content type/document type to query.</param>
        /// <param name="maximumNestingLevel">The maximum nesting level for recursive retrieval.</param>
        /// <param name="orderBy">Field to order by.</param>
        /// <param name="selectTopNPages">Maximum number of items to return.</param>
        /// <param name="whereCondition">Optional filtering condition.</param>
        /// <returns>The result of the dynamic content query.</returns>
        [GraphQLName("getDynamicContent")]
        public async Task<List<DynamicContentItem>> GetDynamicContent(
            [Service] DynamicContentResolver resolver,
            string path = "/",
            string language = null,
            string typeContentPath = "All child pages",
            string contentType = null,
            int? maximumNestingLevel = null,
            string orderBy = null,
            int? selectTopNPages = null,
            string whereCondition = null)
        {
            // Call the async method from DynamicContentResolver
            var result = await resolver.GetDynamicContentAsync(
                path: path,
                language: language,
                typeContentPath: typeContentPath,
                contentType: contentType,
                maximumNestingLevel: maximumNestingLevel ?? -1,
                orderBy: orderBy,
                selectTopNPages: selectTopNPages ?? 0,
                whereCondition: whereCondition
            );

            // Convert to list of DynamicContentItem
            return result.Select(dict => new DynamicContentItem(dict)).ToList();
        }
    }

    /// <summary>
    /// Represents a dynamic content item from the CMS
    /// </summary>
    public class DynamicContentItem
    {
        private readonly Dictionary<string, object> _data;

        public DynamicContentItem(Dictionary<string, object> data)
        {
            _data = data ?? new Dictionary<string, object>();
        }

        // Web page fields
        [GraphQLName("WebPageItemID")]
        public int? WebPageItemID => GetValue<int?>("WebPageItemID");

        [GraphQLName("WebPageItemGUID")]
        public string WebPageItemGUID => GetValue<string>("WebPageItemGUID");

        [GraphQLName("WebPageItemName")]
        public string WebPageItemName => GetValue<string>("WebPageItemName");

        [GraphQLName("WebPageItemTreePath")]
        public string WebPageItemTreePath => GetValue<string>("WebPageItemTreePath");

        [GraphQLName("WebPageItemLevel")]
        public int? WebPageItemLevel => GetValue<int?>("WebPageItemLevel");

        // Article fields
        [GraphQLName("ArticleTitle")]
        public string ArticleTitle => GetValue<string>("ArticleTitle");

        [GraphQLName("ArticlePageText")]
        public string ArticlePageText => GetValue<string>("ArticlePageText");

        [GraphQLName("ArticlePageSummary")]
        public string ArticlePageSummary => GetValue<string>("ArticlePageSummary");

        [GraphQLName("ArticlePagePublishDate")]
        public string ArticlePagePublishDate 
        {
            get
            {
                var date = GetValue<DateTime?>("ArticlePagePublishDate");
                return date?.ToString("yyyy-MM-dd");
            }
        }

        // Generic fields
        [GraphQLName("Title")]
        public string Title => GetValue<string>("Title");

        [GraphQLName("Subtitle")]
        public string Subtitle => GetValue<string>("Subtitle");

        [GraphQLName("Content")]
        public string Content => GetValue<string>("Content");

        [GraphQLName("Image")]
        public string Image => GetValue<string>("Image");

        private T GetValue<T>(string key)
        {
            if (_data.ContainsKey(key) && _data[key] != null)
            {
                var value = _data[key];
                if (value is T typedValue)
                    return typedValue;
                
                // Special handling for nullable types
                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                
                if (underlyingType != null)
                {
                    // It's a nullable type
                    if (value == null)
                        return default(T); // returns null for nullable types
                    
                    try
                    {
                        var convertedValue = Convert.ChangeType(value, underlyingType);
                        return (T)convertedValue;
                    }
                    catch
                    {
                        return default(T); // returns null for nullable types
                    }
                }
                else
                {
                    // Non-nullable type
                    try
                    {
                        return (T)Convert.ChangeType(value, targetType);
                    }
                    catch
                    {
                        // For reference types (like string), return null
                        // For value types, return default
                        return default(T);
                    }
                }
            }
            
            return default(T); // returns null for reference types and nullable value types
        }
    }
} 