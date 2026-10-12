namespace CakeShop.Domain.Identity;

public sealed class AdminUser
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = "ShopAdmin";
    public bool IsActive { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    private AdminUser() { }

    public AdminUser(Guid id, Guid clientId, string email, string passwordHash, string role = "ShopAdmin")
    {
        if (id == Guid.Empty || clientId == Guid.Empty) throw new ArgumentException("User and shop IDs are required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("A valid email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (role is not ("ShopAdmin" or "ShopOwner")) throw new ArgumentException("Unsupported admin role.", nameof(role));
        Id = id;
        ClientId = clientId;
        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedUtc = DateTime.UtcNow;
    }
}
