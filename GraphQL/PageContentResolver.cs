using System.Collections.Generic;
using System.Linq;
using HotChocolate;
using HotChocolate.Types;

namespace Kentico.Xperience.Backend.GraphQL
{
    /// <summary>
    /// Flexible GraphQL resolver for page content
    /// </summary>
    [ExtendObjectType("Query")]
    public class PageContentResolver
    {
        /// <summary>
        /// Gets page data by path
        /// </summary>
        /// <param name="path">The path in the content tree</param>
        /// <param name="contentType">Optional content type filter</param>
        /// <returns>Page data</returns>
        [GraphQLName("getPage")]
        public PageModel GetPage(string path, string contentType = null)
        {
            // This is a placeholder implementation
            // In a real implementation, you would query the CMS for the specific page at this path
            
            return new PageModel
            {
                Id = "page-1",
                Name = $"Page at {path}",
                ContentType = contentType ?? "Page",
                Path = path,
                Fields = new Dictionary<string, object>
                {
                    { "Title", $"Page at {path}" },
                    { "Description", "This is a sample page description" },
                    { "Content", "<p>This is the main content of the page.</p>" }
                }
            };
        }

        /// <summary>
        /// Gets child pages at the specified path
        /// </summary>
        /// <param name="path">The path in the content tree</param>
        /// <param name="contentType">Optional content type filter</param>
        /// <param name="limit">Maximum number of items to return</param>
        /// <param name="offset">Number of items to skip</param>
        /// <param name="orderBy">Field to order by</param>
        /// <param name="orderDirection">Order direction ("asc" or "desc")</param>
        /// <returns>List of child page data</returns>
        [GraphQLName("getChildPages")]
        public List<PageModel> GetChildPages(
            string path, 
            string contentType = null, 
            int? limit = null, 
            int? offset = null, 
            string orderBy = "Name", 
            string orderDirection = "asc")
        {
            // This is a placeholder implementation
            // In a real implementation, you would query the CMS for children of the specified path
            
            var result = new List<PageModel>();
            
            // Create sample child pages
            for (int i = 1; i <= 5; i++)
            {
                var childPath = $"{path.TrimEnd('/')}/child-{i}";
                
                result.Add(new PageModel
                {
                    Id = $"child-{i}",
                    Name = $"Child {i} at {path}",
                    ContentType = contentType ?? "Page",
                    Path = childPath,
                    Fields = new Dictionary<string, object>
                    {
                        { "Title", $"Child {i} at {path}" },
                        { "Description", $"This is child page {i}" },
                        { "Content", $"<p>This is the content of child page {i}.</p>" }
                    }
                });
            }
            
            // Apply sorting
            if (!string.IsNullOrEmpty(orderBy))
            {
                if (orderBy == "Name")
                {
                    result = orderDirection?.ToLowerInvariant() == "asc" 
                        ? result.OrderBy(p => p.Name).ToList()
                        : result.OrderByDescending(p => p.Name).ToList();
                }
                else if (orderBy == "Path")
                {
                    result = orderDirection?.ToLowerInvariant() == "asc" 
                        ? result.OrderBy(p => p.Path).ToList()
                        : result.OrderByDescending(p => p.Path).ToList();
                }
            }
            
            // Apply pagination
            if (offset.HasValue && offset.Value > 0)
            {
                result = result.Skip(offset.Value).ToList();
            }
            
            if (limit.HasValue && limit.Value > 0)
            {
                result = result.Take(limit.Value).ToList();
            }
            
            return result;
        }
    }

    /// <summary>
    /// Represents a generic page model
    /// </summary>
    public class PageModel
    {
        [GraphQLName("id")]
        public string Id { get; set; }
        
        [GraphQLName("name")]
        public string Name { get; set; }
        
        [GraphQLName("contentType")]
        public string ContentType { get; set; }
        
        [GraphQLName("path")]
        public string Path { get; set; }
        
        [GraphQLName("fields")]
        public Dictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
        
        [GraphQLName("url")]
        public string Url => $"/{ContentType.ToLowerInvariant()}/{Id}";
    }
} 