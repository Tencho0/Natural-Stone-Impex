# Epic 01: Project Setup & Scaffolding

## Description
Set up the solution structure, configure both projects (API and Blazor client), establish database connection, and verify everything runs end-to-end. This is the foundation — nothing else can start until this is done.

## Dependencies
None — this is the first epic.

## Stories

---

### Story 1.1: Create Solution and Project Structure

**As** a developer, **I want** the solution scaffolded with the correct project structure **so that** I can start building features immediately.

**Acceptance Criteria:**
- [ ] Solution file `NaturalStoneImpex.sln` exists at repo root
- [ ] `src/NaturalStoneImpex.Api/` — ASP.NET Core 8 Web API project created
- [ ] `src/NaturalStoneImpex.Client/` — Blazor WebAssembly Standalone project created
- [ ] Both projects build without errors (`dotnet build`)
- [ ] `README.md` exists with project overview
- [ ] `CLAUDE.md` exists with conventions and commands
- [ ] `docs/` folder structure created with placeholder files
- [ ] `planning/` folder structure created with all epic files
- [ ] `.gitignore` configured for .NET projects

**Tasks:**
- Create solution: `dotnet new sln -n NaturalStoneImpex`
- Create API project: `dotnet new webapi -n NaturalStoneImpex.Api -o src/NaturalStoneImpex.Api`
- Create Blazor WASM project: `dotnet new blazorwasm -n NaturalStoneImpex.Client -o src/NaturalStoneImpex.Client`
- Add both projects to solution
- Create folder structure: `docs/`, `planning/epics/`
- Write `README.md` and `CLAUDE.md`
- Configure `.gitignore`

---

### Story 1.2: Configure API Project Foundation

**As** a developer, **I want** the API project configured with EF Core, SQL Server, CORS, and proper middleware **so that** the API is ready to accept requests from the Blazor client.

**Acceptance Criteria:**
- [ ] NuGet packages installed: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `BCrypt.Net-Next`
- [ ] `AppDbContext.cs` created in `Data/` folder (empty for now, will add DbSets in later epics)
- [ ] `appsettings.json` has SQL Server connection string (using LocalDB or a configurable connection)
- [ ] `appsettings.Development.json` has development-specific settings
- [ ] CORS configured to allow Blazor client origin (`https://localhost:5002` or configured port)
- [ ] JWT authentication configured in `Program.cs` (middleware registered, settings in appsettings)
- [ ] Global exception handling middleware created — returns `{ "error": "message" }` format
- [ ] Swagger/OpenAPI enabled for development
- [ ] API runs and returns a health check response at `GET /api/health`

**Tasks:**
- Install NuGet packages
- Create `Data/AppDbContext.cs` inheriting from `DbContext`
- Configure connection string in `appsettings.json`
- Add CORS policy in `Program.cs`
- Add JWT Bearer authentication configuration
- Create `Middleware/ExceptionHandlingMiddleware.cs`
- Add health check endpoint
- Verify API starts on configured port

---

### Story 1.3: Configure Blazor Client Foundation

**As** a developer, **I want** the Blazor client configured with Bootstrap 5, routing, layouts, and HTTP client **so that** I can start building pages.

**Acceptance Criteria:**
- [ ] Bootstrap 5 CSS included (CDN link in `index.html` or local copy)
- [ ] Bootstrap 5 JS bundle included (for dropdowns, modals, etc.)
- [ ] Two layouts created: `MainLayout.razor` (public pages) and `AdminLayout.razor` (admin pages with sidebar)
- [ ] Public navigation: Начална страница, Каталог, Контакти, Количка (icon)
- [ ] Admin navigation sidebar: Табло, Продукти, Категории, Поръчки, Доставки
- [ ] `HttpClient` registered in `Program.cs` pointing to API base URL
- [ ] Routing configured: public pages at `/`, admin pages at `/admin/*`
- [ ] A placeholder page exists for each route (just a heading, to verify routing works)
- [ ] Blazor client runs and displays the public layout with navigation

**Tasks:**
- Add Bootstrap 5 to `wwwroot/index.html`
- Create `Layout/MainLayout.razor` with public nav (Bulgarian labels)
- Create `Layout/AdminLayout.razor` with sidebar nav (Bulgarian labels)
- Register `HttpClient` in `Program.cs` with API base URL from config
- Create placeholder `.razor` pages for all routes
- Verify navigation works between pages

---

### Story 1.4: Database Initial Migration

**As** a developer, **I want** the database created with an initial migration **so that** EF Core is working and ready for entity additions.

**Acceptance Criteria:**
- [ ] Initial EF Core migration created (even if empty or with just the AdminUser table)
- [ ] `dotnet ef database update` runs successfully and creates the database
- [ ] Database connection verified — API starts without database errors
- [ ] Seed data mechanism prepared (`DbSeeder.cs` in `Data/Seed/`) — will be populated in Epic 02

**Tasks:**
- Create initial migration: `dotnet ef migrations add InitialCreate --project src/NaturalStoneImpex.Api`
- Run migration: `dotnet ef database update --project src/NaturalStoneImpex.Api`
- Create `Data/Seed/DbSeeder.cs` with empty seed method called from `Program.cs`
- Verify database exists and API connects on startup

---

### Story 1.5: End-to-End Verification

**As** a developer, **I want** to verify the full stack works end-to-end **so that** I have confidence the foundation is solid.

**Acceptance Criteria:**
- [ ] API starts on its configured port
- [ ] Blazor client starts on its configured port
- [ ] Blazor client can call `GET /api/health` and display the result
- [ ] CORS allows the request (no browser errors)
- [ ] Navigation between public pages works
- [ ] Navigation to `/admin` routes works (shows admin layout)
- [ ] No console errors in browser or API terminal

**Tasks:**
- Start both projects simultaneously
- Add a test component on one Blazor page that calls `/api/health` and displays the response
- Verify in browser dev tools: no CORS errors, no 404s, no console errors
- Document the startup process in README.md
