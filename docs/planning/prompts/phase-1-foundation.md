# Phase 1: Foundation — Exact Claude Code Prompts

## Prerequisites

Before starting, make sure:
1. You have .NET 8 SDK installed
2. You have SQL Server (LocalDB or Express) running
3. You created the repo and committed all docs:

```bash
mkdir NaturalStoneImpex
cd NaturalStoneImpex
git init
# Copy all files: CLAUDE.md, README.md, docs/, planning/
git add .
git commit -m "Phase 0: Project documentation and planning"
```

4. Open Claude Code in the `NaturalStoneImpex/` root directory

---

## Session 1.1 — Create Solution and Projects

### Prompt 1 (Plan)
```
Read CLAUDE.md, docs/conventions.md, and planning/epics/01-project-setup.md.

I want to implement Stories 1.1, 1.2, and 1.3 — create the solution structure, configure the API project, and configure the Blazor client.

Before writing any code, tell me your plan:
- What projects will you create and what commands will you use?
- What NuGet packages will you install for each project?
- What ports will each project run on?
- How will you configure CORS?

Don't write any code yet.
```

> **Wait for response. Review the plan. If it mentions packages not in our tech stack or does something unexpected, correct it. Then proceed:**

### Prompt 2 (Execute)
```
The plan looks good. Proceed with the implementation. Make sure:

1. Solution file at the repo root: NaturalStoneImpex.sln
2. API project at src/NaturalStoneImpex.Api/ — ASP.NET Core 8 Web API
3. Blazor client at src/NaturalStoneImpex.Client/ — Blazor WebAssembly Standalone
4. API NuGet packages:
   - Microsoft.EntityFrameworkCore.SqlServer
   - Microsoft.EntityFrameworkCore.Tools
   - Microsoft.AspNetCore.Authentication.JwtBearer
   - BCrypt.Net-Next
5. API configured with:
   - SQL Server connection string in appsettings.json (use LocalDB)
   - CORS allowing the Blazor client origin
   - JWT Bearer authentication middleware
   - Swagger for development
   - Global exception handling middleware returning { "error": "message" }
   - Health check endpoint: GET /api/health returning { "status": "ok", "timestamp": "..." }
6. Empty AppDbContext.cs in Data/ folder
7. Blazor client configured with:
   - Bootstrap 5 CSS and JS via CDN in index.html
   - HttpClient registered pointing to API base URL
   - Two layouts: MainLayout.razor (public nav) and AdminLayout.razor (admin sidebar)
   - Public nav items: Начална страница, Каталог, Контакти, cart icon (🛒)
   - Admin sidebar items: Табло, Продукти, Категории, Поръчки, Доставки
   - Placeholder .razor pages for all routes
   - Routing: public pages at /, admin pages at /admin/*
8. .gitignore for .NET projects
```

### Verify
```bash
dotnet build
# Start API in one terminal:
dotnet run --project src/NaturalStoneImpex.Api
# Start client in another terminal:
dotnet run --project src/NaturalStoneImpex.Client
```

Check:
- [ ] Both projects build without errors
- [ ] API returns JSON at /api/health
- [ ] Blazor client loads in browser
- [ ] Navigation between public pages works
- [ ] /admin routes show admin layout with sidebar

### Commit
```bash
git add .
git commit -m "Epic 01: Stories 1.1-1.3 — Solution scaffolding, API and Blazor client configuration"
```

---

## Session 1.2 — Database and E2E Verification

### Prompt 1
```
Read planning/epics/01-project-setup.md. Implement Stories 1.4 and 1.5.

Story 1.4 — Database Initial Migration:
- Create an initial EF Core migration (can be empty or minimal)
- Run the migration to create the database
- Create Data/Seed/DbSeeder.cs with an empty seed method
- Call the seeder from Program.cs after database migration on startup

Story 1.5 — End-to-End Verification:
- On one of the Blazor placeholder pages (e.g., Home.razor), add a small
  component that calls GET /api/health and displays the result
- This verifies the full chain: Blazor → HttpClient → CORS → API → response

Make sure the database is created using the connection string already
configured in appsettings.json.
```

