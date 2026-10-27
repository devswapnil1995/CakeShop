using CakeShop.Api.Contracts;
using CakeShop.Api.Checkout;
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
builder.Services.AddSingleton<CheckoutOtpService>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("AngularDev");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapCheckoutEndpoints(defaultClientId);
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

app.MapGet("/api/v1/store-profile", async (CakeShopDbContext db, CancellationToken ct) =>
{
    var shop = await db.Clients.AsNoTracking().Where(x => x.Id == defaultClientId && x.IsActive)
        .Select(x => new { x.Id, x.Name, x.Slug }).SingleOrDefaultAsync(ct);
    if (shop is null) return Results.NotFound();
    var profile = await db.StoreProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == defaultClientId, ct);
    return Results.Ok(new StoreProfileDto(shop.Id, shop.Name, shop.Slug,
        profile?.Tagline ?? "Baked with a little magic",
        profile?.StoryTitle ?? "Not just cake. A feeling.",
        profile?.HomeDescription ?? "Thoughtfully baked, beautifully shared. Discover the care and creativity behind every Sweet Panda creation.",
        profile?.Story ?? "We believe the sweetest memories are often the simplest ones. Every bake is made with care, creativity, and lots of heart.",
        profile?.StoryImageUrl, profile?.LogoUrl, profile?.Phone, profile?.WhatsAppNumber, profile?.Email,
        profile?.Address, profile?.OpeningHours, profile?.InstagramUrl, profile?.FacebookUrl, profile?.WebsiteUrl));
}).AllowAnonymous().WithName("GetStoreProfile").WithTags("Shop");

app.MapGet("/api/v1/about-sections", async (CakeShopDbContext db, CancellationToken ct) =>
    Results.Ok(await db.AboutPageSections.AsNoTracking()
        .Where(x => x.ClientId == defaultClientId && x.IsPublished)
        .OrderBy(x => x.SortOrder)
        .Select(x => new AboutPageSectionDto(x.Id, x.Eyebrow, x.Title, x.Body, x.ImageUrl, x.SecondaryImageUrl, x.Layout, x.Animation, x.SortOrder, x.IsPublished))
        .ToListAsync(ct)))
    .AllowAnonymous().WithName("GetAboutPageSections").WithTags("Shop");

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


app.MapGet("/api/v1/collections", async (CakeShopDbContext db, string? kind, CancellationToken ct) =>
{
    var now = DateTime.UtcNow;
    var query = db.StoreCollections.AsNoTracking()
        .Where(x => x.ClientId == defaultClientId && x.IsPublished &&
            (!x.StartsUtc.HasValue || x.StartsUtc <= now) &&
            (!x.EndsUtc.HasValue || x.EndsUtc > now));
    if (!string.IsNullOrWhiteSpace(kind) && (kind == "Hamper" || kind == "Seasonal"))
        query = query.Where(x => x.Kind == kind);
    var collections = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Title)
        .Select(x => new
        {
            x.Id, x.Kind, x.Title, x.Slug, x.Description, x.Highlights, x.ImageUrl, x.NavigationLabel,
            x.ShowInNavigation, x.BundlePrice, x.SortOrder, x.StartsUtc, x.EndsUtc
        }).ToListAsync(ct);
    return Results.Ok(collections);
}).AllowAnonymous().WithName("GetStoreCollections").WithTags("Collections");

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
    CheckoutOtpService otp,
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

    var verifiedPhone = otp.GetVerifiedPhone(request.VerificationToken ?? string.Empty);
    if (verifiedPhone is null || !string.Equals(verifiedPhone, phoneDigits, StringComparison.Ordinal))
        return Results.BadRequest(new { message = "Verify this mobile number with OTP before placing the order." });

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

    if (!otp.TryConsumeVerificationToken(request.VerificationToken ?? string.Empty, phoneDigits))
        return Results.BadRequest(new { message = "Your mobile verification expired or was already used. Verify your number again." });

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

