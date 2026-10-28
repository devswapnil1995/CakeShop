# The Sweet Panda — Release 1 implementation starter

Premium cream/chocolate Angular storefront retained from the approved UI, now accompanied by a .NET 10 / EF Core / SQL Server API foundation. The public catalog calls the API when it is running and gracefully keeps the original sample catalog visible if the API is offline.

## Included
- `web/cake-shop-ui`: Angular 21 storefront, WhatsApp CTA, API client and local development proxy.
- `src/CakeShop.Domain`: shop, category and product domain entities.
- `src/CakeShop.Application`: DTOs and application abstractions.
- `src/CakeShop.Infrastructure`: EF Core SQL Server DbContext and repeatable local seed.
- `src/CakeShop.Api`: public shop/catalog endpoints, JWT-based admin login, tenant-scoped category/product management endpoints and database health.
- `docker-compose.yml`: local SQL Server 2022 Developer container (development only).

## Important implementation status
This is a Release 1 implementation starter, not yet a production-ready deployment. The environment that prepared these files did not have the .NET SDK installed, so the .NET solution has not been compiled or run here. Angular dependency installation timed out, so its production build is also unverified. No EF migration is checked in; create one with the commands below. The project includes a dedicated Angular admin UI and JWT-protected catalog APIs. Real shop contact details, automated tests, Azure deployment and final production security hardening remain launch requirements. The development signing key is only for local development and must never be used in production.

## Prerequisites
- .NET 10 SDK
- Node.js compatible with Angular 21 and npm
- Docker Desktop, or an existing SQL Server instance

## 1. Start SQL Server locally
Copy `.env.example` to `.env`, replace the example value with a strong development-only password, then run:

```bash
docker compose up -d sqlserver
```

Do not reuse this password outside local development. If you use an existing SQL Server instead, configure its connection string through environment or user secrets.

## 2. Configure the connection string
From the repository root, set `ConnectionStrings__DefaultConnection` (or `CAKESHOP_SQL_CONNECTION`) to a SQL Server connection string. For the included Docker service, a typical local-only example is:

```text
Server=localhost,1433;Database=TheSweetPandaDb;User Id=sa;Password=<your-local-password>;Encrypt=True;TrustServerCertificate=True
```

Avoid committing credentials to source control. Prefer .NET user-secrets for local runs or Key Vault / managed identity in Azure.

## 3. Create the initial EF migration and database
Run from the repository root:

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet restore CakeShop.sln
dotnet ef migrations add InitialCreate --project src/CakeShop.Infrastructure --startup-project src/CakeShop.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/CakeShop.Infrastructure --startup-project src/CakeShop.Api
```

Set `Shop__DefaultClientId` to `c8ef8d24-5d9b-4e2e-91e2-79f4b0e9a001` (this is a stable public shop identifier, not a secret). EF tooling uses a design-time factory, so migrations do not need to boot the API or load JWT settings. Then run the repeatable seed once:

```bash
dotnet run --project src/CakeShop.Api -- --seed
```

Do not enable automatic production migrations as a substitute for a controlled deployment migration process.

## 4. Create a development admin and run the API
The `Development` launch profile uses a **local-only** signing key in `appsettings.Development.json`. Never copy that key to staging or production. Before running the seed, set an admin email and a strong password (12+ characters) in your shell. For example in PowerShell:

```powershell
$env:CAKESHOP_ADMIN_EMAIL = "admin@example.com"
$env:CAKESHOP_ADMIN_PASSWORD = "use-a-unique-long-development-password"
dotnet run --project src/CakeShop.Api -- --seed
```

The seed creates an admin only when both environment variables are present. It never resets an existing admin password. Do not put real production credentials in shell history or source control. In production, set `CAKESHOP_JWT_SIGNING_KEY` to a securely generated secret of at least 32 UTF-8 bytes and configure issuer/audience via `Jwt__Issuer` and `Jwt__Audience`. Also configure a production SQL connection string and restrict allowed frontend origins in the hosting environment/network configuration.

Run the API:

```bash
dotnet run --project src/CakeShop.Api --launch-profile http
```
Expected endpoints:
- `GET http://localhost:5080/health`
- `GET http://localhost:5080/api/v1/shop`
- `GET http://localhost:5080/api/v1/categories`
- `GET http://localhost:5080/api/v1/products`
- Optional filters: `?category=chocolate` or `?featured=true`
- `POST /api/v1/admin/auth/login` (anonymous login; returns a short-lived JWT)
- `GET/POST /api/v1/admin/categories`, `PUT /api/v1/admin/categories/{id}` (Bearer token required)
- `GET/POST /api/v1/admin/products`, `PUT /api/v1/admin/products/{id}` (Bearer token required)

