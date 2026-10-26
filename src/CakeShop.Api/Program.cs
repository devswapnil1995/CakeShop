using CakeShop.Api.Contracts;
using CakeShop.Application.Catalog;
using CakeShop.Domain.Catalog;
using CakeShop.Domain.Content;
using CakeShop.Domain.Expenses;
using CakeShop.Domain.Identity;
using CakeShop.Domain.Orders;
using CakeShop.Domain.Payments;
using CakeShop.Infrastructure.Payments;
using CakeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connectionString = !string.IsNullOrWhiteSpace(configuredConnectionString)
    ? configuredConnectionString
    : Environment.GetEnvironmentVariable("CAKESHOP_SQL_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure a non-empty ConnectionStrings:DefaultConnection or CAKESHOP_SQL_CONNECTION.");
var configuredShopId = builder.Configuration["Shop:DefaultClientId"];
if (!Guid.TryParse(configuredShopId, out var defaultClientId) || defaultClientId == Guid.Empty)
    throw new InvalidOperationException("Configure Shop:DefaultClientId with a stable GUID.");

var jwtKey = builder.Configuration["Jwt:SigningKey"] ?? Environment.GetEnvironmentVariable("CAKESHOP_JWT_SIGNING_KEY");
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configure Jwt:SigningKey (at least 32 UTF-8 bytes) or CAKESHOP_JWT_SIGNING_KEY.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CakeShop.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CakeShop.Admin";
var tokenMinutes = int.TryParse(builder.Configuration["Jwt:ExpiryMinutes"], out var parsedMinutes) ? Math.Clamp(parsedMinutes, 5, 120) : 30;

builder.Services.AddDbContext<CakeShopDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddCors(options =>
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins(
            "http://localhost:4200",
            "https://localhost:4200",
            "https://happy-coast-0b85acf00.6.azurestaticapps.net"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<CakeShopDbContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
});

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});
builder.Services.AddHttpClient<RazorpayPaymentService>();

var app = builder.Build();

// Ensure the web root exists before static-file middleware or upload endpoints use it.
// This prevents DirectoryNotFoundException on fresh local checkouts where wwwroot
// has not been created yet.
var webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrWhiteSpace(webRootPath))
{
    webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    app.Environment.WebRootPath = webRootPath;
}
Directory.CreateDirectory(webRootPath);

app.UseExceptionHandler();
app.UseCors("AngularDev");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();


app.MapGet("/api/v1/hero-banners", async (CakeShopDbContext db, CancellationToken ct) =>
{
    var now = DateTime.UtcNow;
    var banners = await db.HeroBanners.AsNoTracking()
        .Where(x => x.ClientId == defaultClientId && x.IsPublished
            && (!x.StartsUtc.HasValue || x.StartsUtc.Value <= now)
            && (!x.EndsUtc.HasValue || x.EndsUtc.Value > now))
        .OrderBy(x => x.SortOrder)
        .ThenBy(x => x.CreatedUtc)
        .Select(x => new HeroBannerDto(
            x.Id, x.Title, x.Subtitle, (x.TextPosition == "top" || x.TextPosition == "bottom" || x.TextPosition == "left" || x.TextPosition == "right") ? x.TextPosition : "left", x.MediaType, x.MediaUrl, x.MobileMediaUrl,
            x.ButtonText, x.ButtonUrl, x.SortOrder, x.StartsUtc, x.EndsUtc))
        .ToListAsync(ct);
    return Results.Ok(banners);
}).WithName("GetPublishedHeroBanners").WithTags("Content");

app.MapGet("/api/v1/shop", async (CakeShopDbContext db, CancellationToken ct) =>
{
    var shop = await db.Clients.AsNoTracking().Where(x => x.Id == defaultClientId && x.IsActive)
        .Select(x => new ShopDto(x.Id, x.Name, x.Slug)).SingleOrDefaultAsync(ct);
    return shop is null ? Results.NotFound() : Results.Ok(shop);
}).WithName("GetShop").WithTags("Shop");

app.MapGet("/api/v1/categories", async (CakeShopDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Categories.AsNoTracking().Where(x => x.ClientId == defaultClientId && x.IsActive)
        .OrderBy(x => x.SortOrder).Select(x => new CategoryDto(x.Id, x.Name, x.Slug, x.SortOrder)).ToListAsync(ct)))
    .WithName("GetCategories").WithTags("Catalog");

app.MapGet("/api/v1/products", async (CakeShopDbContext db, string? category, bool? featured, CancellationToken ct) =>
{
    var query = from product in db.Products.AsNoTracking()
                join cat in db.Categories.AsNoTracking() on new { product.ClientId, Id = product.CategoryId } equals new { cat.ClientId, Id = cat.Id }
                where product.ClientId == defaultClientId && product.IsAvailable && cat.IsActive
                select new { product, cat };
    if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.cat.Slug == category.Trim().ToLowerInvariant());
    if (featured == true) query = query.Where(x => x.product.IsFeatured);
    var result = await query
    .OrderBy(x => x.cat.SortOrder)
    .ThenBy(x => x.product.Name)
    .Select(x => new
    {
        x.product.Id,
        x.product.Name,
        x.product.Slug,
        x.product.Description,
        x.product.Price,
        x.product.ImageUrl,
        x.product.Badge,
        x.product.IsFeatured,
        CategoryName = x.cat.Name,
        CategorySlug = x.cat.Slug,
        Variants = db.ProductVariants
            .Where(v => v.ClientId == x.product.ClientId &&
                        v.ProductId == x.product.Id &&
                        v.IsAvailable)
            .OrderBy(v => v.WeightInGrams)
            .Select(v => new ProductVariantDto(
                v.Id, v.SizeLabel, v.WeightInGrams, v.Price, v.IsAvailable))
            .ToList()
    })
    .ToListAsync(ct); return Results.Ok(result);
}).WithName("GetProducts").WithTags("Catalog");


