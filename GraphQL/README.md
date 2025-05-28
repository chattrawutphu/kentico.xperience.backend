# Dynamic Content GraphQL Resolver for Kentico Xperience

This module provides a flexible GraphQL resolver that can query any content type in Kentico Xperience without requiring specific resolvers for each content type.

## Features

- Query any content type dynamically via GraphQL
- Filter by path in the content tree
- Apply pagination (limit/offset)
- Customize sorting (field and direction)
- Fully type-safe in frontend applications

## Implementation Note

This implementation provides a placeholder GraphQL resolver that demonstrates the structure for a dynamic content resolver. In a production environment, you'll need to:

1. Add the correct references to Kentico Xperience's DocumentEngine and related types
2. Uncomment the actual implementation in `DynamicContentResolver.cs`
3. Ensure you have the necessary permissions to query content

## Usage

### Backend Setup

The dynamic content resolver is automatically registered with the GraphQL schema during application startup. No additional configuration is required.

### Frontend Queries

You can query any content type using the `dynamicPages` query. Here are some examples:

#### Query Articles

```graphql
query GetArticles {
  dynamicPages(contentType: "Article", path: "/Articles") {
    system {
      id
      name
      codeName
    }
    elements {
      articleTitle {
        value
      }
      articlePageSummary {
        value
      }
    }
    url
  }
}
```

#### Query Products

```graphql
query GetProducts {
  dynamicPages(contentType: "Product") {
    system {
      id
      name
    }
    elements {
      name {
        value
      }
      price {
        value
      }
    }
    url
  }
}
```

#### Query with Pagination and Sorting

```graphql
query GetPaginatedContent {
  dynamicPages(
    contentType: "Article",
    limit: 10,
    offset: 0,
    orderBy: "DocumentPublishFrom",
    orderDirection: "desc"
  ) {
    system {
      id
      name
    }
    elements {
      articleTitle {
        value
      }
    }
    url
  }
}
```

## Available Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| contentType | String | Yes | The content type/document type to query (e.g., "Article", "Product") |
| path | String | No | Optional path in the content tree (e.g., "/Articles") |
| limit | Int | No | Maximum number of items to return |
| offset | Int | No | Number of items to skip |
| orderBy | String | No | Field to order by (defaults to "DocumentPublishFrom") |
| orderDirection | String | No | Order direction: "asc" or "desc" (default) |

## Next Steps

1. Integrate with your specific Kentico Xperience CMS version
2. Implement security and permission checks
3. Add caching for performance optimization
4. Create type definitions for TypeScript clients

## Important Notes

1. When querying, you need to specify the fields relevant to the content type you're querying. The system fields are common across all types, but the elements will vary.

2. For TypeScript applications, you may want to create type definitions for each content type to ensure type safety.

3. This resolver uses `DocumentHelper.GetDocuments()` internally, which returns all public fields of the content type. You can select specific fields in your GraphQL query. 