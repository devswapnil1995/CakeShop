namespace CakeShop.Domain.Content;

/// <summary>A tenant-owned content block displayed on the public About page.</summary>
public sealed class AboutPageSection
{
    private AboutPageSection() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Eyebrow { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public string? SecondaryImageUrl { get; private set; }
    public string Layout { get; private set; } = "image-left";
    public string Animation { get; private set; } = "fade-up";
    public int SortOrder { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public AboutPageSection(Guid clientId, string? title, string? body)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId is required.", nameof(clientId));
        Id = Guid.NewGuid();
        ClientId = clientId;
        Update("OUR STORY", title, body, null, null, "image-left", "fade-up", 0, false);
    }

    public void Update(string? eyebrow, string? title, string? body, string? imageUrl, string? secondaryImageUrl,
        string? layout, string? animation, int sortOrder, bool isPublished)
    {
        Eyebrow = Required(eyebrow, 100, nameof(eyebrow));
        Title = Required(title, 180, nameof(title));
        Body = Required(body, 3000, nameof(body));
        ImageUrl = Optional(imageUrl, 1000, nameof(imageUrl));
        SecondaryImageUrl = Optional(secondaryImageUrl, 1000, nameof(secondaryImageUrl));
        Layout = layout is "image-left" or "image-right" or "centered" or "full-bleed" or "editorial" ? layout : "image-left";
        Animation = animation is "none" or "fade-up" or "fade-in" or "slide-left" or "slide-right" or "zoom-in" ? animation : "fade-up";
        SortOrder = Math.Clamp(sortOrder, 0, 10000);
        IsPublished = isPublished;
        UpdatedUtc = DateTime.UtcNow;
    }

    private static string Required(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ArgumentException($"Value is required and must be at most {max} characters.", name);
        return value.Trim();
    }

    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Trim().Length > max) throw new ArgumentException($"Value must be at most {max} characters.", name);
        return value.Trim();
    }
}
