namespace CakeShop.Domain.Tenants;

public sealed class ClientMaster
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    private ClientMaster() { }

    public ClientMaster(Guid id, string name, string slug)
    {
        if (id == Guid.Empty) throw new ArgumentException("Client ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Shop name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Shop slug is required.", nameof(slug));
        Id = id;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        IsActive = true;
        CreatedUtc = DateTime.UtcNow;
    }
}
