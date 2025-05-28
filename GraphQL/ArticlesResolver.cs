using System.Collections.Generic;
using System.Linq;
using HotChocolate;
using HotChocolate.Types;

namespace Kentico.Xperience.Backend.GraphQL
{
    /// <summary>
    /// GraphQL resolver for article-related queries
    /// </summary>
    [ExtendObjectType("Query")]
    public class ArticlesResolver
    {
        /// <summary>
        /// Gets all articles from the specified path
        /// </summary>
        /// <param name="path">The path in the content tree (default: "/Articles")</param>
        /// <returns>List of article page data</returns>
        [GraphQLName("getArticles")]
        public List<ArticlePageModel> GetArticles(string path = "/Articles")
        {
            // This is a placeholder implementation
            // In a real implementation, you would query the CMS for article pages
            // For now, we'll return static data

            return new List<ArticlePageModel>
            {
                new ArticlePageModel
                {
                    Id = "1",
                    Title = "Getting Started with Kentico Xperience",
                    Summary = "Learn the basics of Kentico Xperience and how to build your first website",
                    Text = "<p>Kentico Xperience is a powerful CMS platform that allows you to build dynamic websites with ease.</p>",
                    PublishDate = "2023-05-15",
                    Teaser = "Explore the fundamentals of Kentico Xperience"
                },
                new ArticlePageModel
                {
                    Id = "2",
                    Title = "Advanced GraphQL with Kentico",
                    Summary = "Deep dive into using GraphQL with Kentico Xperience",
                    Text = "<p>GraphQL provides a flexible API layer for your Kentico Xperience websites.</p>",
                    PublishDate = "2023-06-22",
                    Teaser = "Master GraphQL queries in Kentico"
                },
                new ArticlePageModel
                {
                    Id = "3",
                    Title = "Building Next.js Applications with Kentico",
                    Summary = "Step-by-step guide to integrating Next.js with Kentico Xperience",
                    Text = "<p>Next.js is a powerful React framework that works great with Kentico Xperience headless CMS.</p>",
                    PublishDate = "2023-07-10",
                    Teaser = "Create modern web applications with Next.js and Kentico"
                }
            };
        }

        /// <summary>
        /// Gets a specific article by ID
        /// </summary>
        /// <param name="id">The article ID</param>
        /// <returns>Article page data</returns>
        [GraphQLName("getArticleById")]
        public ArticlePageModel GetArticleById(string id)
        {
            // This is a placeholder implementation
            var articles = GetArticles();
            return articles.FirstOrDefault(a => a.Id == id) ?? new ArticlePageModel();
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
        public string Url => $"/articles/{Id}";
    }
} 