var profileApi = app.MapGroup("/api/v1/admin/store-profile").RequireAuthorization().WithTags("Store Profile");
profileApi.MapGet("/", async (ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var shop = await db.Clients.AsNoTracking().Where(x => x.Id == clientId && x.IsActive)
        .Select(x => new { x.Id, x.Name, x.Slug }).SingleOrDefaultAsync(ct);
    if (shop is null) return Results.NotFound();
    var p = await db.StoreProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
    return Results.Ok(new StoreProfileDto(shop.Id, shop.Name, shop.Slug,
        p?.Tagline ?? "Baked with a little magic", p?.StoryTitle ?? "Not just cake. A feeling.",
        p?.HomeDescription ?? "Thoughtfully baked, beautifully shared. Discover the care and creativity behind every Sweet Panda creation.",
        p?.Story ??
        p?.StoryImageUrl, p?.LogoUrl, p?.Phone, p?.WhatsAppNumber, p?.Email, p?.Address,
        p?.OpeningHours, p?.InstagramUrl, p?.FacebookUrl, p?.WebsiteUrl));
});
profileApi.MapPut("/", async (ClaimsPrincipal user, StoreProfileWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var validation = ValidateStoreProfile(request);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var profile = await db.StoreProfiles.SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
    if (profile is null)
    {
        profile = new StoreProfile(clientId);
        db.StoreProfiles.Add(profile);
    }
    profile.Update(request.Tagline, request.StoryTitle, request.HomeDescription, request.Story, request.StoryImageUrl,
        request.LogoUrl, request.Phone, request.WhatsAppNumber, request.Email, request.Address,
        request.OpeningHours, request.InstagramUrl, request.FacebookUrl, request.WebsiteUrl);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

var aboutApi = app.MapGroup("/api/v1/admin/about-sections").RequireAuthorization().WithTags("About Page");
aboutApi.MapGet("/", async (ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    return Results.Ok(await db.AboutPageSections.AsNoTracking().Where(x => x.ClientId == clientId)
        .OrderBy(x => x.SortOrder).Select(x => new AboutPageSectionDto(x.Id, x.Eyebrow, x.Title, x.Body, x.ImageUrl, x.SecondaryImageUrl, x.Layout, x.Animation, x.SortOrder, x.IsPublished)).ToListAsync(ct));
});
aboutApi.MapPost("/", async (ClaimsPrincipal user, AboutPageSectionWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var validation = ValidateAboutSection(request);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var section = new AboutPageSection(clientId, request.Title, request.Body);
    section.Update(request.Eyebrow, request.Title, request.Body, request.ImageUrl, request.SecondaryImageUrl, request.Layout, request.Animation, request.SortOrder, request.IsPublished);
    db.AboutPageSections.Add(section);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/admin/about-sections/{section.Id}", new AboutPageSectionDto(section.Id, section.Eyebrow, section.Title, section.Body, section.ImageUrl, section.SecondaryImageUrl, section.Layout, section.Animation, section.SortOrder, section.IsPublished));
});
aboutApi.MapPut("/{id:guid}", async (Guid id, ClaimsPrincipal user, AboutPageSectionWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var validation = ValidateAboutSection(request);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var section = await db.AboutPageSections.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (section is null) return Results.NotFound();
    section.Update(request.Eyebrow, request.Title, request.Body, request.ImageUrl, request.SecondaryImageUrl, request.Layout, request.Animation, request.SortOrder, request.IsPublished);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});
aboutApi.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var section = await db.AboutPageSections.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (section is null) return Results.NotFound();
    db.AboutPageSections.Remove(section);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

var collectionApi = app.MapGroup("/api/v1/admin/collections").RequireAuthorization().WithTags("Collections");
collectionApi.MapGet("/", async (ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var items = await db.StoreCollections.AsNoTracking().Where(x => x.ClientId == clientId)
        .OrderBy(x => x.SortOrder).ThenBy(x => x.Title)
        .Select(x => new { x.Id, x.Kind, x.Title, x.Slug, x.Description, x.Highlights, x.ImageUrl, x.NavigationLabel,
            x.ShowInNavigation, x.BundlePrice, x.IsPublished, x.SortOrder, x.StartsUtc, x.EndsUtc })
        .ToListAsync(ct);
    return Results.Ok(items);
});
collectionApi.MapPost("/", async (ClaimsPrincipal user, StoreCollectionWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var validation = ValidateStoreCollection(request);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var slug = request.Slug!.Trim().ToLowerInvariant();
    if (await db.StoreCollections.AnyAsync(x => x.ClientId == clientId && x.Kind == request.Kind && x.Slug == slug, ct))
        return Results.Conflict(new { message = "A collection with this slug already exists for this type." });
    var entity = new StoreCollection(clientId, request.Kind!, request.Title!, slug, request.Description ?? "",
        request.ImageUrl, request.NavigationLabel, request.ShowInNavigation, request.SortOrder,
        request.StartsUtc, request.EndsUtc, request.BundlePrice, request.Highlights);
    db.StoreCollections.Add(entity);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/admin/collections/{entity.Id}", new { entity.Id });
});
collectionApi.MapPut("/{id:guid}", async (Guid id, ClaimsPrincipal user, StoreCollectionWriteRequest request, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var entity = await db.StoreCollections.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (entity is null) return Results.NotFound();
    var validation = ValidateStoreCollection(request);
    if (validation is not null) return Results.BadRequest(new { message = validation });
    var slug = request.Slug!.Trim().ToLowerInvariant();
    if (await db.StoreCollections.AnyAsync(x => x.ClientId == clientId && x.Id != id && x.Kind == request.Kind && x.Slug == slug, ct))
        return Results.Conflict(new { message = "A collection with this slug already exists for this type." });
    entity.Update(request.Kind!, request.Title!, slug, request.Description ?? "", request.ImageUrl,
        request.NavigationLabel, request.ShowInNavigation, request.SortOrder, request.StartsUtc, request.EndsUtc, request.BundlePrice, request.Highlights);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});
collectionApi.MapPost("/{id:guid}/publish", async (Guid id, ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var entity = await db.StoreCollections.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (entity is null) return Results.NotFound();
    entity.SetPublished(true); await db.SaveChangesAsync(ct); return Results.NoContent();
});
collectionApi.MapPost("/{id:guid}/unpublish", async (Guid id, ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var entity = await db.StoreCollections.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (entity is null) return Results.NotFound();
    entity.SetPublished(false); await db.SaveChangesAsync(ct); return Results.NoContent();
});
collectionApi.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, CakeShopDbContext db, CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId)) return Results.Forbid();
    var entity = await db.StoreCollections.SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
    if (entity is null) return Results.NotFound();
    db.StoreCollections.Remove(entity); await db.SaveChangesAsync(ct); return Results.NoContent();
});



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
            Items = db.CustomerOrderItems.AsNoTracking()
                .Where(item => item.ClientId == x.ClientId && item.OrderId == x.Id)
                .OrderBy(item => item.ProductName)
                .Select(item => new
                {
                    item.ProductName,
                    item.SizeLabel,
                    item.WeightInGrams,
                    item.Quantity,
                    item.UnitPrice,
                    item.LineTotal
                }).ToList(),
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
    ClaimsPrincipal user,
    CreateExpenseRequest request,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    var validation = ValidateExpense(
        request.Category, request.Amount, request.Description);

    if (validation is not null)
        return Results.BadRequest(new { message = validation });

    var expense = new Expense(
        Guid.NewGuid(),
        clientId,
        request.Category,
        request.Amount,
        request.ExpenseDate,
        request.Description);

    db.Expenses.Add(expense);
    await db.SaveChangesAsync(ct);

    return Results.Created(
        $"/api/v1/admin/expenses/{expense.Id}",
        new
        {
            expense.Id,
            expense.Category,
            expense.Amount,
            expense.ExpenseDate,
            expense.Description,
            expense.CreatedUtc,
            expense.UpdatedUtc
        });
})
.WithName("CreateAdminExpense")
.WithTags("Admin Expenses");


