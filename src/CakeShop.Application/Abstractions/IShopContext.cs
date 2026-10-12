namespace CakeShop.Application.Abstractions;

/// <summary>Server-resolved shop scope. Never populate this from an untrusted request body.</summary>
public interface IShopContext
{
    Guid ClientId { get; }
}
