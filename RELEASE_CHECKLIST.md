# The Sweet Panda — Release 1 Launch Checklist

## A. Shop information and customer experience
- [ ] Replace `WHATSAPP_NUMBER` in `web/cake-shop-ui/src/app/app.component.ts` with the real shop WhatsApp number (country code + number, digits only).
- [ ] Replace the placeholder shop address and opening hours in `src/app/app.component.html`.
- [ ] Confirm all product names, descriptions, prices, availability, badges and images with the shop owner.
- [ ] Confirm product prices are displayed with the intended tax/delivery wording; the current storefront uses “From ₹…”.
- [ ] Test WhatsApp actions on desktop and mobile, including custom cake and individual product messages.
- [ ] Verify image licensing/usage rights and ensure every image has useful alt text.
- [ ] Review responsive layouts, keyboard navigation, contrast and empty/error states on mobile and desktop.

## B. Build and database verification
- [ ] Install the .NET 10 SDK and a supported Node.js version for Angular 21.
- [ ] Run `dotnet restore CakeShop.sln` and `dotnet build CakeShop.sln`; resolve all errors and warnings that affect launch.
- [ ] Run `npm install` and `npm run build` from `web/cake-shop-ui`; resolve all build errors.
- [ ] Configure a local SQL Server connection string and generate/apply the `InitialCreate` EF migration.
- [ ] Run the development seed and verify the shop, categories, products and admin user are created.
- [ ] Confirm `GET /health`, `/api/v1/shop`, `/api/v1/categories`, and `/api/v1/products` return expected responses.
- [ ] Test admin login, invalid credentials, expired/invalid token, create/update product, create/update category and duplicate slug handling.
- [ ] Confirm creating an unavailable product preserves `isAvailable=false`, and it does not appear in the public catalogue.
- [ ] Verify a product cannot reference a category from another shop; do not expose multi-tenant onboarding until server-side tenant resolution is implemented.

## C. Production security and operations
- [ ] Use HTTPS only; configure a production-grade signing key through a secret store, never the Development settings file.
- [ ] Configure production SQL access using managed identity/least privilege where supported; do not ship SQL `sa` credentials.
- [ ] Configure restrictive CORS for the actual production frontend origin(s).
- [ ] Add login rate limiting, security headers/CSP, audit logging and monitoring before public launch.
- [ ] Review backup/restore, database migration/rollback and health alert procedures.
- [ ] Confirm admin credentials are unique, strong, and not stored in source control or deployment logs.
- [ ] Review privacy notice, terms, contact details, and applicable local tax/business requirements.

## D. Current known limitations
- No EF migration is committed; generate it from the current model before the first database update.
- Automated unit/integration tests and a CI pipeline are not included yet.
- Password reset, refresh/revocation, admin user management, image upload, orders/checkout and subscriptions are not included in Release 1.
- The current backend is configured for one default shop. Future multi-shop support requires server-side tenant resolution and membership/authorization rules before launch.
- Source-level changes were reviewed, but this environment did not have the .NET SDK and Angular package installation timed out; compilation and end-to-end validation remain pending.
