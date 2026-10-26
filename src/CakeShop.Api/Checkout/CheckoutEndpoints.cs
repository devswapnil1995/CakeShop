using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using CakeShop.Domain.Orders;
using CakeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CakeShop.Api.Checkout;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder endpoints, Guid clientId)
    {
        var group = endpoints.MapGroup("/api/v1/checkout").AllowAnonymous().WithTags("Checkout");

        group.MapPost("/otp/request", (
            OtpRequest request,
            CheckoutOtpService otp,
            IConfiguration configuration,
            IWebHostEnvironment environment) =>
        {
            if (!IsTestMode(configuration, environment))
                return Results.Problem(
                    title: "SMS verification is not enabled",
                    detail: "MSG91 delivery is intentionally disabled until production activation is approved.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            var phone = NormalizePhone(request.Phone);
            if (phone is null)
                return Results.BadRequest(new { message = "Enter a valid mobile number with 10 to 15 digits." });

            var result = otp.RequestCode(phone);
            if (!result.Success)
                return Results.Json(new { message = result.Message, retryAfterSeconds = result.RetryAfterSeconds }, statusCode: StatusCodes.Status429TooManyRequests);

            // The test code is only returned by the local Development environment.
            return Results.Ok(new
            {
                message = "Test OTP generated. No SMS was sent and no provider was charged.",
                testCode = result.TestCode,
                expiresInSeconds = 300,
                retryAfterSeconds = 30
            });
        });

        group.MapPost("/otp/verify", (
            OtpVerifyRequest request,
            CheckoutOtpService otp,
            IConfiguration configuration,
            IWebHostEnvironment environment) =>
        {
            if (!IsTestMode(configuration, environment))
                return Results.Problem(
                    title: "SMS verification is not enabled",
                    detail: "MSG91 delivery is intentionally disabled until production activation is approved.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            var phone = NormalizePhone(request.Phone);
            if (phone is null || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 6)
                return Results.BadRequest(new { message = "Enter the 6-digit OTP sent to your mobile number." });

            var result = otp.VerifyCode(phone, request.Code);
            if (!result.Success)
                return Results.BadRequest(new { message = result.Message });

            return Results.Ok(new
            {
                verified = true,
                verificationToken = result.VerificationToken,
                expiresInSeconds = 600,
                message = "Mobile number verified for checkout."
            });
        });

        group.MapPost("/orders", async (
            CartCheckoutRequest request,
            CheckoutOtpService otp,
            CakeShopDbContext db,
            CancellationToken ct) =>
        {
            var customerName = request.CustomerName?.Trim();
            var phone = NormalizePhone(request.CustomerPhone);
            var customization = request.Customization?.Trim();

            if (string.IsNullOrWhiteSpace(customerName) || customerName.Length > 120)
                return Results.BadRequest(new { message = "Customer name is required (maximum 120 characters)." });
            if (phone is null || string.IsNullOrWhiteSpace(request.VerificationToken))
                return Results.BadRequest(new { message = "Verify your mobile number before placing the order." });
            if (request.Items is null || request.Items.Count is < 1 or > 50)
                return Results.BadRequest(new { message = "Your cart must contain between 1 and 50 different items." });
            if (customization?.Length > 1000)
                return Results.BadRequest(new { message = "Order notes must be at most 1000 characters." });

            var verifiedPhone = otp.GetVerifiedPhone(request.VerificationToken);
            if (verifiedPhone is null || !string.Equals(verifiedPhone, phone, StringComparison.Ordinal))
                return Results.BadRequest(new { message = "Verify this mobile number again before placing the order." });

            var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
            var indiaToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone));
            if (request.PreferredDate.HasValue && request.PreferredDate.Value < indiaToday)
                return Results.BadRequest(new { message = "Preferred date cannot be in the past." });

            var mergedItems = request.Items
                .GroupBy(item => new { item.ProductId, item.VariantId })
                .Select(group => new CartCheckoutItemRequest(group.Key.ProductId, group.Key.VariantId, group.Sum(x => x.Quantity)))
                .ToList();

            if (mergedItems.Any(item => item.ProductId == Guid.Empty || item.VariantId == Guid.Empty || item.Quantity is < 1 or > 100))
                return Results.BadRequest(new { message = "Each cart item needs a valid product, available size and quantity from 1 to 100." });

            var lines = new List<ResolvedCartLine>();
            foreach (var item in mergedItems)
            {
                var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(
                    x => x.Id == item.ProductId && x.ClientId == clientId && x.IsAvailable, ct);
                if (product is null)
                    return Results.Conflict(new { message = "A product in your cart is no longer available. Refresh your cart and try again." });

                var categoryIsActive = await db.Categories.AsNoTracking().AnyAsync(
                    x => x.Id == product.CategoryId && x.ClientId == clientId && x.IsActive, ct);
                if (!categoryIsActive)
                    return Results.Conflict(new { message = "A product category is no longer available. Refresh your cart and try again." });

                var variant = await db.ProductVariants.AsNoTracking().SingleOrDefaultAsync(
                    x => x.Id == item.VariantId && x.ProductId == product.Id && x.ClientId == clientId && x.IsAvailable, ct);
                if (variant is null)
                    return Results.Conflict(new { message = $"The selected size for {product.Name} is no longer available. Refresh your cart and try again." });

                var lineTotal = decimal.Round(variant.Price * item.Quantity, 2, MidpointRounding.AwayFromZero);
                lines.Add(new ResolvedCartLine(product.Id, variant.Id, product.Name, variant.SizeLabel, variant.WeightInGrams, item.Quantity, variant.Price, lineTotal));
            }

            var totalAmount = decimal.Round(lines.Sum(x => x.LineTotal), 2, MidpointRounding.AwayFromZero);
            var totalQuantity = lines.Sum(x => x.Quantity);
            if (totalAmount <= 0 || totalAmount > 10000000m)
                return Results.BadRequest(new { message = "Cart total is outside the supported checkout limit." });

            // Consume the verification token once, after all validation but before order persistence.
            if (!otp.TryConsumeVerificationToken(request.VerificationToken, phone))
                return Results.BadRequest(new { message = "Your mobile verification expired or was already used. Verify your number again." });

            var first = lines[0];
            var orderId = Guid.NewGuid();
            var orderNumber = $"SP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var order = new CustomerOrder(
                orderId, clientId, orderNumber, customerName, phone,
                first.ProductId, first.ProductName, first.Quantity, first.SizeLabel,
                request.PreferredDate, customization, first.UnitPrice);
            order.SetCartSummary(lines.Count, totalQuantity, totalAmount);

            db.CustomerOrders.Add(order);
            foreach (var line in lines)
            {
                db.CustomerOrderItems.Add(new CakeShop.Domain.Orders.CustomerOrderItem(
                    Guid.NewGuid(), clientId, orderId, line.ProductId, line.VariantId,
                    line.ProductName, line.SizeLabel, line.WeightInGrams, line.Quantity, line.UnitPrice));
            }

            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/orders/{order.Id}", new
            {
                order.Id,
                order.OrderNumber,
                order.CakeName,
                order.Quantity,
                order.UnitPrice,
                order.TotalAmount,
                Status = order.Status.ToString(),
                order.CreatedUtc,
                Items = lines.Select(x => new { x.ProductName, x.SizeLabel, x.Quantity, x.UnitPrice, x.LineTotal }),
                Message = "Your cart order has been saved successfully."
            });
        });

        return endpoints;
    }

    private static bool IsTestMode(IConfiguration configuration, IWebHostEnvironment environment) =>
        environment.IsDevelopment() &&
        string.Equals(configuration["Otp:Mode"], "Test", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(configuration["Otp:Provider"], "MSG91", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length is >= 10 and <= 15 ? digits : null;
    }

    private sealed record ResolvedCartLine(
        Guid ProductId, Guid VariantId, string ProductName, string SizeLabel,
        int WeightInGrams, int Quantity, decimal UnitPrice, decimal LineTotal);
}

