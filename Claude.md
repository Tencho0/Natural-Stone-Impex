# CLAUDE.md — Natural Stone Impex

## Project Overview

Inventory and order management system for a Bulgarian building materials shop called **Natural Stone Impex**. Two parts: a public customer storefront (no auth) and an admin panel (JWT auth, single user).

**Full requirements**: `docs/technical-specification.md`
**Business context**: `docs/prd.md`
**Database design**: `docs/database-schema.md`
**API contract**: `docs/api-endpoints.md`
**Current progress & task breakdown**: `planning/overview.md` and `planning/epics/`

## Tech Stack

- **Frontend**: Blazor WebAssembly (Standalone) + Bootstrap 5
- **Backend**: ASP.NET Core 8 Web API
- **Database**: SQL Server + Entity Framework Core (Code First)
- **Auth**: JWT Bearer tokens (single admin user, seeded via DbSeeder)
- **Images**: Local file storage (`wwwroot/uploads/products/`)
- **Receipt printing**: HTML + `window.print()` via JS interop

## Commands

```bash
# Run API (from repo root)
dotnet run --project src/NaturalStoneImpex.Api

# Run Blazor client (from repo root)
dotnet run --project src/NaturalStoneImpex.Client

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project src/NaturalStoneImpex.Api

# Apply migrations
dotnet ef database update --project src/NaturalStoneImpex.Api

# Build entire solution
dotnet build

# Run tests
dotnet test
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

### Blazor Client
- Service interfaces (`IProductService`, `IOrderService`, etc.) with implementations in `Services/`
- `HttpClient` configured with API base URL and automatic Bearer token attachment
- `CustomAuthStateProvider` manages JWT auth state
- Cart state managed by `CartService` (singleton — persists within browser tab session)
- Use `EditForm` with `DataAnnotationsValidator` for all forms
- Bootstrap 5 classes for all styling — no custom CSS unless necessary
- Loading spinners for async operations
- Error handling: catch API errors and display Bulgarian messages via Bootstrap alerts

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
├── NaturalStoneImpex.Api/
│   ├── Controllers/          # API endpoints (thin, delegate to services)
│   ├── Data/
│   │   ├── AppDbContext.cs   # EF Core context with all DbSets
│   │   └── Seed/DbSeeder.cs # Seeds admin user and sample data
│   ├── Middleware/           # Exception handling, etc.
│   ├── Migrations/           # EF Core migrations (auto-generated)
│   ├── Models/
│   │   ├── Entities/         # EF Core entity classes
│   │   └── DTOs/             # Request/response DTOs
│   ├── Services/             # Business logic layer
│   └── wwwroot/uploads/      # Product images (gitignored)
│
└── NaturalStoneImpex.Client/
    ├── Auth/                 # CustomAuthStateProvider
    ├── Components/           # Shared Blazor components (ProductCard, Pagination, etc.)
    ├── Layout/               # MainLayout (public), AdminLayout (admin)
    ├── Models/               # Client-side DTOs / view models
    ├── Pages/
    │   ├── Public/           # Home, Catalog, ProductDetail, Cart, Checkout, Contacts
    │   └── Admin/            # Login, Dashboard, Products, Categories, Orders, Invoices, Receipt
    ├── Services/             # API client services (IProductService, etc.)
    └── wwwroot/              # Static assets, index.html, CSS
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
- Create the service interface and implementation in client `Services/`
- Create the page in the correct subfolder (`Pages/Public/` or `Pages/Admin/`)
- Add `@attribute [Authorize]` for admin pages
- All text in Bulgarian
- Use Bootstrap 5 classes for layout and components
- Handle loading, empty, and error states