app.MapGet("/api/v1/payments/availability", (
    IConfiguration configuration) =>
{
    var testMode = string.Equals(
        configuration["Razorpay:Environment"],
        "Test",
        StringComparison.OrdinalIgnoreCase);

    var hasTestKey = configuration["Razorpay:KeyId"]?
        .StartsWith("rzp_test_", StringComparison.Ordinal) == true;

    var hasSecret = !string.IsNullOrWhiteSpace(
        configuration["Razorpay:KeySecret"]);

    return Results.Ok(new
    {
        available = testMode && hasTestKey && hasSecret
    });
})
.AllowAnonymous()
.WithName("GetPaymentAvailability")
.WithTags("Payments");


app.MapPost("/api/v1/orders", async (
    CreateCustomerOrderRequest request,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    var customerName = request.CustomerName?.Trim();
    var phone = request.CustomerPhone?.Trim();
    var cakeSize = request.CakeSize?.Trim();
    var customization = request.Customization?.Trim();

    if (string.IsNullOrWhiteSpace(customerName) || customerName.Length > 120)
        return Results.BadRequest(new { message = "Customer name is required (maximum 120 characters)." });

    if (string.IsNullOrWhiteSpace(phone) || phone.Length > 20)
        return Results.BadRequest(new { message = "A valid phone number is required." });

    var phoneDigits = new string(phone.Where(char.IsDigit).ToArray());
    if (phoneDigits.Length < 10 || phoneDigits.Length > 15)
        return Results.BadRequest(new { message = "Phone number must contain 10 to 15 digits." });

    if (request.Quantity < 1 || request.Quantity > 100)
        return Results.BadRequest(new { message = "Quantity must be between 1 and 100." });

    if (string.IsNullOrWhiteSpace(cakeSize) || cakeSize.Length > 30)
        return Results.BadRequest(new { message = "Cake size is required (maximum 30 characters)." });

    if (customization?.Length > 1000)
        return Results.BadRequest(new { message = "Customization must be at most 1000 characters." });


    var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");

    var indiaToday = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone));

    if (request.PreferredDate.HasValue &&
        request.PreferredDate.Value < indiaToday)
    {
        return Results.BadRequest(new
        {
            message = "Preferred date cannot be in the past."
        });
    }



    var product = await db.Products
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.Id == request.ProductId
                 && x.ClientId == defaultClientId
                 && x.IsAvailable,
            ct);

    if (product is null)
    {
        return Results.BadRequest(new
        {
            message = "Select an available cake from this shop."
        });
    }

    var availableVariants = db.ProductVariants
    .AsNoTracking()
    .Where(v =>
        v.ClientId == defaultClientId &&
        v.ProductId == product.Id &&
        v.IsAvailable);

    var hasVariants = await availableVariants.AnyAsync(ct);
    
    decimal unitPrice;
    string selectedCakeSize;

    if (request.VariantId.HasValue)
    {
        var variant = await availableVariants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.VariantId.Value,
                ct);

        if (variant is null)
        {
            return Results.BadRequest(new
            {
                message = "The selected cake size is unavailable. Please select another size."
            });
        }

        unitPrice = variant.Price;
        selectedCakeSize = variant.SizeLabel;
    }
    else
    {
        // Backward compatibility: allow legacy products only
        // while they have no configured size variants.

        if (hasVariants)
        {
            if (!request.VariantId.HasValue)
            {
                return Results.BadRequest(new
                {
                    message = "Select an available cake size."
                });
            }

            var variant = await availableVariants.SingleOrDefaultAsync(
                v => v.Id == request.VariantId.Value,
                ct);

            if (variant is null)
            {
                return Results.BadRequest(new
                {
                    message = "The selected cake size is unavailable."
                });
            }

            unitPrice = variant.Price;
            selectedCakeSize = variant.SizeLabel;
        }
        else
        {
            return Results.BadRequest(new
            {
                message = "This product has no configured sizes. Please contact the shop."
            });
        }
    }

    var orderId = Guid.NewGuid();
    var orderNumber =
        $"SP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    var order = new CustomerOrder(
        orderId,
        defaultClientId,
        orderNumber,
        customerName,
        phone,
        product.Id,
        product.Name,
        request.Quantity,
        selectedCakeSize,
        request.PreferredDate,
        customization,
        unitPrice);

    db.CustomerOrders.Add(order);

    await db.SaveChangesAsync(ct);

    return Results.Created(
        $"/api/v1/orders/{order.Id}",
        new
        {
            order.Id,
            order.OrderNumber,
            order.CakeName,
            order.Quantity,
            order.UnitPrice,
            order.TotalAmount,
            Status = order.Status.ToString(),
            order.CreatedUtc,
            Message = "Your order has been saved successfully."
        });
})
.AllowAnonymous()
.WithName("CreateCustomerOrder")
.WithTags("Orders");


