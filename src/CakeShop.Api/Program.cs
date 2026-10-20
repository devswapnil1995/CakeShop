using CakeShop.Api.Contracts;
using CakeShop.Application.Catalog;
using CakeShop.Domain.Catalog;
using CakeShop.Domain.Expenses;
using CakeShop.Domain.Identity;
using CakeShop.Domain.Orders;
using CakeShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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
builder.Services.AddCors(options => options.AddPolicy("AngularDev", policy => policy.WithOrigins("http://localhost:4200", "https://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
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


var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("AngularDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();

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
    var result = await query.OrderBy(x => x.cat.SortOrder).ThenBy(x => x.product.Name)
        .Select(x => new ProductDto(x.product.Id, x.product.Name, x.product.Slug, x.product.Description, x.product.Price, x.product.ImageUrl, x.product.Badge, x.product.IsFeatured, x.cat.Name, x.cat.Slug)).ToListAsync(ct);
    return Results.Ok(result);
}).WithName("GetProducts").WithTags("Catalog");


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
        return Results.BadRequest(new { message = "Select an available cake from this shop." });

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
        cakeSize,
        request.PreferredDate,
        customization,
        product.Price);

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
            x.UpdatedUtc
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
    var validation = ValidateProduct(request);
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
    var validation = ValidateProduct(request);
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
static string? ValidateProduct(ProductWriteRequest request)
{
    if (request.CategoryId == Guid.Empty) return "Category is required.";
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160) return "Product name is required and must be at most 160 characters.";
    if (request.Price < 0 || request.Price > 10000000) return "Price must be between 0 and 10,000,000.";
    if ((request.Description?.Length ?? 0) > 2000) return "Description must be at most 2,000 characters.";
    if ((request.Badge?.Length ?? 0) > 80) return "Badge must be at most 80 characters.";
    if ((request.ImageUrl?.Length ?? 0) > 1000 || (!string.IsNullOrWhiteSpace(request.ImageUrl) && (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))) return "Image URL must be a valid HTTPS URL (or empty).";
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
    string? Customization);

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
