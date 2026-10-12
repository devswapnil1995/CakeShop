namespace CakeShop.Domain.Catalog;

public sealed class Category
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    private Category() { }

    public void Update(string name, string slug, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Category name and slug are required.");
        Name = name.Trim(); Slug = slug.Trim().ToLowerInvariant(); SortOrder = sortOrder;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
    public Category(Guid id, Guid clientId, string name, string slug, int sortOrder = 0)
    {
        if (id == Guid.Empty || clientId == Guid.Empty) throw new ArgumentException("IDs are required.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Category name and slug are required.");
        Id = id; ClientId = clientId; Name = name.Trim(); Slug = slug.Trim().ToLowerInvariant(); SortOrder = sortOrder; IsActive = true;
    }
}