app.MapPost("/api/v1/orders/{id:guid}/payment", async (
    Guid id,
    CakeShopDbContext db,
    RazorpayPaymentService razorpay,
    IConfiguration configuration,
    CancellationToken ct) =>
{
    // This single-shop MVP uses the configured shop ID.
    // Resolve this from a trusted storefront/tenant mapping
    // before enabling multiple public storefronts.
    var order = await db.CustomerOrders
        .SingleOrDefaultAsync(
            x => x.Id == id && x.ClientId == defaultClientId,
            ct);

    if (order is null)
        return Results.NotFound(new { message = "Order not found." });

    if (order.Status == CustomerOrderStatus.Cancelled ||
        order.Status == CustomerOrderStatus.Completed)
    {
        return Results.Conflict(new
        {
            message = "Payment is unavailable for this order status."
        });
    }

    if (order.TotalAmount is not decimal total ||
        total < 1m ||
        total != decimal.Round(total, 2, MidpointRounding.AwayFromZero))
    {
        return Results.BadRequest(new
        {
            message = "The order does not have a valid payable total."
        });
    }


    var alreadyPaid = await db.PaymentTransactions.AnyAsync(
        p => p.OrderId == order.Id
          && p.ClientId == order.ClientId
          && p.Provider == "Razorpay"
          && p.Status == PaymentStatus.Captured,
        ct);

    if (alreadyPaid)
    {
        return Results.Conflict(new
        {
            message = "This order has already been paid.",
            alreadyPaid = true
        });
    }


    var environment = configuration["Razorpay:Environment"];

    if (!string.Equals(environment, "Test",
            StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new
        {
            message = "Only Test Mode is supported by this endpoint."
        });
    }

    //var settings = await db.TenantPaymentSettings
    //    .AsNoTracking()
    //    .SingleOrDefaultAsync(x =>
    //        x.ClientId == defaultClientId &&
    //        x.Provider == "Razorpay" &&
    //        x.Environment == PaymentEnvironment.Test &&
    //        x.IsEnabled,
    //        ct);

    //if (settings is null ||
    //    string.IsNullOrWhiteSpace(settings.MerchantAccountReference))
    //{
    //    return Results.Conflict(new
    //    {
    //        message = "Online payments are not configured for this shop. Please use WhatsApp ordering."
    //    });
    //}

    // Reuse an existing pending provider order when possible.
    var existing = await db.PaymentTransactions
        .AsNoTracking()
        .Where(x =>
            x.ClientId == defaultClientId &&
            x.OrderId == order.Id &&
            x.Provider == "Razorpay" &&
            x.Status == PaymentStatus.Pending &&
            x.ProviderOrderId != null)
        .OrderByDescending(x => x.CreatedUtc)
        .FirstOrDefaultAsync(ct);

    if (existing is not null)
    {
        return Results.Ok(new
        {
            paymentId = existing.Id,
            orderId = existing.ProviderOrderId,
            amount = decimal.ToInt64(total * 100m),
            currency = "INR",
            keyId = configuration["Razorpay:KeyId"]
        });
    }

    var amountInPaise = decimal.ToInt64(total * 100m);

    var providerOrder = await razorpay.CreateOrderAsync(
     amountInPaise,
     order.OrderNumber,
     ct);

    var transaction = new PaymentTransaction(
        Guid.NewGuid(),
        defaultClientId,
        order.Id,
        total);

    transaction.SetProviderOrderId(providerOrder.OrderId);

    db.PaymentTransactions.Add(transaction);
    await db.SaveChangesAsync(ct);

    return Results.Created(
        $"/api/v1/orders/{order.Id}/payment",
        new
        {
            paymentId = transaction.Id,
            orderId = providerOrder.OrderId,
            amount = providerOrder.AmountInPaise,
            currency = providerOrder.Currency,
            keyId = configuration["Razorpay:KeyId"]
        });
})
.AllowAnonymous()
.WithName("CreateCustomerPaymentOrder")
.WithTags("Payments");


app.MapPost("/api/v1/payments/verify", async (
    VerifyRazorpayPaymentRequest request,
    CakeShopDbContext db,
    RazorpayPaymentService razorpay,
    CancellationToken ct) =>
{
    var transaction = await db.PaymentTransactions.SingleOrDefaultAsync(
        x => x.Id == request.PaymentTransactionId
          && x.ClientId == defaultClientId,
        ct);

    if (transaction is null)
        return Results.NotFound(new
        {
            message = "Payment transaction not found."
        });

    // A previously captured payment must belong to this payment attempt.
    if (transaction.Status == PaymentStatus.Captured)
    {
        return transaction.ProviderPaymentId == request.RazorpayPaymentId
            ? Results.Ok(new { verified = true, status = "Captured" })
            : Results.Conflict(new
            {
                message = "A different payment was already captured."
            });
    }

    if (transaction.Status != PaymentStatus.Pending ||
        string.IsNullOrWhiteSpace(transaction.ProviderOrderId))
    {
        return Results.Conflict(new
        {
            message = "Payment is not awaiting verification."
        });
    }

    // Verify using the provider order ID saved on the server.
    if (!razorpay.VerifyCheckoutSignature(
            transaction.ProviderOrderId,
            request.RazorpayPaymentId,
            request.RazorpaySignature))
    {
        return Results.BadRequest(new
        {
            message = "Invalid payment signature."
        });
    }

    var expectedAmount = decimal.ToInt64(transaction.Amount * 100m);

    var captured = await razorpay.IsPaymentCapturedAsync(
        request.RazorpayPaymentId,
        transaction.ProviderOrderId,
        expectedAmount,
        ct);

    if (!captured)
    {
        return Results.Conflict(new
        {
            message =
                "Payment is not captured or does not match the expected order and amount."
        });
    }

    // Find the corresponding order within the same tenant.
    var order = await db.CustomerOrders.SingleOrDefaultAsync(
        x => x.Id == transaction.OrderId
          && x.ClientId == transaction.ClientId,
        ct);

    if (order is null)
    {
        return Results.Conflict(new
        {
            message = "The order associated with this payment was not found."
        });
    }

    // Use the domain transition rules.
    if (order.Status == CustomerOrderStatus.Pending)
    {
        order.UpdateStatus(CustomerOrderStatus.Confirmed);
    }
    else if (order.Status != CustomerOrderStatus.Confirmed)
    {
        return Results.Conflict(new
        {
            message = "The order cannot be confirmed in its current state."
        });
    }

    // Persist payment and order changes together.
    await using var dbTransaction =
        await db.Database.BeginTransactionAsync(ct);

    transaction.MarkCaptured(request.RazorpayPaymentId);

    await db.SaveChangesAsync(ct);
    await dbTransaction.CommitAsync(ct);

    return Results.Ok(new
    {
        verified = true,
        status = "Captured",
        orderStatus = order.Status.ToString()
    });
})
.AllowAnonymous()
.WithName("VerifyCustomerPayment")
.WithTags("Payments");