public sealed record OtpRequest(string? Phone);
public sealed record OtpVerifyRequest(string? Phone, string? Code);
public sealed record CartCheckoutItemRequest(Guid ProductId, Guid VariantId, int Quantity);
public sealed record CartCheckoutRequest(
    string? CustomerName,
    string? CustomerPhone,
    string? VerificationToken,
    List<CartCheckoutItemRequest>? Items,
    DateOnly? PreferredDate,
    string? Customization);

public sealed class CheckoutOtpService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OtpChallenge> _challenges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VerifiedPhone> _verifiedTokens = new(StringComparer.Ordinal);

    public OtpRequestResult RequestCode(string phone)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (_challenges.TryGetValue(phone, out var existing) && existing.CreatedUtc.AddSeconds(30) > now)
                return new(false, null, "Please wait before requesting another OTP.", (int)Math.Ceiling((existing.CreatedUtc.AddSeconds(30) - now).TotalSeconds));

            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _challenges[phone] = new OtpChallenge(Hash(code), now, now.AddMinutes(5), 0);
            return new(true, code, string.Empty, 0);
        }
    }

    public OtpVerifyResult VerifyCode(string phone, string code)
    {
        lock (_gate)
        {
            if (!_challenges.TryGetValue(phone, out var challenge))
                return new(false, null, "Request a new OTP first.");
            if (challenge.ExpiresUtc <= DateTimeOffset.UtcNow)
            {
                _challenges.Remove(phone);
                return new(false, null, "OTP expired. Request a new code.");
            }
            if (challenge.Attempts >= 5)
            {
                _challenges.Remove(phone);
                return new(false, null, "Too many incorrect attempts. Request a new OTP.");
            }

            challenge.Attempts++;
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(challenge.CodeHash), Convert.FromHexString(Hash(code))))
                return new(false, null, "Incorrect OTP. Please try again.");

            _challenges.Remove(phone);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _verifiedTokens[token] = new VerifiedPhone(phone, DateTimeOffset.UtcNow.AddMinutes(10));
            return new(true, token, string.Empty);
        }
    }

    public string? GetVerifiedPhone(string token)
    {
        lock (_gate)
        {
            if (!_verifiedTokens.TryGetValue(token, out var verified)) return null;
            if (verified.ExpiresUtc <= DateTimeOffset.UtcNow)
            {
                _verifiedTokens.Remove(token);
                return null;
            }
            return verified.Phone;
        }
    }

    public bool TryConsumeVerificationToken(string token, string phone)
    {
        lock (_gate)
        {
            if (!_verifiedTokens.TryGetValue(token, out var verified) ||
                verified.ExpiresUtc <= DateTimeOffset.UtcNow ||
                !string.Equals(verified.Phone, phone, StringComparison.Ordinal))
            {
                _verifiedTokens.Remove(token);
                return false;
            }
            _verifiedTokens.Remove(token);
            return true;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed class OtpChallenge(string codeHash, DateTimeOffset createdUtc, DateTimeOffset expiresUtc, int attempts)
    {
        public string CodeHash { get; } = codeHash;
        public DateTimeOffset CreatedUtc { get; } = createdUtc;
        public DateTimeOffset ExpiresUtc { get; } = expiresUtc;
        public int Attempts { get; set; } = attempts;
    }
    private sealed record VerifiedPhone(string Phone, DateTimeOffset ExpiresUtc);
}

public sealed record OtpRequestResult(bool Success, string? TestCode, string Message, int RetryAfterSeconds);
public sealed record OtpVerifyResult(bool Success, string? VerificationToken, string Message);