adminApi.MapGet("/expenses", async (
    ClaimsPrincipal user,
    CakeShopDbContext db,
    DateOnly? fromDate,
    DateOnly? toDate,
    string? category,
    int page = 1,
    int pageSize = 10,
    CancellationToken ct = default) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    if (fromDate.HasValue && toDate.HasValue &&
        fromDate.Value > toDate.Value)
    {
        return Results.BadRequest(new
        {
            message = "From date cannot be after to date."
        });
    }

    if (page < 1 || pageSize < 1 || pageSize > 100)
    {
        return Results.BadRequest(new
        {
            message = "Page must be at least 1 and page size must be between 1 and 100."
        });
    }

    var query = db.Expenses
        .AsNoTracking()
        .Where(x => x.ClientId == clientId);

    if (fromDate.HasValue)
        query = query.Where(x => x.ExpenseDate >= fromDate.Value);

    if (toDate.HasValue)
        query = query.Where(x => x.ExpenseDate <= toDate.Value);

    if (!string.IsNullOrWhiteSpace(category))
    {
        var normalizedCategory = category.Trim();
        query = query.Where(x => x.Category == normalizedCategory);
    }

    var totalCount = await query.CountAsync(ct);

    var items = await query
        .OrderByDescending(x => x.ExpenseDate)
        .ThenByDescending(x => x.CreatedUtc)
        .ThenByDescending(x => x.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new
        {
            x.Id,
            x.Category,
            x.Amount,
            x.ExpenseDate,
            x.Description,
            x.CreatedUtc,
            x.UpdatedUtc
        })
        .ToListAsync(ct);

    return Results.Ok(new
    {
        items,
        page,
        pageSize,
        totalCount,
        totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
    });
})
.WithName("GetAdminExpenses")
.WithTags("Admin Expenses");



