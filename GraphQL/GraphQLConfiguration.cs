using System.Collections.Generic;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

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
            // Register the resolvers
            services.AddSingleton<DynamicContentResolver>();
            services.AddSingleton<ArticlesResolver>();
            services.AddSingleton<PageContentResolver>();

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
    }
} 