Login JSON: `{ "email": "admin@example.com", "password": "your-password" }`. Send the returned `accessToken` as `Authorization: Bearer <token>` to admin endpoints. Product/category writes are scoped to the `client_id` claim in the validated token; clients cannot choose another shop by submitting a `ClientId`. Tokens expire after 30 minutes by default. For production, add refresh/session revocation, rate limiting on login, audit logging, restrictive CORS, HTTPS enforcement and monitoring before launch.

The shop ID is resolved from server configuration for this single-shop MVP; it is not accepted from the browser as an authorization input. For future multi-tenancy, replace the default-shop resolver with authenticated membership/domain-based tenant resolution before exposing tenant-specific data.

## 5. Run Angular
```bash
cd web/cake-shop-ui
npm install
npm start
```
The dev proxy sends `/api` requests to `http://localhost:5080`. Replace the placeholder WhatsApp number in `src/app/app.component.ts` with the shop's actual WhatsApp number in international format, digits only, before launch.

## Architecture / security notes
- All catalog records include `ClientId`; product-to-category relationship uses a composite tenant-aware foreign key.
- Public catalog endpoints are read-only; authenticated admin endpoints support category/product create and update.
- This is currently a single-shop deployment. Multi-shop onboarding, tenant membership management, audit logs and subscription billing are not implemented.
- Public endpoints return only active/available catalog data. Apply rate limiting, security headers, monitoring, proper secret storage and production CORS restrictions before launch.

## Azure Blob Storage for media assets

Uploaded product, collection, About page, store-profile and hero media are stored in the existing Azure Storage container named `image`, under the virtual `assets/` folder. Upload paths are grouped by purpose and tenant, for example `assets/products/{clientId}/...`, `assets/collections/{clientId}/...`, `assets/about/{clientId}/...`, `assets/profile/{clientId}/...` and `assets/heroes/{clientId}/...`.

Configure the API with these settings:
- `AzureBlobStorage:AccountName`: storage account name.
- `AzureBlobStorage:ContainerName`: `image` (default).
- `AzureBlobStorage:AccountKey`: the storage account access key, supplied only through local user-secrets or the hosting environment; never commit it or expose it to Angular.

For local development, from the API project directory run `dotnet user-secrets set "AzureBlobStorage:AccountKey" "<your-access-key>"`. Set `AzureBlobStorage__AccountName`, `AzureBlobStorage__ContainerName`, and `AzureBlobStorage__AccountKey` in the API hosting environment (or use Key Vault). Do not place the access key in frontend environment files. The container must already exist. Public storefront image URLs require the `image` container to allow anonymous blob reads; otherwise use a private-container/SAS delivery design.

Existing database rows may still contain legacy `/uploads/...` URLs. Those files must be copied to Blob Storage and the corresponding image URLs updated before removing legacy URL compatibility; this code change moves new uploads to Blob Storage and does not automatically migrate previously uploaded files.

## Admin dashboard (Release 1)

Once the API/database are running and a development admin has been seeded, open `http://localhost:4200/admin` to use the Angular admin dashboard. The public storefront remains at `http://localhost:4200/`.

The dashboard provides:
- Email/password sign-in against `POST /api/v1/admin/auth/login`.
- Product listing, create and update forms, including price, category, image URL, badge, featured status, and availability.
- Category listing, create and update forms, including slug, display order and active status.
- Access tokens held in browser `sessionStorage` and attached to admin API calls. This reduces persistence across browser sessions but does not replace XSS protections; deploy only with HTTPS and a strict content-security policy.

The admin account is created only when `CAKESHOP_ADMIN_EMAIL` and `CAKESHOP_ADMIN_PASSWORD` are configured for the seed command. Use a strong, unique password (at least 12 characters), keep the JWT signing key in a secret store/environment variable, and never commit production secrets. The dashboard does not implement password reset, refresh tokens, or user management yet.

### Release 1 validation note

This environment does not have a usable .NET SDK and package installation for Angular timed out, so the API and UI have not been compiled or end-to-end tested here. Run `npm install` and `npm run build` in `web/cake-shop-ui`, and `dotnet build CakeShop.sln` from the repository root in a .NET 10 environment before deployment. Resolve any compiler or runtime errors found by those checks before launch.


## Release 1 launch checklist

Use [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md) as the sign-off list. Do not launch publicly until build and smoke tests pass in a .NET 10 / Node environment and the real shop contact details have replaced the placeholders.