adminApi.MapGet("/reports/profit", async (
    DateOnly fromDate,
    DateOnly toDate,
    ClaimsPrincipal user,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    if (fromDate > toDate)
    {
        return Results.BadRequest(new
        {
            message = "From date must be on or before the to date."
        });
    }

    if (toDate == DateOnly.MaxValue)
    {
        return Results.BadRequest(new
        {
            message = "The selected end date is outside the supported reporting range."
        });
    }

    if (toDate.DayNumber - fromDate.DayNumber > 366)
    {
        return Results.BadRequest(new
        {
            message = "Select a date range of no more than 367 calendar days."
        });
    }

    var indiaZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows()
            ? "India Standard Time"
            : "Asia/Kolkata");

    var localStart = DateTime.SpecifyKind(
        fromDate.ToDateTime(TimeOnly.MinValue),
        DateTimeKind.Unspecified);

    var localEndExclusive = DateTime.SpecifyKind(
        toDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
        DateTimeKind.Unspecified);

    var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, indiaZone);
    var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEndExclusive, indiaZone);

    var completedOrders = db.CustomerOrders
        .AsNoTracking()
        .Where(x =>
            x.ClientId == clientId &&
            x.Status == CustomerOrderStatus.Completed &&
            x.CompletedUtc >= utcStart &&
            x.CompletedUtc < utcEnd);

    var completedOrderCount = await completedOrders.CountAsync(ct);

    var missingPriceCount = await completedOrders
        .CountAsync(x => x.TotalAmount == null, ct);

    var revenue = await completedOrders
        .Where(x => x.TotalAmount != null)
        .SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;

    var expenseQuery = db.Expenses
        .AsNoTracking()
        .Where(x =>
            x.ClientId == clientId &&
            x.ExpenseDate >= fromDate &&
            x.ExpenseDate <= toDate);

    var expenseCount = await expenseQuery.CountAsync(ct);

    var expenses = await expenseQuery
        .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

    var expenseBreakdown = await expenseQuery
    .GroupBy(x => x.Category)
    .Select(group => new
    {
        category = group.Key,
        amount = group.Sum(x => x.Amount),
        count = group.Count()
    })
    .OrderByDescending(x => x.amount)
    .ToListAsync(ct);

    return Results.Ok(new
    {
        fromDate = fromDate.ToString("yyyy-MM-dd"),
        toDate = toDate.ToString("yyyy-MM-dd"),
        completedOrderCount,
        completedOrdersMissingPrice = missingPriceCount,
        revenue,
        expenseCount,
        expenses,
        expenseBreakdown,
        estimatedOperatingResult = revenue - expenses,
        revenueComplete = missingPriceCount == 0
    });
})
.WithName("GetAdminProfitReport")
.WithTags("Admin Reports");



adminApi.MapGet("/expenses/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    var expense = await db.Expenses
        .AsNoTracking()
        .Where(x => x.Id == id && x.ClientId == clientId)
        .Select(x => new
        {
            x.Id,
            x.Category,
            x.Amount,
            x.ExpenseDate,
            x.Description,
            x.CreatedUtc,
            x.UpdatedUtc
        })
        .SingleOrDefaultAsync(ct);

    return expense is null
        ? Results.NotFound(new { message = "Expense not found." })
        : Results.Ok(expense);
})
.WithName("GetAdminExpense")
.WithTags("Admin Expenses");


adminApi.MapPut("/expenses/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    UpdateExpenseRequest request,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    var validation = ValidateExpense(
        request.Category, request.Amount, request.Description);

    if (validation is not null)
        return Results.BadRequest(new { message = validation });

    var expense = await db.Expenses
        .SingleOrDefaultAsync(
            x => x.Id == id && x.ClientId == clientId,
            ct);

    if (expense is null)
        return Results.NotFound(new { message = "Expense not found." });

    expense.Update(
        request.Category,
        request.Amount,
        request.ExpenseDate,
        request.Description);

    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        expense.Id,
        expense.Category,
        expense.Amount,
        expense.ExpenseDate,
        expense.Description,
        expense.CreatedUtc,
        expense.UpdatedUtc
    });
})
.WithName("UpdateAdminExpense")
.WithTags("Admin Expenses");


adminApi.MapDelete("/expenses/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    CakeShopDbContext db,
    CancellationToken ct) =>
{
    if (!TryGetClientId(user, out var clientId))
        return Results.Forbid();

    var expense = await db.Expenses
        .SingleOrDefaultAsync(
            x => x.Id == id && x.ClientId == clientId,
            ct);

    if (expense is null)
        return Results.NotFound(new { message = "Expense not found." });

    db.Expenses.Remove(expense);
    await db.SaveChangesAsync(ct);

    return Results.NoContent();
})
.WithName("DeleteAdminExpense")
.WithTags("Admin Expenses");


adminApi.MapGet("/products/{productId:guid}/variants",
    async (Guid productId, ClaimsPrincipal user,
        CakeShopDbContext db, CancellationToken ct) =>
    {
        if (!TryGetClientId(user, out var clientId))
            return Results.Forbid();

        var productExists = await db.Products.AnyAsync(
            x => x.Id == productId && x.ClientId == clientId, ct);

        if (!productExists)
            return Results.NotFound();

        var variants = await db.ProductVariants
            .AsNoTracking()
            .Where(x => x.ProductId == productId && x.ClientId == clientId)
            .OrderBy(x => x.WeightInGrams)
            .Select(x => new ProductVariantDto(
                x.Id, x.SizeLabel, x.WeightInGrams, x.Price, x.IsAvailable))
            .ToListAsync(ct);

        return Results.Ok(variants);
    });