app.MapPost("/api/v1/payments/webhook", async (
    HttpRequest request,
    CakeShopDbContext db,
    IConfiguration configuration,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var webhookSecret = configuration["Razorpay:WebhookSecret"];
    var suppliedSignature =
        request.Headers["X-Razorpay-Signature"].ToString();
    var eventId = request.Headers["x-razorpay-event-id"].ToString();

    if (string.IsNullOrWhiteSpace(webhookSecret) ||
        string.IsNullOrWhiteSpace(suppliedSignature) ||
        string.IsNullOrWhiteSpace(eventId) ||
        eventId.Length > 200)
    {
        return Results.Unauthorized();
    }

    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync(ct);

    var expectedSignature = System.Security.Cryptography.HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(webhookSecret),
        Encoding.UTF8.GetBytes(rawBody));

    byte[] actualSignature;
    try
    {
        actualSignature = Convert.FromHexString(suppliedSignature);
    }
    catch (FormatException)
    {
        return Results.Unauthorized();
    }

    if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            expectedSignature, actualSignature))
    {
        return Results.Unauthorized();
    }

    using var document = JsonDocument.Parse(rawBody);
    var root = document.RootElement;

    // Acknowledge event types this handler doesn't process.
    if (root.GetProperty("event").GetString() != "payment.captured")
        return Results.Ok(new { received = true });

    var payment = root.GetProperty("payload")
       .GetProperty("payment")
       .GetProperty("entity");

    var providerPaymentId = payment.GetProperty("id").GetString();
    var providerOrderId = payment.GetProperty("order_id").GetString();
    var currency = payment.GetProperty("currency").GetString();
    var amount = payment.GetProperty("amount").GetInt64();
    var status = payment.GetProperty("status").GetString();

    if (string.IsNullOrWhiteSpace(providerPaymentId) ||
        string.IsNullOrWhiteSpace(providerOrderId) ||
        currency != "INR" ||
        status != "captured")
    {
        return Results.BadRequest();
    }

    await using var transaction = await db.Database.BeginTransactionAsync(ct);

    // Record only events this handler actually processes.
    db.ProcessedPaymentWebhooks.Add(
        new CakeShop.Domain.Payments.ProcessedPaymentWebhook(eventId));

    var paymentTransaction = await db.PaymentTransactions.SingleOrDefaultAsync(
        x => x.Provider == "Razorpay" &&
             x.ProviderOrderId == providerOrderId,
        ct);

    if (paymentTransaction is null)
    {
        logger.LogWarning(
            "Razorpay captured event references unknown order {ProviderOrderId}.",
            providerOrderId);

        return Results.NotFound(new { message = "Payment order not found." });
    }

    var expectedAmount = decimal.ToInt64(paymentTransaction.Amount * 100m);

    if (paymentTransaction.Currency != currency || expectedAmount != amount)
        return Results.BadRequest(new { message = "Payment amount or currency mismatch." });


    var order = await db.CustomerOrders.SingleOrDefaultAsync(
        x => x.Id == paymentTransaction.OrderId
          && x.ClientId == paymentTransaction.ClientId,
        ct);

    if (order is null)
    {
        logger.LogError(
            "Captured Razorpay payment {PaymentId} has no matching order.",
            providerPaymentId);

        return Results.Conflict(new
        {
            message = "Payment captured, but the associated order was not found."
        });
    }

    if (paymentTransaction.Status == PaymentStatus.Captured)
    {
        if (paymentTransaction.ProviderPaymentId == providerPaymentId)
        {
            // This payment is already recorded.
            // Continue through the idempotent event-recording path.
        }
        else
        {
            logger.LogError(
                "A different payment ID was received for captured transaction {TransactionId}.",
                paymentTransaction.Id);

            // Do not overwrite the previously captured payment.
            // Record this event safely and flag it for reconciliation.
        }
    }
    else
    {
        paymentTransaction.MarkCaptured(providerPaymentId);
    }

    paymentTransaction.MarkCaptured(providerPaymentId);

    if (order.Status == CustomerOrderStatus.Pending)
    {
        order.UpdateStatus(CustomerOrderStatus.Confirmed);
    }
    else if (order.Status != CustomerOrderStatus.Confirmed)
    {
        // Preserve the captured payment, but flag the order for owner review.
        logger.LogError(
            "Payment {PaymentId} was captured for order {OrderNumber}, " +
            "which is in status {OrderStatus}. Manual reconciliation is required.",
            providerPaymentId,
            order.OrderNumber,
            order.Status);
    }

    try
    {
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    catch (DbUpdateException)
    {
        await transaction.RollbackAsync(ct);

        var alreadyProcessed = await db.ProcessedPaymentWebhooks
            .AsNoTracking()
            .AnyAsync(
                x => x.Provider == "Razorpay" && x.EventId == eventId,
                ct);

        if (alreadyProcessed)
            return Results.Ok(new { received = true, duplicate = true });

        throw;
    }


    return Results.Ok(new { received = true, processed = true });
})
.AllowAnonymous()
.WithName("RazorpayWebhook")
.WithTags("Payments");