### Verify
```bash
dotnet ef database update --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Api
# In another terminal:
dotnet run --project src/NaturalStoneImpex.Client
```

Check:
- [ ] Database exists in SQL Server (check via SSMS or CLI)
- [ ] API starts without database errors
- [ ] Blazor home page shows health check result (proves CORS + HttpClient work)
- [ ] No console errors in browser dev tools

### Commit
```bash
git add .
git commit -m "Epic 01: Stories 1.4-1.5 — Database initial migration and E2E verification"
```

---

## Session 1.3 — Authentication (AdminUser Entity + Seed)

### Prompt 1 (Plan)
```
Read docs/conventions.md, docs/database-schema.md (AdminUser section only),
docs/api-endpoints.md (Authentication section only), and
planning/epics/02-authentication.md.

I want to implement Stories 2.1 and 2.2 — the AdminUser entity with
seed data, and the login API endpoint.

Before writing any code, tell me:
- What will the AdminUser entity look like?
- How will you hash the password in the seeder?
- What will the login endpoint return?
- What claims will be in the JWT token?

Don't write any code yet.
```

> **Wait for response. Verify it matches our database-schema.md and api-endpoints.md. Correct if needed. Then:**

### Prompt 2 (Execute)
```
Proceed. Implement Stories 2.1 and 2.2:

Story 2.1 — AdminUser Entity and Seed:
- Create Models/Entities/AdminUser.cs with fields: Id (int, PK), Username
  (string, max 50, unique), PasswordHash (string, max 500), CreatedAt (datetime2)
- Add DbSet<AdminUser> to AppDbContext
- Configure entity in OnModelCreating (unique index on Username)
- Create and apply EF Core migration named "AddAdminUser"
- In DbSeeder.cs: seed admin user with username "admin", password "Admin123!"
  hashed with BCrypt. Skip if admin already exists.
- Call seeder on startup

Story 2.2 — Login API Endpoint:
- Create Models/DTOs/LoginRequest.cs: { Username, Password }
- Create Models/DTOs/LoginResponse.cs: { Token, ExpiresAt }
- Create Controllers/AuthController.cs with POST /api/auth/login
- Login logic: find user by username, verify BCrypt hash, generate JWT
  with claims: sub (user ID), username. Token expires in 8 hours.
- JWT settings (key, issuer, audience) in appsettings.json
- On invalid credentials return 401: { "error": "Невалидно потребителско
  име или парола." }

Test via Swagger that login works.
```

### Verify
```bash
dotnet ef database update --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Api
```

Check via Swagger:
- [ ] POST /api/auth/login with `{"username":"admin","password":"Admin123!"}` returns 200 with token
- [ ] POST /api/auth/login with wrong password returns 401 with Bulgarian error
- [ ] AdminUser exists in database (check via SSMS)

### Commit
```bash
git add .
git commit -m "Epic 02: Stories 2.1-2.2 — AdminUser entity, seed data, login endpoint"
```

---

## Session 1.4 — Authentication (Blazor Auth State + Login Page)

### Prompt 1
```
Read docs/conventions.md and planning/epics/02-authentication.md.

Implement Story 2.3 — Blazor Authentication State Provider:
- Create Auth/CustomAuthStateProvider.cs implementing AuthenticationStateProvider
- Create Services/IAuthService.cs and Services/AuthService.cs in the Blazor client
  - AuthService handles: Login(username, password) → calls POST /api/auth/login,
    stores token in memory, notifies auth state provider
  - Logout() → clears token, notifies auth state provider
- Token stored in a C# variable (in-memory, NOT localStorage)
- HttpClient automatically includes "Authorization: Bearer {token}" header
  on all requests when logged in. Use a DelegatingHandler or configure
  HttpClient directly.
- Register all services in Program.cs
- Add CascadingAuthenticationState to App.razor
- AuthorizeView available in components

Don't implement the login page yet — just the infrastructure.
```