adminApi.MapPost("/products/{productId:guid}/variants",
    async (Guid productId, ClaimsPrincipal user,
        ProductVariantWriteRequest request,
        CakeShopDbContext db, CancellationToken ct) =>
    {
        if (!TryGetClientId(user, out var clientId))
            return Results.Forbid();

        var productExists = await db.Products.AnyAsync(
            x => x.Id == productId && x.ClientId == clientId, ct);

        if (!productExists)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.SizeLabel) ||
            request.SizeLabel.Trim().Length > 30 ||
            request.WeightInGrams <= 0 || request.WeightInGrams > 100000 ||
            request.Price <= 0 || request.Price > 9999999999999999m ||
            request.Price != decimal.Round(request.Price, 2, MidpointRounding.AwayFromZero))
        {
            return Results.BadRequest(new
            {
                message = "Enter a valid size, weight and price."
            });
        }

        var weightExists = await db.ProductVariants.AnyAsync(
            x => x.ClientId == clientId &&
                 x.ProductId == productId &&
                 x.WeightInGrams == request.WeightInGrams, ct);

        if (weightExists)
            return Results.Conflict(new
            {
                message = "A variant for this weight already exists."
            });

        var variant = new ProductVariant(
            Guid.NewGuid(),
            clientId,
            productId,
            request.SizeLabel,
            request.WeightInGrams,
            request.Price,
            request.IsAvailable);

        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/v1/admin/products/{productId}/variants/{variant.Id}",
            new ProductVariantDto(
                variant.Id, variant.SizeLabel, variant.WeightInGrams,
                variant.Price, variant.IsAvailable));
    });

adminApi.MapPut("/products/{productId:guid}/variants/{variantId:guid}",
    async (Guid productId, Guid variantId, ClaimsPrincipal user,
        ProductVariantWriteRequest request,
        CakeShopDbContext db, CancellationToken ct) =>
    {
        if (!TryGetClientId(user, out var clientId))
            return Results.Forbid();

        var variant = await db.ProductVariants.SingleOrDefaultAsync(
            x => x.Id == variantId &&
                 x.ProductId == productId &&
                 x.ClientId == clientId, ct);

        if (variant is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.SizeLabel) ||
            request.SizeLabel.Trim().Length > 30 ||
            request.WeightInGrams <= 0 || request.WeightInGrams > 100000 ||
            request.Price <= 0 || request.Price > 9999999999999999m ||
            request.Price != decimal.Round(request.Price, 2, MidpointRounding.AwayFromZero))
        {
            return Results.BadRequest(new
            {
                message = "Enter a valid size, weight and price."
            });
        }

        var duplicateWeight = await db.ProductVariants.AnyAsync(
            x => x.ClientId == clientId &&
                 x.ProductId == productId &&
                 x.WeightInGrams == request.WeightInGrams &&
                 x.Id != variantId, ct);

        if (duplicateWeight)
            return Results.Conflict(new
            {
                message = "A variant for this weight already exists."
            });

        variant.Update(
            request.SizeLabel,
            request.WeightInGrams,
            request.Price,
            request.IsAvailable);

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ProductVariantDto(
            variant.Id, variant.SizeLabel, variant.WeightInGrams,
            variant.Price, variant.IsAvailable));
    });

if (args.Any(arg => string.Equals(arg, "--seed", StringComparison.OrdinalIgnoreCase)))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CakeShopDbContext>();
    await DevelopmentSeed.SeedAsync(db);
    var adminEmail = Environment.GetEnvironmentVariable("CAKESHOP_ADMIN_EMAIL");
    var adminPassword = Environment.GetEnvironmentVariable("CAKESHOP_ADMIN_PASSWORD");
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        if (adminPassword.Length < 12) throw new InvalidOperationException("CAKESHOP_ADMIN_PASSWORD must be at least 12 characters.");
        var normalizedEmail = adminEmail.Trim().ToUpperInvariant();
        if (!await db.AdminUsers.AnyAsync(x => x.ClientId == defaultClientId && x.NormalizedEmail == normalizedEmail))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();
            var temporaryUser = new AdminUser(Guid.NewGuid(), defaultClientId, adminEmail, "temporary-hash-not-used");
            var hash = hasher.HashPassword(temporaryUser, adminPassword);
            db.AdminUsers.Add(new AdminUser(temporaryUser.Id, defaultClientId, adminEmail, hash, "ShopOwner"));
            await db.SaveChangesAsync();
            Console.WriteLine($"Development admin created for {adminEmail}. Store the password securely and remove it from your shell history.");
        }
        else Console.WriteLine("Admin user already exists; seed did not reset its password.");
    }
    else Console.WriteLine("CAKESHOP_ADMIN_EMAIL / CAKESHOP_ADMIN_PASSWORD not set; skipped admin user creation.");
    Console.WriteLine("Development seed completed. Ensure EF migrations were applied first.");
    return;
}

