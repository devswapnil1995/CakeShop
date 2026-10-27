namespace CakeShop.Domain.Content;

/// <summary>Editable public identity and contact information for one bakery tenant.</summary>
public sealed class StoreProfile
{
    private StoreProfile() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Tagline { get; private set; } = string.Empty;
    public string StoryTitle { get; private set; } = string.Empty;
    public string Story { get; private set; } = string.Empty;
    public string? StoryImageUrl { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? Phone { get; private set; }
    public string? WhatsAppNumber { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? OpeningHours { get; private set; }
    public string? InstagramUrl { get; private set; }
    public string? FacebookUrl { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public StoreProfile(Guid clientId)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId is required.", nameof(clientId));
        Id = Guid.NewGuid();
        ClientId = clientId;
        Tagline = "Baked with a little magic";
        StoryTitle = "Not just cake. A feeling.";
        Story = "We believe the sweetest memories are often the simplest ones. Every bake is made with care, creativity, and lots of heart.";
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Update(string? tagline, string? storyTitle, string? story, string? storyImageUrl,
        string? logoUrl, string? phone, string? whatsAppNumber, string? email, string? address,
        string? openingHours, string? instagramUrl, string? facebookUrl, string? websiteUrl)
    {
        Tagline = Required(tagline, 120, nameof(tagline));
        StoryTitle = Required(storyTitle, 180, nameof(storyTitle));
        Story = Required(story, 3000, nameof(story));
        StoryImageUrl = Optional(storyImageUrl, 1000, nameof(storyImageUrl));
        LogoUrl = Optional(logoUrl, 1000, nameof(logoUrl));
        Phone = Optional(phone, 40, nameof(phone));
        WhatsAppNumber = Optional(whatsAppNumber, 40, nameof(whatsAppNumber));
        Email = Optional(email, 254, nameof(email));
        Address = Optional(address, 500, nameof(address));
        OpeningHours = Optional(openingHours, 500, nameof(openingHours));
        InstagramUrl = Optional(instagramUrl, 500, nameof(instagramUrl));
        FacebookUrl = Optional(facebookUrl, 500, nameof(facebookUrl));
        WebsiteUrl = Optional(websiteUrl, 500, nameof(websiteUrl));
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
