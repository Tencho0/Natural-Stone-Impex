# CLAUDE.md — Natural Stone Impex

## Project Overview

Inventory and order management system for a Bulgarian building materials shop called **Natural Stone Impex**. Two parts: a public customer storefront (no auth) and an admin panel (JWT auth, single user).

**Full requirements**: `docs/technical-specification.md`
**Business context**: `docs/product-requirements.md`
**Database design**: `docs/database-schema.md`
**API contract**: `docs/api-endpoints.md`
**Strict rules**: `docs/conventions.md` — READ THIS BEFORE WRITING ANY CODE
**Current progress & task breakdown**: `planning/overview.md` and `planning/epics/`

## Tech Stack

- **Host**: one Blazor Web App. `NaturalStoneImpex.Api` hosts the API controllers and the Razor components (`Components/App.razor`); `NaturalStoneImpex.Client` is its WebAssembly project. One process, one origin, no CORS.
- **Frontend**: storefront pages are static SSR in `Api/Components` (they opt out with `@attribute [ExcludeFromInteractiveRouting]`); admin pages route as interactive WebAssembly from `NaturalStoneImpex.Client`; the visualizer is a WebAssembly island on a static SSR page. Bootstrap 5 mapped to the design tokens (`wwwroot/css/nsi/`). Storefront pages never load the .NET WebAssembly runtime.
- **Backend**: ASP.NET Core 10 Web API (.NET 10 LTS, all projects target `net10.0`)
- **Database**: SQL Server + Entity Framework Core (Code First)
- **Auth**: single admin user (seeded via DbSeeder). The browser uses an HttpOnly cookie set by the static SSR page `/admin/login`; API clients can still use JWT Bearer tokens from `POST /api/auth/login`. A policy scheme picks Bearer when an `Authorization` header is present.
- **Images**: Local file storage (`wwwroot/uploads/products/`)
- **Receipt printing**: HTML + `window.print()` via JS interop

## Commands

```bash
# Run the app: API, storefront and admin on https://localhost:5001 (from repo root)
dotnet run --project src/NaturalStoneImpex.Api

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project src/NaturalStoneImpex.Api

# Apply migrations
dotnet ef database update --project src/NaturalStoneImpex.Api

# Build entire solution
dotnet build

# Run tests
dotnet test

# Download visualizer ONNX models (one-time, required for the visualizer feature)
powershell -File scripts/download-visualizer-models.ps1
```

## Conventions

### Language & Localization
- **All UI text must be in Bulgarian.** Every label, button, message, placeholder, validation error, and tooltip must be in Bulgarian.
- No i18n framework — hardcoded Bulgarian strings are fine for V1.
- Date format: `DD.MM.YYYY` (e.g., `18.02.2026`)
- Currency: Euro — display as `XX.XX €` (symbol after the number, space before €)

### Pricing & VAT
- Every product stores three price fields: `PriceWithoutVat`, `VatAmount`, `PriceWithVat` (all `decimal(18,2)`)
- All three are entered by the admin. The form auto-calculates `PriceWithVat = PriceWithoutVat + VatAmount` but the admin can override.
- Validation: `PriceWithVat` must equal `PriceWithoutVat + VatAmount`
- Prices stored and displayed in EUR with exactly 2 decimal places

### Units of Measurement
- Enum `UnitType`: `Kg = 0`, `Sqm = 1`
- Display as: `"кг"` and `"м²"`
- Stock quantities are `decimal(18,2)` to support fractional amounts (e.g., 2.5 кг)

### Order Numbers
- Auto-generated format: `NSI-YYYYMMDD-XXXX`
- Example: `NSI-20260218-0001`
- Sequential per day, zero-padded to 4 digits

### Data Architecture
- **OrderItem stores price snapshots** — copy product prices at time of order. Never reference live product prices for historical orders.
- **Invoices are immutable** — no edit or delete after creation.
- **Soft delete for products** — set `IsActive = false`, never hard delete (preserves order history).
- **Stock is decremented on order confirmation**, not on order placement.
- **Stock is incremented on invoice creation** (automatic).

