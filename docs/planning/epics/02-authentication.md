# Epic 02: Authentication

## Description
Implement admin login with JWT tokens. A single admin account is seeded into the database. The Blazor client handles login, stores the token, and protects admin routes.

## Dependencies
- Epic 01 (Project Setup) must be completed.

## Stories

---

### Story 2.1: AdminUser Entity and Seed Data

**As** a developer, **I want** the AdminUser entity created and an admin account seeded **so that** there is a user to authenticate against.

**Acceptance Criteria:**
- [ ] `AdminUser` entity created: `Id` (int), `Username` (string), `PasswordHash` (string)
- [ ] `AdminUser` added as a `DbSet` in `AppDbContext`
- [ ] EF Core migration created and applied
- [ ] `DbSeeder.cs` seeds one admin user: username `admin`, password `Admin123!` (hashed with BCrypt)
- [ ] Seeder runs on application startup, skips if admin already exists
- [ ] Database has the admin user after running the app

**Tasks:**
- Create `Models/Entities/AdminUser.cs`
- Add `DbSet<AdminUser>` to `AppDbContext`
- Create and apply migration
- Implement BCrypt hashing in `DbSeeder.cs`
- Call seeder from `Program.cs` after database migration

---

### Story 2.2: Login API Endpoint

**As** the admin, **I want** a login endpoint that returns a JWT token **so that** I can authenticate with the system.

**Acceptance Criteria:**
- [ ] `POST /api/auth/login` accepts `{ "username": "...", "password": "..." }`
- [ ] On valid credentials: returns `{ "token": "jwt_token_here" }` with HTTP 200
- [ ] On invalid credentials: returns `{ "error": "Невалидно потребителско име или парола." }` with HTTP 401
- [ ] JWT token contains claims: `sub` (user ID), `username`, expiration (8 hours)
- [ ] JWT signing key configured in `appsettings.json`
- [ ] Password verified using BCrypt

**Tasks:**
- Create `Models/DTOs/LoginRequest.cs` and `Models/DTOs/LoginResponse.cs`
- Create `Controllers/AuthController.cs`
- Implement login logic: find user by username, verify BCrypt hash, generate JWT
- Configure JWT settings in `appsettings.json` (key, issuer, audience, expiration)
- Test with Swagger

---

### Story 2.3: Blazor Authentication State Provider

**As** a developer, **I want** Blazor to know whether the user is authenticated **so that** admin routes are protected on the client side.

**Acceptance Criteria:**
- [ ] `CustomAuthStateProvider.cs` created, implements `AuthenticationStateProvider`
- [ ] Token stored in memory (C# variable) — NOT in localStorage for now (can add persistence later)
- [ ] `AuthenticationStateProvider` registered in `Program.cs`
- [ ] `CascadingAuthenticationState` wraps the app in `App.razor`
- [ ] `AuthorizeView` can be used in components to show/hide content based on auth state
- [ ] `HttpClient` automatically includes `Authorization: Bearer {token}` header on all requests when logged in

**Tasks:**
- Create `Auth/CustomAuthStateProvider.cs`
- Create `Services/IAuthService.cs` and `Services/AuthService.cs` (handles login call, stores token, notifies auth state provider)
- Register services in `Program.cs`
- Add `CascadingAuthenticationState` to `App.razor`
- Create a delegating handler or configure `HttpClient` to attach the Bearer token

---

### Story 2.4: Admin Login Page

**As** the admin, **I want** a login page at `/admin/login` **so that** I can enter my credentials and access the admin panel.

**Acceptance Criteria:**
- [ ] Login page at `/admin/login` with form: username input, password input, login button
- [ ] All labels in Bulgarian: "Потребителско име", "Парола", "Вход"
- [ ] On successful login: redirect to `/admin` (dashboard)
- [ ] On failed login: show error message "Невалидно потребителско име или парола."
- [ ] Loading state shown while login request is in progress
- [ ] Form validates that both fields are filled before submitting
- [ ] Page uses a centered card layout (no sidebar, no public nav — clean login screen)

**Tasks:**
- Create `Pages/Admin/Login.razor`
- Implement form with `EditForm` and validation
- Call `AuthService.Login()` on submit
- Handle success (redirect) and failure (show error)
- Style with Bootstrap (centered card, clean design)

---

### Story 2.5: Admin Route Protection

**As** the admin, **I want** all `/admin/*` routes (except login) to require authentication **so that** unauthorized users cannot access the admin panel.

**Acceptance Criteria:**
- [ ] All admin pages (except Login) have `@attribute [Authorize]`
- [ ] Unauthenticated users visiting `/admin/*` are redirected to `/admin/login`
- [ ] `RedirectToLogin` component created for handling unauthorized redirects
- [ ] After login, user is redirected to the originally requested page (or dashboard by default)
- [ ] Admin layout shows a "Изход" (Logout) button that clears the token and redirects to `/admin/login`
- [ ] API endpoints with `[Authorize]` return 401 if no valid token is provided

**Tasks:**
- Add `@attribute [Authorize]` to all admin pages except Login
- Create `Components/RedirectToLogin.razor` used in `App.razor` for `<NotAuthorized>`
- Add logout functionality to `AuthService` and `AdminLayout.razor`
- Verify API returns 401 for protected endpoints without a token
- Test the full flow: visit admin page → redirected to login → log in → redirected back