app.Run();

static bool TryGetClientId(ClaimsPrincipal user, out Guid clientId) => Guid.TryParse(user.FindFirstValue("client_id"), out clientId) && clientId != Guid.Empty;
static string? NormalizeSlug(string? slug, string? name)
{
    var source = string.IsNullOrWhiteSpace(slug) ? name : slug;
    if (string.IsNullOrWhiteSpace(source)) return null;
    var chars = source.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
    var normalized = new string(chars);
    while (normalized.Contains("--", StringComparison.Ordinal)) normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
    return normalized.Trim('-');
}
static string? ValidateProduct(ProductWriteRequest request, Guid clientId)
{
    if (request.CategoryId == Guid.Empty) return "Category is required.";
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160) return "Product name is required and must be at most 160 characters.";
    if (request.Price < 0 || request.Price > 10000000) return "Price must be between 0 and 10,000,000.";
    if ((request.Description?.Length ?? 0) > 2000) return "Description must be at most 2,000 characters.";
    if ((request.Badge?.Length ?? 0) > 80) return "Badge must be at most 80 characters.";
    var imageUrl = request.ImageUrl?.Trim();
    var expectedTenantPrefix = $"/uploads/products/{clientId:N}/";
    var isSafeUploadedImage = !string.IsNullOrWhiteSpace(imageUrl) &&
        imageUrl.StartsWith(expectedTenantPrefix, StringComparison.Ordinal) &&
        !imageUrl.StartsWith("//", StringComparison.Ordinal) &&
        !imageUrl.Contains("..", StringComparison.Ordinal) &&
        !imageUrl.Contains('\\', StringComparison.Ordinal);
    var isHttpsImage = !string.IsNullOrWhiteSpace(imageUrl) &&
        Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) &&
        imageUri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(imageUri.UserInfo);
    if ((imageUrl?.Length ?? 0) > 1000 || (!string.IsNullOrWhiteSpace(imageUrl) && !isSafeUploadedImage && !isHttpsImage))
        return "Image URL must be an HTTPS URL or an uploaded image belonging to this shop (or empty).";
    var slug = NormalizeSlug(request.Slug, request.Name);
    if (string.IsNullOrWhiteSpace(slug) || slug.Length > 180) return "Slug is invalid or too long.";
    return null;
}

static string? ValidateExpense(
    string? category,
    decimal amount,
    string? description)
{
    if (string.IsNullOrWhiteSpace(category) ||
        category.Trim().Length > 80)
    {
        return "Category is required and must be at most 80 characters.";
    }

    if (amount <= 0 || amount > 9999999999999999m)
        return "Amount must be greater than zero and within the supported limit.";

    if (amount != decimal.Round(amount, 2, MidpointRounding.AwayFromZero))
        return "Amount cannot have more than two decimal places.";

    if (description?.Trim().Length > 500)
        return "Description must be at most 500 characters.";

    return null;
}

static string NormalizeHeroTextPosition(string? value) =>
    value is "top" or "bottom" or "left" or "right" ? value : "left";

static string? ValidateAboutSection(AboutPageSectionWriteRequest r)
{
    if (string.IsNullOrWhiteSpace(r.Eyebrow) || r.Eyebrow.Trim().Length > 100) return "Eyebrow is required and must be at most 100 characters.";
    if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 180) return "Title is required and must be at most 180 characters.";
    if (string.IsNullOrWhiteSpace(r.Body) || r.Body.Trim().Length > 3000) return "Body is required and must be at most 3000 characters.";
    if (r.SortOrder < 0 || r.SortOrder > 10000) return "Sort order must be between 0 and 10000.";
    if (r.Layout is not ("image-left" or "image-right" or "centered" or "full-bleed" or "editorial")) return "Choose a supported layout.";
    if (r.Animation is not ("none" or "fade-up" or "fade-in" or "slide-left" or "slide-right" or "zoom-in")) return "Choose a supported animation.";
    foreach (var url in new[] { r.ImageUrl, r.SecondaryImageUrl })
        if (!string.IsNullOrWhiteSpace(url) && url.Length > 1000) return "Image URLs must be at most 1000 characters.";
        else if (!string.IsNullOrWhiteSpace(url) &&
            !(url.StartsWith("/uploads/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal) && !url.Contains("..", StringComparison.Ordinal) && !url.Contains('\\')) &&
            !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)))
            return "Image URLs must be HTTPS URLs or safe uploaded paths.";
    return null;
}