### API Design
- Controllers are thin — business logic lives in `Services/` layer
- Never expose EF entities directly in API responses — always use DTOs from `Models/DTOs/`
- Consistent error response format: `{ "error": "Bulgarian error message" }`
- Pagination response format: `{ "items": [...], "totalCount": N, "page": N, "pageSize": N, "totalPages": N }`
- All admin endpoints require `[Authorize]` attribute
- Public endpoints (catalog, product detail, place order) require no auth

### Storefront (static SSR, `Api/Components`)
- Pages inject the API services (`IProductService`, `IOrderService`, `IStorefrontQueryService`, ...) and call them in-process. No HTTP hop, no typed clients on the server. Never run parallel queries on the scoped `DbContext`.
- Forms are `EditForm`s with `Enhance`, `FormName`, `[SupplyParameterFromForm]` and `DataAnnotationsValidator`, validated on the server, then Post/Redirect/Get. Every page works without JavaScript.
- Small vanilla modules in `Client/wwwroot/js/storefront/` attach through `PageScript` (`onLoad`, `onUpdate`, `onDispose`). Keep them under 8 KB brotli in total (a test checks it).
- Cart: the `nsi_cart` cookie (`productId:quantity` pairs only), read and written by `CartStore`. Prices always come from the database. Products priced 0 cannot be added.
- Contact details come from `ShopInfo` (Client project, shared with the admin receipt). Money and quantities go through `PriceFormat`.
- Links to the admin use `data-enhance-nav="false"`.

### Admin and visualizer (WebAssembly, `NaturalStoneImpex.Client`)
- Service interfaces (`IProductService`, `IOrderService`, etc.) with implementations in `Services/`. They call the API over HTTP on the same origin.
- The `HttpClient` sends the admin cookie automatically. `CsrfHeaderHandler` adds `X-NSI-Request: 1`, which the server requires on cookie-authenticated state-changing `/api` calls and on `POST /api/cart/items`.
- `CookieAuthStateProvider` asks `GET /api/auth/me` who is signed in; logout calls `POST /api/auth/logout`.
- The visualizer adds to the cart through `ICartApi` (`POST /api/cart/items`).
- Use `EditForm` with `DataAnnotationsValidator` for all forms.
- Loading states for async operations; catch API errors and show Bulgarian messages.
- Links to the storefront use `forceLoad: true` or `target="_top"`.

### Styling (both)
- Follow `.claude/skills/frontend-design/SKILL.md` and the redesign spec: tokens in `css/nsi/tokens.css`, Geologica and Wix Madefor Text (self-hosted), 2px radius, hairlines, no shadows.
- Stylesheets: `tokens.css`, `base.css` everywhere; `components.css` and `storefront.css` on static SSR pages; `visualizer.css` on `/visualizer`; `admin.css` on the WebAssembly admin pages and the login.

### Code Style
- C# naming: PascalCase for public members, camelCase for local variables, _camelCase for private fields
- Async/await throughout — no `.Result` or `.Wait()` blocking calls
- Use `var` when the type is obvious from context
- Nullable reference types enabled
- DTOs use `record` types where appropriate

### Database
- EF Core Code First approach
- Connection string in `appsettings.json` under `ConnectionStrings:DefaultConnection`
- All relationships configured explicitly in `AppDbContext.OnModelCreating()`
- Decimal precision: `decimal(18,2)` for all price and quantity fields
- Timestamps: `CreatedAt` and `UpdatedAt` on entities that need them (auto-set)

### Admin Credentials (Development)
- Username: `admin`
- Password: `Admin123!`
- Seeded by `Data/Seed/DbSeeder.cs` on first run

## File Structure Reference

