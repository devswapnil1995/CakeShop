namespace CakeShop.Domain.Content;

/// <summary>
/// Tenant-owned storefront collection, used for gift hampers and date-bound seasonal campaigns.
/// </summary>
public sealed class StoreCollection
{
    private StoreCollection() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Kind { get; private set; } = "Seasonal";
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public string? NavigationLabel { get; private set; }
    public bool ShowInNavigation { get; private set; }
    public bool IsPublished { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime? StartsUtc { get; private set; }
    public DateTime? EndsUtc { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public StoreCollection(Guid clientId, string kind, string title, string slug, string description,
        string? imageUrl, string? navigationLabel, bool showInNavigation, int sortOrder,
        DateTime? startsUtc, DateTime? endsUtc)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId is required.", nameof(clientId));
        Id = Guid.NewGuid();
        ClientId = clientId;
        Update(kind, title, slug, description, imageUrl, navigationLabel, showInNavigation, sortOrder, startsUtc, endsUtc);
        CreatedUtc = UpdatedUtc = DateTime.UtcNow;
    }

    public void Update(string kind, string title, string slug, string description, string? imageUrl,
        string? navigationLabel, bool showInNavigation, int sortOrder, DateTime? startsUtc, DateTime? endsUtc)
    {
        if (kind is not ("Hamper" or "Seasonal")) throw new ArgumentException("Kind must be Hamper or Seasonal.", nameof(kind));
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160) throw new ArgumentException("Title is required and must be at most 160 characters.");
        if (string.IsNullOrWhiteSpace(slug) || slug.Trim().Length > 180) throw new ArgumentException("Slug is required and must be at most 180 characters.");
        if (description?.Trim().Length > 2000) throw new ArgumentException("Description must be at most 2000 characters.");
        if (imageUrl?.Length > 1000 || navigationLabel?.Trim().Length > 60) throw new ArgumentException("Image URL or navigation label is too long.");
        if (sortOrder < 0 || sortOrder > 10000) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        if (startsUtc.HasValue && endsUtc.HasValue && endsUtc <= startsUtc) throw new ArgumentException("End date must be after start date.");
        Kind = kind; Title = title.Trim(); Slug = slug.Trim().ToLowerInvariant(); Description = description?.Trim() ?? string.Empty;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        NavigationLabel = string.IsNullOrWhiteSpace(navigationLabel) ? null : navigationLabel.Trim();
        ShowInNavigation = showInNavigation; SortOrder = sortOrder; StartsUtc = startsUtc; EndsUtc = endsUtc;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetPublished(bool value) { IsPublished = value; UpdatedUtc = DateTime.UtcNow; }
}