var auth = app.MapGroup("/api/v1/admin/auth").WithTags("Admin Authentication");
auth.MapPost("/login", async (LoginRequest request, CakeShopDbContext db, IPasswordHasher<AdminUser> hasher, IConfiguration config, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { message = "Email and password are required." });
    var normalizedEmail = request.Email.Trim().ToUpperInvariant();
    var admin = await db.AdminUsers.SingleOrDefaultAsync(x => x.ClientId == defaultClientId && x.NormalizedEmail == normalizedEmail && x.IsActive, ct);
    if (admin is null || hasher.VerifyHashedPassword(admin, admin.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    var now = DateTime.UtcNow;
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
        new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
        new Claim(ClaimTypes.Name, admin.Email),
        new Claim(JwtRegisteredClaimNames.Email, admin.Email),
        new Claim("client_id", admin.ClientId.ToString()),
        new Claim(ClaimTypes.Role, admin.Role)
    };
    var signingKey = config["Jwt:SigningKey"] ?? Environment.GetEnvironmentVariable("CAKESHOP_JWT_SIGNING_KEY")!;
    var issuer = config["Jwt:Issuer"] ?? "CakeShop.Api";
    var audience = config["Jwt:Audience"] ?? "CakeShop.Admin";
    var expiryMinutes = int.TryParse(config["Jwt:ExpiryMinutes"], out var minutes) ? Math.Clamp(minutes, 5, 120) : 30;
    var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(issuer, audience, claims, now, now.AddMinutes(expiryMinutes), credentials);
    return Results.Ok(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), token.ValidTo, admin.Email, admin.Role));
}).AllowAnonymous();

var adminApi = app.MapGroup("/api/v1/admin").RequireAuthorization().WithTags("Admin Catalog");


adminApi.MapPost("/products/images", async (
    ClaimsPrincipal user, IFormFile file, IWebHostEnvironment environment, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { message = "Choose an image to upload." });
    if (file.Length > 10 * 1024 * 1024)
        return Results.BadRequest(new { message = "Product images must be 10 MB or smaller." });

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".png"] = new[] { "image/png" },
        [".webp"] = new[] { "image/webp" }
    };
    if (!allowed.TryGetValue(extension, out var contentTypes) ||
        !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "Supported product images: JPG, PNG and WebP. File type must match the extension." });

    var header = new byte[12];
    await using (var input = file.OpenReadStream())
    {
        var read = await input.ReadAsync(header.AsMemory(0, Math.Min(header.Length, (int)file.Length)), ct);
        if (!IsAllowedHeroFileSignature(extension, header, read))
            return Results.BadRequest(new { message = "The file contents do not match the selected image type." });
    }

    var uploadRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "products", clientId.ToString("N"));
    Directory.CreateDirectory(uploadRoot);
    var safeName = $"{Guid.NewGuid():N}{extension}";
    var fullPath = Path.Combine(uploadRoot, safeName);
    await using (var output = File.Create(fullPath))
    await using (var input = file.OpenReadStream())
        await input.CopyToAsync(output, ct);

    return Results.Ok(new { mediaUrl = $"/uploads/products/{clientId:N}/{safeName}" });
}).DisableAntiforgery();

adminApi.MapPost("/hero-banners/media", async (
    ClaimsPrincipal user, IFormFile file, IWebHostEnvironment environment, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    if (file is null || file.Length == 0) return Results.BadRequest(new { message = "Choose a file to upload." });
    if (file.Length > 20 * 1024 * 1024) return Results.BadRequest(new { message = "Media files must be 20 MB or smaller." });

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowed = new Dictionary<string, (string MediaType, string[] ContentTypes)>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ("Image", new[] { "image/jpeg" }),
        [".jpeg"] = ("Image", new[] { "image/jpeg" }),
        [".png"] = ("Image", new[] { "image/png" }),
        [".webp"] = ("Image", new[] { "image/webp" }),
        [".mp4"] = ("Video", new[] { "video/mp4" }),
        [".webm"] = ("Video", new[] { "video/webm" })
    };
    if (!allowed.TryGetValue(extension, out var fileInfo) || !fileInfo.ContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "Supported files: JPG, PNG, WebP, MP4, and WebM. File type must match the extension." });

    var header = new byte[12];
    await using (var input = file.OpenReadStream())
    {
        var read = await input.ReadAsync(header.AsMemory(0, Math.Min(header.Length, (int)file.Length)), ct);
        if (!IsAllowedHeroFileSignature(extension, header, read))
            return Results.BadRequest(new { message = "The file contents do not match the selected media type." });
    }

    var uploadRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "heroes", clientId.ToString("N"));
    Directory.CreateDirectory(uploadRoot);
    var safeName = $"{Guid.NewGuid():N}{extension}";
    var fullPath = Path.Combine(uploadRoot, safeName);
    await using (var output = File.Create(fullPath))
    await using (var input = file.OpenReadStream())
        await input.CopyToAsync(output, ct);

    return Results.Ok(new { mediaUrl = $"/uploads/heroes/{clientId:N}/{safeName}", mediaType = fileInfo.MediaType });
}).DisableAntiforgery();

