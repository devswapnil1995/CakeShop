namespace CakeShop.Application.Catalog;

public sealed record ShopDto(Guid Id, string Name, string Slug);
public sealed record CategoryDto(Guid Id, string Name, string Slug, int SortOrder);
public sealed record ProductDto(Guid Id, string Name, string Slug, string Description, decimal Price, string? ImageUrl, string? Badge, bool IsFeatured, string CategoryName, string CategorySlug);