```
src/
├── NaturalStoneImpex.Api/    # The Blazor Web App host (run this project)
│   ├── Program.cs            # Host wiring: auth, API, Razor components, static assets
│   ├── Auth/                 # Auth schemes, ReturnUrl validation, CSRF header middleware
│   ├── Components/
│   │   ├── App.razor         # Root HTML document; picks the render mode per page
│   │   ├── _Imports.razor
│   │   ├── Layout/           # MainLayout (storefront), LoginLayout
│   │   ├── Shared/           # Storefront components (SeoHead, ProductCard, Pagination, Toast, ...)
│   │   └── Pages/            # Static SSR storefront pages; Visualizer (SSR + island); Admin/Login.razor
│   ├── Controllers/          # API endpoints (thin, delegate to services)
│   ├── Endpoints/            # Minimal API endpoints (cart form posts, /api/cart/items, sitemap, robots)
│   ├── Storefront/           # Pure storefront classes (cart cookie, CartStore, calculator, checkout, SEO)
│   ├── Data/
│   │   ├── AppDbContext.cs   # EF Core context with all DbSets
│   │   └── Seed/DbSeeder.cs # Seeds admin user and sample data
│   ├── Middleware/           # /api exception handler, storefront error pages, HEAD as GET, /uploads static files
│   ├── Migrations/           # EF Core migrations (auto-generated)
│   ├── MLModels/             # Visualizer ONNX models (downloaded, copied to the output)
│   ├── Models/
│   │   ├── Entities/         # EF Core entity classes
│   │   └── DTOs/             # Request/response DTOs
│   ├── Services/             # Business logic layer (Segmentation/ for the visualizer)
│   └── wwwroot/uploads/      # Runtime data, gitignored, not a static web asset, not published; served from disk by UseUploadsStaticFiles
│
└── NaturalStoneImpex.Client/ # The Web App's WebAssembly project (not run on its own)
    ├── Program.cs            # WebAssembly services: HttpClient, auth state, API clients
    ├── Routes.razor          # Router (interactive routing for admin pages)
    ├── ShopInfo.cs           # Shop contact details, shared by the storefront and the receipt
    ├── Auth/                 # CookieAuthStateProvider, CsrfHeaderHandler
    ├── Components/           # Admin components (Pagination, InlineConfirm, ...), VisualizerApp island
    ├── Formatting/           # PriceFormat and other formatters, shared by server and WebAssembly
    ├── Layout/               # AdminLayout, PrintLayout (receipt)
    ├── Models/               # Client-side DTOs
    ├── Pages/Admin/          # Dashboard, Products, Categories, Orders, Invoices, Receipt
    ├── Services/             # API client services (IProductService, ICartApi, ...)
    └── wwwroot/              # All static assets (css/nsi, fonts, js, Bootstrap), served at the site root
```

## Working With This Codebase

When starting a task:
1. Read this file for conventions
2. Read the relevant epic file in `planning/epics/` for acceptance criteria
3. Reference `docs/technical-specification.md` for detailed feature behavior
4. Reference `docs/database-schema.md` for entity definitions
5. Reference `docs/api-endpoints.md` for API contract

When creating new entities, always:
- Add the entity in `Models/Entities/`
- Add the `DbSet` in `AppDbContext`
- Configure relationships in `OnModelCreating`
- Create a migration
- Create corresponding DTOs in `Models/DTOs/`

When creating new API endpoints, always:
- Create/update the service interface and implementation in `Services/`
- Create the controller action in `Controllers/`
- Use DTOs for request/response — never expose entities
- Add `[Authorize]` for admin endpoints
- Return consistent error format

When creating new Blazor pages, always:
- Storefront: create the page in `Api/Components/Pages/` with `@attribute [ExcludeFromInteractiveRouting]`, inject the API services directly, and add `SeoHead`
- Admin: create the page in `Client/Pages/Admin/`, with the service interface and implementation in client `Services/`
- Add `@attribute [Authorize]` for admin pages
- All text in Bulgarian
- Use Bootstrap 5 classes for layout and components
- Handle loading, empty, and error states