adminApi.MapGet("/hero-banners", async (ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var banners = await db.HeroBanners.AsNoTracking()
        .Where(x => x.ClientId == clientId)
        .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedUtc)
        .Select(x => new HeroBannerDto(
            x.Id, x.Title, x.Subtitle, (x.TextPosition == "top" || x.TextPosition == "bottom" || x.TextPosition == "left" || x.TextPosition == "right") ? x.TextPosition : "left", x.MediaType, x.MediaUrl, x.MobileMediaUrl,
            x.ButtonText, x.ButtonUrl, x.SortOrder, x.StartsUtc, x.EndsUtc, x.IsPublished))
        .ToListAsync(ct);
    return Results.Ok(banners);
});

adminApi.MapPost("/hero-banners", async (
    ClaimsPrincipal user, HeroBannerWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var validation = ValidateHeroBanner(request);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var banner = new HeroBanner(clientId, request.Title ?? string.Empty, request.Subtitle ?? string.Empty, request.MediaType,
        request.MediaUrl, request.MobileMediaUrl, request.ButtonText, request.ButtonUrl,
        request.SortOrder, request.StartsUtc, request.EndsUtc, NormalizeHeroTextPosition(request.TextPosition));
    db.HeroBanners.Add(banner);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/admin/hero-banners/{banner.Id}",
        new HeroBannerDto(banner.Id, banner.Title, banner.Subtitle, NormalizeHeroTextPosition(banner.TextPosition), banner.MediaType,
            banner.MediaUrl, banner.MobileMediaUrl, banner.ButtonText, banner.ButtonUrl,
            banner.SortOrder, banner.StartsUtc, banner.EndsUtc, banner.IsPublished));
});

adminApi.MapPut("/hero-banners/{id:guid}", async (
    Guid id, ClaimsPrincipal user, HeroBannerWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var validation = ValidateHeroBanner(request);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var banner = await db.HeroBanners.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (banner is null) return Results.NotFound();
    banner.UpdateContent(request.Title ?? string.Empty, request.Subtitle ?? string.Empty, request.MediaType, request.MediaUrl,
        request.MobileMediaUrl, request.ButtonText, request.ButtonUrl,
        request.SortOrder, request.StartsUtc, request.EndsUtc, NormalizeHeroTextPosition(request.TextPosition));
    await db.SaveChangesAsync(ct);
    return Results.Ok(new HeroBannerDto(banner.Id, banner.Title, banner.Subtitle, NormalizeHeroTextPosition(banner.TextPosition), banner.MediaType,
        banner.MediaUrl, banner.MobileMediaUrl, banner.ButtonText, banner.ButtonUrl,
        banner.SortOrder, banner.StartsUtc, banner.EndsUtc, banner.IsPublished));
});

