namespace CakeShop.Domain.Content;

public sealed class HeroBanner
{
    private HeroBanner() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Subtitle { get; private set; } = string.Empty;
    public string TextPosition { get; private set; } = "left";
    public string MediaType { get; private set; } = "Image";
    public string MediaUrl { get; private set; } = string.Empty;
    public string? MobileMediaUrl { get; private set; }
    public string? ButtonText { get; private set; }
    public string? ButtonUrl { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime? StartsUtc { get; private set; }
    public DateTime? EndsUtc { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public HeroBanner(
        Guid clientId, string title, string subtitle, string mediaType, string mediaUrl,
        string? mobileMediaUrl = null, string? buttonText = null, string? buttonUrl = null,
        int sortOrder = 0, DateTime? startsUtc = null, DateTime? endsUtc = null, string textPosition = "left")
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId is required.", nameof(clientId));
        Id = Guid.NewGuid();
        ClientId = clientId;
        UpdateContent(title, subtitle, mediaType, mediaUrl, mobileMediaUrl, buttonText, buttonUrl, sortOrder, startsUtc, endsUtc, textPosition);
        IsPublished = false;
        CreatedUtc = DateTime.UtcNow;
        UpdatedUtc = CreatedUtc;
    }

    public void UpdateContent(
        string title, string subtitle, string mediaType, string mediaUrl,
        string? mobileMediaUrl, string? buttonText, string? buttonUrl,
        int sortOrder, DateTime? startsUtc, DateTime? endsUtc, string textPosition = "CenterLeft")
    {
        if ((title?.Trim().Length ?? 0) > 160)
            throw new ArgumentException("Title must be at most 160 characters.", nameof(title));
        if ((subtitle?.Trim().Length ?? 0) > 500)
            throw new ArgumentException("Subtitle must be at most 500 characters.", nameof(subtitle));
        if (textPosition is not ("top" or "bottom" or "left" or "right"))
            throw new ArgumentException("Invalid text position.", nameof(textPosition));
        if (mediaType is not ("Image" or "Video")) throw new ArgumentException("Media type must be Image or Video.", nameof(mediaType));
        if (string.IsNullOrWhiteSpace(mediaUrl) || mediaUrl.Trim().Length > 2000)
            throw new ArgumentException("Media URL is required and must be at most 2000 characters.", nameof(mediaUrl));
        if (startsUtc.HasValue && endsUtc.HasValue && endsUtc.Value <= startsUtc.Value)
            throw new ArgumentException("End date must be after start date.");
        if ((buttonText?.Length ?? 0) > 80 || (buttonUrl?.Length ?? 0) > 2000)
            throw new ArgumentException("Button text or URL exceeds its allowed length.");

        Title = title?.Trim() ?? string.Empty;
        Subtitle = subtitle?.Trim() ?? string.Empty;
        TextPosition = textPosition;
        MediaType = mediaType;
        MediaUrl = mediaUrl.Trim();
        MobileMediaUrl = string.IsNullOrWhiteSpace(mobileMediaUrl) ? null : mobileMediaUrl.Trim();
        ButtonText = string.IsNullOrWhiteSpace(buttonText) ? null : buttonText.Trim();
        ButtonUrl = string.IsNullOrWhiteSpace(buttonUrl) ? null : buttonUrl.Trim();
        SortOrder = sortOrder;
        StartsUtc = startsUtc;
        EndsUtc = endsUtc;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Publish() { IsPublished = true; UpdatedUtc = DateTime.UtcNow; }
    public void Unpublish() { IsPublished = false; UpdatedUtc = DateTime.UtcNow; }
}
