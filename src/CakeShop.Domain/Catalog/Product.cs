namespace CakeShop.Domain.Catalog;

public sealed class Product
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? Badge { get; private set; }
    public bool IsAvailable { get; private set; }
    public bool IsFeatured { get; private set; }
    private Product() { }

    public void Update(Guid categoryId, string name, string slug, string description, decimal price, string? imageUrl, string? badge, bool isFeatured, bool isAvailable)
    {
        if (categoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(categoryId));
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Product name and slug are required.");
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        if (imageUrl is { Length: > 1000 }) throw new ArgumentException("Image URL is too long.", nameof(imageUrl));
        CategoryId = categoryId; Name = name.Trim(); Slug = slug.Trim().ToLowerInvariant();
        Description = description?.Trim() ?? string.Empty; Price = price; ImageUrl = imageUrl?.Trim();
        Badge = badge?.Trim(); IsFeatured = isFeatured; IsAvailable = isAvailable;
    }
    public Product(Guid id, Guid clientId, Guid categoryId, string name, string slug, string description, decimal price, string? imageUrl, string? badge, bool isFeatured = false, bool isAvailable = true)
    {
        if (id == Guid.Empty || clientId == Guid.Empty || categoryId == Guid.Empty) throw new ArgumentException("IDs are required.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Product name and slug are required.");
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        Name = name.Trim(); Slug = slug.Trim().ToLowerInvariant(); Description = description?.Trim() ?? string.Empty;
        Id = id; ClientId = clientId; CategoryId = categoryId; Price = price; ImageUrl = imageUrl; Badge = badge; IsFeatured = isFeatured; IsAvailable = isAvailable;
    }
}