adminApi.MapPost("/hero-banners/{id:guid}/publish", async (
    Guid id, ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var banner = await db.HeroBanners.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (banner is null) return Results.NotFound();
    banner.Publish();
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

adminApi.MapPost("/hero-banners/{id:guid}/unpublish", async (
    Guid id, ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var banner = await db.HeroBanners.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (banner is null) return Results.NotFound();
    banner.Unpublish();
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

adminApi.MapDelete("/hero-banners/{id:guid}", async (
    Guid id, ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var banner = await db.HeroBanners.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (banner is null) return Results.NotFound();
    db.HeroBanners.Remove(banner);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});




adminApi.MapGet("/orders", async (
    ClaimsPrincipal user,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    var orders = await db.CustomerOrders
        .AsNoTracking()
        .Where(x => x.ClientId == clientId)
        .OrderByDescending(x => x.CreatedUtc)
        .Select(x => new
        {
            x.Id,
            x.OrderNumber,
            x.CustomerName,
            x.CustomerPhone,
            x.CakeName,
            x.Quantity,
            x.CakeSize,
            x.PreferredDate,
            x.Customization,
            x.UnitPrice,
            x.TotalAmount,
            x.CompletedUtc,
            Status = x.Status.ToString(),
            x.CreatedUtc,
            x.UpdatedUtc,
            PaymentStatus = db.PaymentTransactions
    .Where(p => p.OrderId == x.Id
             && p.ClientId == x.ClientId
             && p.Status == PaymentStatus.Captured)
    .Select(p => p.Status.ToString())
    .FirstOrDefault()
    ?? db.PaymentTransactions
        .Where(p => p.OrderId == x.Id
                 && p.ClientId == x.ClientId)
        .OrderByDescending(p => p.CreatedUtc)
        .Select(p => p.Status.ToString())
        .FirstOrDefault(),
        })
        .ToListAsync(ct);

    return Results.Ok(orders);
})
.WithName("GetAdminOrders")
.WithTags("Admin Orders");


adminApi.MapPut("/orders/{id:guid}/status", async (
    Guid id,
    ClaimsPrincipal user,
    UpdateCustomerOrderStatusRequest request,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    if (!Enum.TryParse<CustomerOrderStatus>(
            request.Status, true, out var status)
        || !Enum.IsDefined(status))
    {
        return Results.BadRequest(new
        {
            message = "Status must be Pending, Confirmed, Preparing, Ready, Completed, or Cancelled."
        });
    }

    var order = await db.CustomerOrders
        .SingleOrDefaultAsync(
            x => x.Id == id && x.ClientId == clientId,
            ct);

    if (order is null)
        return Results.NotFound(new { message = "Order not found." });

    if (!order.CanTransitionTo(status))
    {
        return Results.Conflict(new
        {
            message = $"Cannot change order status from {order.Status} to {status}."
        });
    }

    // Capture the old status before updating the order.
    var previousStatus = order.Status;

    // Read the authenticated user's identifier when available.
    var changedBy =
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value
        ?? user.FindFirst(ClaimTypes.Email)?.Value
        ?? user.FindFirst("email")?.Value;

    // Update the order and create its history record together.
    order.UpdateStatus(status);

    var history = new OrderStatusHistory(
        id: Guid.NewGuid(),
        clientId: clientId,
        orderId: order.Id,
        previousStatus: previousStatus,
        newStatus: order.Status,
        changedBy: changedBy);

    db.OrderStatusHistories.Add(history);

    // Both changes are saved in the same database transaction.
    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        order.Id,
        order.OrderNumber,
        PreviousStatus = previousStatus.ToString(),
        Status = order.Status.ToString(),
        order.UpdatedUtc,
        HistoryId = history.Id,
        history.ChangedUtc,
        history.ChangedBy
    });
})
.WithName("UpdateAdminOrderStatus")
.WithTags("Admin Orders");


adminApi.MapGet("/orders/{id:guid}/history", async (
    Guid id,
    ClaimsPrincipal user,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    // Confirm the order belongs to the authenticated tenant.
    var orderExists = await db.CustomerOrders
        .AsNoTracking()
        .AnyAsync(
            x => x.Id == id && x.ClientId == clientId,
            ct);

    if (!orderExists)
        return Results.NotFound(new { message = "Order not found." });

    var history = await db.OrderStatusHistories
        .AsNoTracking()
        .Where(x => x.OrderId == id && x.ClientId == clientId)
        .OrderBy(x => x.ChangedUtc)
        .ThenBy(x => x.Id)
        .Select(x => new
        {
            x.Id,
            x.OrderId,
            PreviousStatus = x.PreviousStatus.ToString(),
            NewStatus = x.NewStatus.ToString(),
            x.ChangedUtc,
            x.ChangedBy
        })
        .ToListAsync(ct);

    return Results.Ok(history);
})
.WithName("GetAdminOrderStatusHistory")
.WithTags("Admin Orders");


adminApi.MapGet("/categories", async (ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    return Results.Ok(await db.Categories.AsNoTracking().Where(x => x.ClientId == clientId)
        .OrderBy(x => x.SortOrder).Select(x => new AdminCategoryDto(x.Id, x.Name, x.Slug, x.SortOrder, x.IsActive)).ToListAsync(ct));
});

adminApi.MapPost("/categories", async (ClaimsPrincipal user, CategoryWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var name = request.Name?.Trim(); var slug = NormalizeSlug(request.Slug, name);
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || string.IsNullOrWhiteSpace(slug) || slug.Length > 120)
        return Results.BadRequest(new { message = "A category name (max 100 chars) and valid slug (max 120 chars) are required." });
    if (await db.Categories.AnyAsync(x => x.ClientId == clientId && x.Slug == slug, ct)) return Results.Conflict(new { message = "A category with this slug already exists." });
    var entity = new Category(Guid.NewGuid(), clientId, name, slug, request.SortOrder);
    entity.SetActive(request.IsActive);
    db.Categories.Add(entity); await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/admin/categories/{entity.Id}", new AdminCategoryDto(entity.Id, entity.Name, entity.Slug, entity.SortOrder, entity.IsActive));
});

adminApi.MapPut("/categories/{id:guid}", async (Guid id, ClaimsPrincipal user, CategoryWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var entity = await db.Categories.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (entity is null) return Results.NotFound();
    var name = request.Name?.Trim(); var slug = NormalizeSlug(request.Slug, name);
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || string.IsNullOrWhiteSpace(slug) || slug.Length > 120) return Results.BadRequest(new { message = "Category name or slug is invalid." });
    if (await db.Categories.AnyAsync(x => x.ClientId == clientId && x.Id != id && x.Slug == slug, ct)) return Results.Conflict(new { message = "A category with this slug already exists." });
    entity.Update(name, slug, request.SortOrder); entity.SetActive(request.IsActive); await db.SaveChangesAsync(ct);
    return Results.Ok(new AdminCategoryDto(entity.Id, entity.Name, entity.Slug, entity.SortOrder, entity.IsActive));
});

adminApi.MapGet("/products", async (ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var rows = await (from product in db.Products.AsNoTracking()
                      join category in db.Categories.AsNoTracking() on new { product.ClientId, Id = product.CategoryId } equals new { category.ClientId, Id = category.Id }
                      where product.ClientId == clientId
                      orderby category.SortOrder, product.Name
                      select new AdminProductDto(product.Id, product.CategoryId, product.Name, product.Slug, product.Description, product.Price, product.ImageUrl, product.Badge, product.IsFeatured, product.IsAvailable, category.Name)).ToListAsync(ct);
    return Results.Ok(rows);
});