static string? ValidateStoreProfile(StoreProfileWriteRequest r)
{
    if (string.IsNullOrWhiteSpace(r.Tagline) || r.Tagline.Trim().Length > 120) return "Tagline is required and must be at most 120 characters.";
    if (string.IsNullOrWhiteSpace(r.StoryTitle) || r.StoryTitle.Trim().Length > 180) return "Story title is required and must be at most 180 characters.";
    if (string.IsNullOrWhiteSpace(r.HomeDescription) || r.HomeDescription.Trim().Length > 320) return "Homepage description is required and must be at most 320 characters.";
    if (string.IsNullOrWhiteSpace(r.Story) || r.Story.Trim().Length > 3000) return "Story is required and must be at most 3000 characters.";
    if (r.StoryImageUrl?.Length > 1000 || r.LogoUrl?.Length > 1000 || r.Phone?.Length > 40 ||
        r.WhatsAppNumber?.Length > 40 || r.Email?.Length > 254 || r.Address?.Length > 500 ||
        r.OpeningHours?.Length > 500 || r.InstagramUrl?.Length > 500 || r.FacebookUrl?.Length > 500 ||
        r.WebsiteUrl?.Length > 500) return "One or more profile fields exceed their allowed length.";
    foreach (var url in new[] { r.StoryImageUrl, r.LogoUrl, r.InstagramUrl, r.FacebookUrl, r.WebsiteUrl })
        if (!string.IsNullOrWhiteSpace(url) &&
            !(url.StartsWith("/uploads/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal) && !url.Contains("..", StringComparison.Ordinal)) &&
            !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)))
            return "Image and social URLs must be safe relative upload paths or HTTPS URLs.";
    if (!string.IsNullOrWhiteSpace(r.Email) && !System.Net.Mail.MailAddress.TryCreate(r.Email, out _)) return "Enter a valid email address.";
    return null;
}

static string? ValidateStoreCollection(StoreCollectionWriteRequest request)
{
    if (request.Kind is not ("Hamper" or "Seasonal")) return "Kind must be Hamper or Seasonal.";
    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 160) return "Title is required and must be at most 160 characters.";
    if (string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Trim().Length > 180 ||
        request.Slug.Trim().Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-'))) return "Slug may contain only letters, numbers and hyphens.";
    if (request.Description?.Length > 2000) return "Description must be at most 2000 characters.";
    if (request.ImageUrl?.Length > 1000 || request.NavigationLabel?.Length > 60) return "Image URL or navigation label is too long.";
    if (request.ImageUrl is { Length: > 0 } image && !(image.StartsWith("/uploads/", StringComparison.Ordinal) && !image.StartsWith("//", StringComparison.Ordinal) && !image.Contains("..", StringComparison.Ordinal) && !image.Contains('\\')) &&
        !(Uri.TryCreate(image, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo))) return "Image URL must be a safe relative upload path or HTTPS URL.";
    if (request.SortOrder < 0 || request.SortOrder > 10000) return "Sort order must be between 0 and 10000.";
    if ((request.Highlights?.Trim().Length ?? 0) > 4000) return "Highlights must be at most 4000 characters.";
    if (request.BundlePrice.HasValue && (request.Kind != "Hamper" || request.BundlePrice <= 0 || request.BundlePrice > 10000000 || request.BundlePrice != decimal.Round(request.BundlePrice.Value, 2, MidpointRounding.AwayFromZero))) return "Bundle price must be a valid positive amount for hampers only.";
    if (request.StartsUtc.HasValue && request.EndsUtc.HasValue && request.EndsUtc <= request.StartsUtc) return "End date must be after start date.";
    return null;
}

static string? ValidateHeroBanner(HeroBannerWriteRequest request)
{
    if ((request.Title?.Trim().Length ?? 0) > 160)
        return "Title must be at most 160 characters.";
    if ((request.Subtitle?.Trim().Length ?? 0) > 500)
        return "Subtitle must be at most 500 characters.";
    if (request.TextPosition is not (null or "top" or "bottom" or "left" or "right"))
        return "TextPosition must be top, bottom, left, or right.";
    if (request.MediaType is not ("Image" or "Video"))
        return "MediaType must be Image or Video.";
    if (!IsAllowedHeroMediaUrl(request.MediaUrl) ||
        (!string.IsNullOrWhiteSpace(request.MobileMediaUrl) && !IsAllowedHeroMediaUrl(request.MobileMediaUrl)))
        return "Media URLs must be valid HTTPS URLs.";
    if (request.ButtonText?.Length > 80 || request.ButtonUrl?.Length > 2000)
        return "Button text or URL exceeds its allowed length.";
    if (!string.IsNullOrWhiteSpace(request.ButtonUrl) && !IsAllowedHeroDestination(request.ButtonUrl))
        return "Button destination must be a relative path or HTTPS URL.";
    if (request.StartsUtc.HasValue && request.EndsUtc.HasValue && request.EndsUtc <= request.StartsUtc)
        return "End date must be after start date.";
    if (request.SortOrder < 0 || request.SortOrder > 10000)
        return "Sort order must be between 0 and 10000.";
    return null;
}