### Verify
- [ ] Project builds without errors
- [ ] No runtime errors on startup

### Prompt 2
```
Now implement Stories 2.4 and 2.5 — Login page and route protection.

Story 2.4 — Admin Login Page:
- Page at /admin/login using a clean centered card layout (no sidebar, no public nav)
- Form fields: "Потребителско име" (text input), "Парола" (password input)
- "Вход" button
- On success: redirect to /admin (dashboard)
- On failure: show "Невалидно потребителско име или парола." as a Bootstrap
  alert-danger above the form
- Loading spinner on the button while request is in progress
- Form validates both fields are filled before submitting

Story 2.5 — Route Protection:
- All admin pages (except Login) have @attribute [Authorize]
- Create Components/RedirectToLogin.razor — used in App.razor <NotAuthorized>
  to redirect unauthenticated users to /admin/login
- AdminLayout.razor shows "Изход" (Logout) button in the sidebar/header
  that clears the token and redirects to /admin/login
- After login, redirect to /admin (or the originally requested page)
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Full test flow:
- [ ] Go to `localhost:5002/admin` → redirected to `/admin/login`
- [ ] Enter wrong credentials → error message shown in Bulgarian
- [ ] Enter `admin` / `Admin123!` → redirected to `/admin` (dashboard placeholder)
- [ ] Refresh the page → still on admin (or redirected to login since token is in-memory — this is expected)
- [ ] Click "Изход" → redirected to login page
- [ ] Go to `/admin/products` without login → redirected to login
- [ ] Public pages (/, /catalog, /contacts) work without login

### Commit
```bash
git add .
git commit -m "Epic 02: Stories 2.3-2.5 — Blazor auth state, login page, route protection"
```

---

## Phase 1 Complete ✅

At this point you should have:
- ✅ Solution with two projects that build and run
- ✅ SQL Server database with AdminUser table
- ✅ Admin user seeded (admin / Admin123!)
- ✅ Login endpoint returning JWT
- ✅ Blazor login page working
- ✅ Admin routes protected
- ✅ Public routes accessible without auth
- ✅ CORS, HttpClient, and JWT all working end-to-end

Update planning/overview.md:
```markdown
| 01 | Project Setup & Scaffolding | ✅ Completed  | None              |
| 02 | Authentication              | ✅ Completed  | Epic 01           |
```

```bash
git add planning/overview.md
git commit -m "Update planning status: Phase 1 complete"
```

**Next**: Phase 2 — Category and Product Management. Start a fresh Claude Code session.

---

## Troubleshooting

### If Claude Code installs unexpected packages:
```
Stop. I only want the packages listed in CLAUDE.md and docs/conventions.md.
Remove [package name] and use [correct approach] instead.
```

### If Claude Code uses English in the UI:
```
All UI text must be in Bulgarian. Replace all English text in the
components you just created. Refer to docs/conventions.md.
```

### If Claude Code creates files in wrong locations:
```
The file structure must match CLAUDE.md. Move [file] to [correct path].
Refer to the File Structure Reference section in CLAUDE.md.
```

### If the session gets confused or produces inconsistent code:
Close the session and start a new one. Don't try to fix a confused session — it usually makes things worse. Start fresh with a clear prompt.

### If CORS errors in browser:
```
The Blazor client is getting CORS errors when calling the API.
Check that the CORS policy in Program.cs allows the exact origin
of the Blazor client (including port number). Show me the current
CORS configuration.
```

### If JWT not being sent with requests:
```
The Authorization header is not being included in API requests from
the Blazor client. Show me how the HttpClient is configured and how
the DelegatingHandler attaches the Bearer token. The token should be
attached to every request when the user is logged in.
```