adminApi.MapPost("/products", async (ClaimsPrincipal user, ProductWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var validation = ValidateProduct(request, clientId);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    if (!await db.Categories.AnyAsync(x => x.Id == request.CategoryId && x.ClientId == clientId && x.IsActive, ct)) return Results.BadRequest(new { message = "Choose an active category belonging to this shop." });
    var slug = NormalizeSlug(request.Slug, request.Name);
    if (await db.Products.AnyAsync(x => x.ClientId == clientId && x.Slug == slug, ct)) return Results.Conflict(new { message = "A product with this slug already exists." });
    var entity = new Product(Guid.NewGuid(), clientId, request.CategoryId, request.Name!, slug!, request.Description ?? "", request.Price, request.ImageUrl, request.Badge, request.IsFeatured, request.IsAvailable);
    db.Products.Add(entity); await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/admin/products/{entity.Id}", new { entity.Id, entity.CategoryId, entity.Name, entity.Slug, entity.Description, entity.Price, entity.ImageUrl, entity.Badge, entity.IsFeatured, entity.IsAvailable });
});
adminApi.MapPut("/products/{id:guid}", async (Guid id, ClaimsPrincipal user, ProductWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var entity = await db.Products.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (entity is null) return Results.NotFound();
    var validation = ValidateProduct(request, clientId);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    if (!await db.Categories.AnyAsync(x => x.Id == request.CategoryId && x.ClientId == clientId && x.IsActive, ct)) return Results.BadRequest(new { message = "Choose an active category belonging to this shop." });
    var slug = NormalizeSlug(request.Slug, request.Name);
    if (await db.Products.AnyAsync(x => x.ClientId == clientId && x.Id != id && x.Slug == slug, ct)) return Results.Conflict(new { message = "A product with this slug already exists." });
    entity.Update(request.CategoryId, request.Name!, slug!, request.Description ?? "", request.Price, request.ImageUrl, request.Badge, request.IsFeatured, request.IsAvailable);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { entity.Id, entity.CategoryId, entity.Name, entity.Slug, entity.Description, entity.Price, entity.ImageUrl, entity.Badge, entity.IsFeatured, entity.IsAvailable });
});

// Daily Operations dashboard. All aggregates are tenant-scoped and use India local-day boundaries.
adminApi.MapGet("/dashboard/summary", async (
    ClaimsPrincipal user,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();

    var indiaZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
    var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaZone));
    var localStart = localToday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
    var localEnd = localStart.AddDays(1);
    var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, indiaZone);
    var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, indiaZone);

    var orders = db.CustomerOrders.AsNoTracking().Where(x => x.ClientId == clientId);
    var createdToday = await orders.CountAsync(x => x.CreatedUtc >= utcStart && x.CreatedUtc < utcEnd, ct);
    var dueToday = await orders.CountAsync(x => x.PreferredDate == localToday && x.Status != CustomerOrderStatus.Cancelled, ct);
    var pending = await orders.CountAsync(x => x.Status == CustomerOrderStatus.Pending, ct);
    var preparing = await orders.CountAsync(x => x.Status == CustomerOrderStatus.Preparing, ct);
    var ready = await orders.CountAsync(x => x.Status == CustomerOrderStatus.Ready, ct);
    var upcoming = await orders
        .Where(x => x.PreferredDate > localToday && x.Status != CustomerOrderStatus.Cancelled)
        .OrderBy(x => x.PreferredDate).ThenBy(x => x.CreatedUtc)
        .Take(10)
        .Select(x => new { x.Id, x.OrderNumber, x.CustomerName, x.CakeName, x.Quantity, x.CakeSize, x.PreferredDate, Status = x.Status.ToString() })
        .ToListAsync(ct);
    var dueOrders = await orders
        .Where(x => x.PreferredDate == localToday && x.Status != CustomerOrderStatus.Cancelled)
        .OrderBy(x => x.CreatedUtc)
        .Select(x => new { x.Id, x.OrderNumber, x.CustomerName, x.CakeName, x.Quantity, x.CakeSize, x.PreferredDate, Status = x.Status.ToString() })
        .ToListAsync(ct);

    var completedOrdersToday = orders.Where(x =>
        x.Status == CustomerOrderStatus.Completed
        && x.CompletedUtc >= utcStart
        && x.CompletedUtc < utcEnd);

    var completedOrdersWithoutPrice = await completedOrdersToday
        .CountAsync(x => x.TotalAmount == null, ct);

    var completedRevenue = await completedOrdersToday
        .Where(x => x.TotalAmount != null)
        .SumAsync(x => (decimal?)x.TotalAmount, ct);

    var revenueAvailable = completedOrdersWithoutPrice == 0;

    return Results.Ok(new
    {
        localDate = localToday.ToString("yyyy-MM-dd"),
        createdToday,
        dueToday,
        pendingConfirmation = pending,
        preparing,
        ready,
        completedRevenueToday = completedRevenue,
        revenueAvailable,
        completedOrdersMissingPrice = completedOrdersWithoutPrice,
        dueOrders,
        upcomingOrders = upcoming
    });
}).WithName("GetAdminDashboardSummary").WithTags("Admin Dashboard");


adminApi.MapPost("/expenses", async (