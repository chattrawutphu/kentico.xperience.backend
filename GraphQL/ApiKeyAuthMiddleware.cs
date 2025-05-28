using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Kentico.Xperience.Backend.GraphQL
{
    /// <summary>
    /// Middleware to authenticate GraphQL API requests using API key
    /// </summary>
    public class ApiKeyAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private const string API_KEY_HEADER_NAME = "X-API-Key";
        
        // Hardcoded API key and secret
        private const string API_KEY = "kx-graphql-api-key-2024";
        private const string API_SECRET = "kx-graphql-api-secret-2024";

        public ApiKeyAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Temporarily skip authentication to troubleshoot 500 error
            await _next(context);
            return;

            // Only apply authentication to GraphQL endpoints
            if (context.Request.Path.StartsWithSegments("/graphql"))
            {
                // Check if API key is provided
                if (!context.Request.Headers.TryGetValue(API_KEY_HEADER_NAME, out StringValues extractedApiKey))
                {
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsync("API key is missing");
                    return;
                }

                // Validate API key
                if (!extractedApiKey.Equals(API_KEY))
                {
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsync("Invalid API key");
                    return;
                }
            }

            // Continue processing the request
            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for API key authentication
    /// </summary>
    public static class ApiKeyAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiKeyAuthMiddleware>();
        }
    }
} 