static bool IsAllowedHeroMediaUrl(string value) =>
    (value.StartsWith("/uploads/heroes/", StringComparison.Ordinal) &&
     !value.StartsWith("//", StringComparison.Ordinal) &&
     !value.Contains("..", StringComparison.Ordinal) &&
     !value.Contains('\\', StringComparison.Ordinal)) ||
    (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
     uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo));

static bool IsAllowedHeroFileSignature(string extension, byte[] header, int read)
{
    if (extension is ".jpg" or ".jpeg")
        return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    if (extension == ".png")
        return read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
               header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
    if (extension == ".webp")
        return read >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
               header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
    if (extension == ".mp4")
        return read >= 8 && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p';
    if (extension == ".webm")
        return read >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;
    return false;
}

static bool IsAllowedHeroDestination(string value) =>
    (value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal)) ||
    (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
     uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo));

public sealed record LoginRequest(string? Email, string? Password);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresUtc, string Email, string Role);
public sealed record CategoryWriteRequest(string? Name, string? Slug, int SortOrder = 0, bool IsActive = true);
public sealed record AdminCategoryDto(Guid Id, string Name, string Slug, int SortOrder, bool IsActive);
public sealed record ProductWriteRequest(Guid CategoryId, string? Name, string? Slug, string? Description, decimal Price, string? ImageUrl, string? Badge, bool IsFeatured, bool IsAvailable = true);
public sealed record AdminProductDto(Guid Id, Guid CategoryId, string Name, string Slug, string Description, decimal Price, string? ImageUrl, string? Badge, bool IsFeatured, bool IsAvailable, string CategoryName);

public sealed record CreateCustomerOrderRequest(
    Guid ProductId,
    string? CustomerName,
    string? CustomerPhone,
    int Quantity,
    string? CakeSize,
    DateOnly? PreferredDate,
    string? Customization,
    Guid? VariantId,
    string? VerificationToken = null);

public sealed record UpdateCustomerOrderStatusRequest(
    string? Status);

public partial class Program { }


public sealed record CreateExpenseRequest(
    string? Category,
    decimal Amount,
    DateOnly ExpenseDate,
    string? Description);

public sealed record UpdateExpenseRequest(
    string? Category,
    decimal Amount,
    DateOnly ExpenseDate,
    string? Description);



public sealed record ProductVariantDto(
    Guid Id,
    string SizeLabel,
    int WeightInGrams,
    decimal Price,
    bool IsAvailable);

public sealed record ProductVariantWriteRequest(
    string? SizeLabel,
    int WeightInGrams,
    decimal Price,
    bool IsAvailable);


public sealed record VerifyRazorpayPaymentRequest(
    Guid PaymentTransactionId,
    string RazorpayPaymentId,
    string RazorpaySignature);

public sealed record HeroBannerWriteRequest(
    string? Title,
    string? Subtitle,
    string MediaType,
    string MediaUrl,
    string? MobileMediaUrl,
    string? ButtonText,
    string? ButtonUrl,
    int SortOrder,
    DateTime? StartsUtc,
    DateTime? EndsUtc,
    string? TextPosition = null);

public sealed record HeroBannerDto(
    Guid Id,
    string Title,
    string Subtitle,
    string TextPosition,
    string MediaType,
    string MediaUrl,
    string? MobileMediaUrl,
    string? ButtonText,
    string? ButtonUrl,
    int SortOrder,
    DateTime? StartsUtc,
    DateTime? EndsUtc,
    bool IsPublished = false);

public sealed record AboutPageSectionDto(Guid Id, string Eyebrow, string Title, string Body, string? ImageUrl,
    string? SecondaryImageUrl, string Layout, string Animation, int SortOrder, bool IsPublished);
public sealed record AboutPageSectionWriteRequest(string? Eyebrow, string? Title, string? Body, string? ImageUrl,
    string? SecondaryImageUrl, string? Layout, string? Animation, int SortOrder, bool IsPublished);

public sealed record StoreProfileDto(Guid ClientId, string Name, string Slug, string Tagline, string StoryTitle,
    string HomeDescription, string Story, string? StoryImageUrl, string? LogoUrl, string? Phone, string? WhatsAppNumber, string? Email,
    string? Address, string? OpeningHours, string? InstagramUrl, string? FacebookUrl, string? WebsiteUrl);

public sealed record StoreProfileWriteRequest(string? Tagline, string? StoryTitle, string? HomeDescription, string? Story,
    string? StoryImageUrl, string? LogoUrl, string? Phone, string? WhatsAppNumber, string? Email,
    string? Address, string? OpeningHours, string? InstagramUrl, string? FacebookUrl, string? WebsiteUrl);

public sealed record StoreCollectionWriteRequest(string? Kind, string? Title, string? Slug, string? Description,
    string? ImageUrl, string? NavigationLabel, bool ShowInNavigation, int SortOrder,
    DateTime? StartsUtc, DateTime? EndsUtc, bool IsPublished, decimal? BundlePrice = null, string? Highlights = null);

