# Storefront Redesign + Static SSR Storefront Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Api project into a single Blazor Web App host that serves a static-SSR storefront in the new "Образец" design, with the admin panel and the visualizer kept as interactive WebAssembly.

**Architecture:** `NaturalStoneImpex.Api` hosts controllers plus Razor components. Storefront pages are static SSR, live in `Api/Components`, and call API services in-process. `NaturalStoneImpex.Client` becomes the Web App's WebAssembly project: admin pages run with interactive WASM routing, and the visualizer runs as a WASM island. Admin auth moves to an HttpOnly cookie (JWT stays for API clients), and the cart is a cookie holding product IDs and quantities.

**Tech Stack:** .NET 10, ASP.NET Core, Blazor Web App (static SSR + InteractiveWebAssembly), EF Core SQL Server, Bootstrap 5.3 (themed via `--bs-*`), Bootstrap Icons 1.11.3 (self-hosted), Geologica + Wix Madefor Text (self-hosted woff2), xUnit + EF InMemory, Playwright MCP for browser checks.

**Spec:** `docs/superpowers/specs/2026-11-07-storefront-redesign-ssr-design.md`. Read it before any task. The visual reference is https://claude.ai/artifact/NtVjHieQNGj8uLbQitm6A5 (Direction A section, product-page mockup, admin mockup).

**Branch:** work on `feature/storefront-redesign-ssr`, created from `main` before Task 1.

## Global Constraints

- All UI text is Bulgarian. Copy rules: sentence case, active voice, no exclamation marks, no em dashes, one label per intent ("Добави в количката" everywhere).
- Dates are `DD.MM.YYYY`. Money is `12.50 €`: invariant culture, 2 decimals, a space, then €. Use `PriceFormat` (Task 4) everywhere, never `ToString("F2")` directly.
- Colour tokens are exactly: `--paper #E8E9E6`, `--surface #F4F5F3`, `--ink #1C2023`, `--stone #5A6165`, `--rule #C7CBCA`, `--accent #2F4A5C`, `--accent-hover #253C4B`, `--on-accent #F4F5F3`, `--ok #3D6B4A`, `--warn #8A6418`, `--danger #A1352B`. Use one accent and no gradients.
- Fonts: Geologica (300, 400, 500) for headlines and Wix Madefor Text (400, 500, 600) for everything else, self-hosted. Every page has `<html lang="bg">`. No monospace face. Use `font-variant-numeric: tabular-nums` for prices, quantities and numbers in tables.
- Shape and spacing:
  - Radius is 2px everywhere.
  - Groups are separated by 1px `--rule` hairlines. Cards get no shadows.
  - Interactive targets are at least 44px.
  - Spacing scale (px): 4, 8, 12, 16, 20, 24, 32, 48, 56, 72.
  - Container max width is 1200px, with a 20px gutter on phones and 32px on desktop.
- Motion:
  - Easing is `cubic-bezier(0.2, 0.7, 0.2, 1)`. Durations are 120ms for press, 160ms for hover and colour, 240ms for menus.
  - No entrance or scroll animations.
  - `prefers-reduced-motion` turns transitions off.
- Storefront pages never load `dotnet.*.wasm` or .NET assemblies. `_framework/blazor.web.js` is allowed.
- No DB schema change. Existing endpoint routes and DTO shapes stay the same. API additions are limited to the spec's §5.7 list plus `GET /api/orders/{id}/stock-check` (Task 18).
- Prices always come from the DB. The cart cookie holds only `productId:quantity` pairs.
- Stock never blocks ordering. Confirming an order may drive stock below zero.
- Products priced 0 cannot be added to the cart. They show "Цена при запитване".
- Thin controllers and endpoints: logic lives in services or pure classes, which get the tests.
- Existing routes stay the same. Links that cross between the storefront and the admin use `data-enhance-nav="false"` or `forceLoad: true`.

## Review Focus

1. **Bulgarian decimal input.** "4,5", "4.5", "1 204,5" (with a normal or no-break space) and "  12 " must parse the same way in the calculator, cart and checkout, whatever the server culture is. Pinned in Task 4 (`DecimalInputTests`).
2. **Stale or hostile cart cookie.** Unknown, inactive or zero-priced product IDs, a duplicate ID, 500 lines, a negative quantity or garbage text must render a usable cart page, never an exception. Pinned in Task 5 (`CartCookieTests`) and Task 11 (`CartViewModelTests`).
3. **Double-submitted checkout.** A second POST after the order succeeds must not create a second order. Pinned in Task 12 (`CheckoutHandlerTests.Empty_cart_creates_no_order`) plus the JS submit lock.
4. **Crossing between storefront and admin.** Going header→admin, admin "Към сайта"→home, or visualizer→cart must do a full page load and never leave a blank page or a WASM "not found". Pinned in Task 3 step 7 and Task 15 step 6 (Playwright).
5. **`ReturnUrl` tricks on login.** `//evil.com`, `/\evil.com`, `https://evil.com`, `%2F%2Fevil.com` and `javascript:` must all fall back to `/admin`. Pinned in Task 3 (`ReturnUrlValidatorTests`).

## File Structure

```
src/NaturalStoneImpex.Api/
  Program.cs                                  host wiring (Tasks 2, 3, 5, 6, 13)
  Auth/AuthSchemes.cs                         scheme names + policy selector (Task 3)
  Auth/ReturnUrlValidator.cs                  local-only ReturnUrl (Task 3)
  Auth/AdminCsrfHeaderMiddleware.cs           X-NSI-Request rule (Task 3)
  Storefront/DecimalInput.cs                  comma/dot parsing (Task 4)
  Storefront/QuantityCalculator.cs            m²/kg calculator (Task 4)
  Storefront/CartCookie.cs                    cookie (de)serialisation (Task 5)
  Storefront/CartStore.cs                     ICartStore over the cookie (Task 5)
  Storefront/Flash.cs                         one-shot toast cookie (Task 5)
  Storefront/StorefrontQueryService.cs        storefront reads (Task 7)
  Storefront/ProductJsonLd.cs                 JSON-LD builder (Task 10)
  Storefront/CartViewModel.cs                 cart lines + totals + flags (Task 11)
  Storefront/CheckoutHandler.cs               cart → CreateOrderRequest → order (Task 12)
  Storefront/SitemapBuilder.cs                sitemap/robots text (Task 13)
  Endpoints/CartEndpoints.cs                  /cart/add|update|remove, /api/cart/items (Task 5)
  Endpoints/SeoEndpoints.cs                   /sitemap.xml, /robots.txt (Task 13)
  Components/App.razor, Components/_Imports.razor                       (Task 2)
  Components/Layout/MainLayout.razor, LoginLayout.razor                 (Tasks 3, 6)
  Components/Shared/SeoHead.razor, PageScript.razor, Toast.razor,
                    ProductCard.razor, CategoryIndex.razor, Pagination.razor (Tasks 6, 8, 9)
  Components/Pages/*.razor                    storefront pages (Tasks 6, 8-13, 15)
  Components/Pages/Admin/Login.razor          static SSR login (Task 3)
src/NaturalStoneImpex.Client/
  Formatting/PriceFormat.cs                   shared money/qty formatting (Task 4)
  Routes.razor                                (renamed from App.razor, Task 2)
  Auth/CookieAuthStateProvider.cs, Auth/CsrfHeaderHandler.cs   (Task 3)
  Components/VisualizerApp.razor              visualizer island (Task 15)
  Services/CartApi.cs                         POST /api/cart/items (Task 15)
  Layout/AdminLayout.razor                    top bar (Task 16)
  wwwroot/fonts/, wwwroot/lib/bootstrap-icons/                  (Task 4)
  wwwroot/css/nsi/tokens.css, base.css, components.css, storefront.css, admin.css (Tasks 4, 6, 16)
  wwwroot/js/storefront/*.js                  page modules (Tasks 6, 9, 10, 11, 12)
tests/NaturalStoneImpex.Api.Tests/
  Storefront/*Tests.cs, Auth/*Tests.cs, OrderConfirmTests.cs
```

All static assets (CSS, fonts, JS, Bootstrap) live in `Client/wwwroot`, which the host serves at the site root. `Api/wwwroot` holds only runtime uploads.

---

## Phase 1: Guardrails

### Task 1: Replace the old design guidance

**Files:**
- Modify: `.claude/skills/frontend-design/SKILL.md` (full rewrite)
- Modify: `docs/conventions.md` (design and UI rules section)
- Modify: `docs/planning/design-plan.md` and `docs/planning/prompts/design-phase-1-foundation.md` through `design-phase-5-admin-pages.md` (header note only)
- Add: the spec and this plan to git

- [ ] **Step 1: Create the branch.** `git checkout -b feature/storefront-redesign-ssr`
- [ ] **Step 2: Rewrite `SKILL.md`.** Keep the frontmatter `name: frontend-design` and set the description to "Design rules for Natural Stone Impex (Direction A 'Образец'). Use for any UI work in this repo." The body is at most 120 lines. It covers:
  - the design read and the dials (variance 6, motion 2, density 4 storefront / 7 admin);
  - every token from Global Constraints;
  - fonts and why (Bulgarian `locl`);
  - component rules from spec §6.6–6.7;
  - the retired patterns from spec §6.8 as a "never" list;
  - the copy rules from spec §7.10;
  - render-mode rules: storefront = static SSR in `Api/Components` with `[ExcludeFromInteractiveRouting]`; admin = Client pages with interactive WASM routing; visualizer = island.

  It links the spec, and drops every bronze/gold/Fraunces/fade-in instruction and the "never change Bulgarian text" rule.
- [ ] **Step 3: Update `docs/conventions.md`.** Replace its design rules with a short pointer to the skill and the spec. Remove any rule that contradicts them.
- [ ] **Step 4: Mark the old design docs superseded.** Put this line at the top of `design-plan.md` and each `design-phase-*.md`: `> Superseded on 07.11.2026 by docs/superpowers/specs/2026-11-07-storefront-redesign-ssr-design.md. Do not follow.`
- [ ] **Step 5: Commit.**
```bash
git add .claude/skills/frontend-design/SKILL.md docs/conventions.md docs/planning docs/superpowers
git commit -m "docs: adopt Образец design direction, retire old design guidance"
```

---

## Phase 2: One host

### Task 2: Api becomes the Blazor Web App host; Client becomes its WASM project

Every current page keeps working as interactive WASM inside the new host, with the current look. Admin deep links and refreshes return 401 until Task 3. Test admin only through in-app navigation in this task.

**Files:**
- Modify: `src/NaturalStoneImpex.Client/NaturalStoneImpex.Client.csproj`, `Program.cs`, `Services/ProductService.cs` (and any other `ResolveImageUrl` user, found with `grep -rn ResolveImageUrl src`)
- Rename: `src/NaturalStoneImpex.Client/App.razor` → `Routes.razor`
- Delete: `src/NaturalStoneImpex.Client/wwwroot/index.html`
- Create: `src/NaturalStoneImpex.Api/Components/App.razor`, `Components/_Imports.razor`
- Modify: `src/NaturalStoneImpex.Api/NaturalStoneImpex.Api.csproj`, `Program.cs`, `Properties/launchSettings.json`, `appsettings.json` (remove `ClientUrl`)
- Modify: `CLAUDE.md` and `README.md`: commands, tech stack, file structure

**Interfaces:**
- Produces:
  - `NaturalStoneImpex.Client.Routes`, the router component.
  - `Api.Components.App`, the root document.
  - Interactive routing is the default. A page opts out with `@attribute [ExcludeFromInteractiveRouting]`.

- [ ] **Step 1: Convert the Client project.**
  - Keep `Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"`.
  - Remove `OverrideHtmlAssetPlaceholders` and the `...WebAssembly.DevServer` package.
  - Add `<NoDefaultLaunchSettingsFile>true</NoDefaultLaunchSettingsFile>` and `<StaticWebAssetProjectMode>Default</StaticWebAssetProjectMode>`.
  - In `Program.cs`, delete both `RootComponents.Add` lines and set the HttpClient `BaseAddress` to `new Uri(builder.HostEnvironment.BaseAddress)`.
  - Rename `App.razor` to `Routes.razor`; its contents stay the same.
  - Delete `wwwroot/index.html`.
- [ ] **Step 2: Remove cross-origin image handling.** Delete `ResolveImageUrl` and its calls; image paths stay as the API returns them (`/uploads/...`).
- [ ] **Step 3: Make the Api project the host.**
  - Add `<ProjectReference Include="..\NaturalStoneImpex.Client\NaturalStoneImpex.Client.csproj" />` and the package `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.14.
  - In `Program.cs`:
    - Add `AddRazorComponents().AddInteractiveWebAssemblyComponents()`.
    - Remove `AddCors`, `UseCors`, and the `Access-Control-Allow-Origin` `OnPrepareResponse` block.
    - Wrap `ExceptionHandlingMiddleware` as `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), b => b.UseMiddleware<ExceptionHandlingMiddleware>())`.
    - After `UseAuthorization()`, add `app.UseAntiforgery()`, `app.MapStaticAssets()`, `app.MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(typeof(NaturalStoneImpex.Client._Imports).Assembly)`.
    - Keep `UseStaticFiles()` for `/uploads`.
- [ ] **Step 4: Create `Components/App.razor`.**
  - `<html lang="bg">`, with the same `<head>` links as the deleted `index.html`, using `@Assets["..."]` for local files.
  - `<HeadOutlet @rendermode="PageRenderMode" />`.
  - Body: `<Routes @rendermode="PageRenderMode" />`, the blazor-error-ui block, the existing `js/*.js` script tags, and `<script src="_framework/blazor.web.js"></script>`.
  - `@code`: `[CascadingParameter] HttpContext HttpContext`; `IComponentRenderMode? PageRenderMode => HttpContext.AcceptsInteractiveRouting() ? new InteractiveWebAssemblyRenderMode(prerender: false) : null;`
- [ ] **Step 5: Update launch settings and docs.**
  - `launchUrl` becomes `""` (Swagger stays at `/swagger`).
  - In `CLAUDE.md`, the run commands become a single `dotnet run --project src/NaturalStoneImpex.Api` on https://localhost:5001.
  - The tech stack and file structure in `CLAUDE.md` and `README.md` describe the new layout from this plan's File Structure, and the client run command is removed.
- [ ] **Step 6: Build and test.** Run `dotnet build` (expected: 0 errors, no warnings beyond the existing ImageSharp NU190x ones). Run `dotnet test` (expected: all existing tests pass).
- [ ] **Step 7: Smoke test in a browser.** Run the host. With Playwright:
  - `/`, `/catalog`, `/products/{id}` and `/visualizer` render with product images.
  - Log in at `/admin/login`, then use in-app links to open Orders and a product edit form.
  - The console shows no CORS errors.
- [ ] **Step 8: Commit.** `git commit -am "refactor: host Blazor WASM client inside the Api as a Blazor Web App"` (also `git add` the new files).

### Task 3: Cookie authentication for the admin

**Files:**
- Create: `src/NaturalStoneImpex.Api/Auth/AuthSchemes.cs`, `Auth/ReturnUrlValidator.cs`, `Auth/AdminCsrfHeaderMiddleware.cs`, `Components/Pages/Admin/Login.razor`, `Components/Layout/LoginLayout.razor`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`, `Services/IAuthService.cs`, `Services/AuthService.cs`, `Controllers/AuthController.cs`
- Create: `src/NaturalStoneImpex.Client/Auth/CookieAuthStateProvider.cs`, `Auth/CsrfHeaderHandler.cs`
- Modify: `src/NaturalStoneImpex.Client/Program.cs`, `Services/IAuthService.cs`, `Services/AuthService.cs`, `Components/RedirectToLogin.razor`, `Layout/AdminLayout.razor` (logout and "Към сайта")
- Delete: `src/NaturalStoneImpex.Client/Pages/Admin/Login.razor`, `Layout/LoginLayout.razor`, `Auth/CustomAuthStateProvider.cs`, `Auth/AuthTokenHandler.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/Auth/ReturnUrlValidatorTests.cs`, `Auth/AuthSchemesTests.cs`, `Auth/AdminCsrfHeaderTests.cs`, `Auth/ValidateCredentialsTests.cs`

**Interfaces:**
- Produces (Api):
  - `AuthSchemes`, with members:
    - `public const string Policy = "NsiAuth"`
    - `public const string Cookie = "NsiAdminCookie"`
    - `public const string Bearer = JwtBearerDefaults.AuthenticationScheme`
    - `public static string Select(HttpContext ctx)`
  - `ReturnUrlValidator.ToLocalOrDefault(string? returnUrl, string fallback = "/admin") : string`
  - `AdminCsrfHeaderMiddleware.IsAllowed(string method, PathString path, string? authenticatedScheme, IHeaderDictionary headers) : bool`
  - `IAuthService.ValidateCredentialsAsync(string username, string password) : Task<AdminUser?>`
  - Endpoints:
    - `GET /api/auth/me` returns 200 `{ "username": "admin" }`, or 401.
    - `POST /api/auth/logout` returns 204.
  - The header name is `X-NSI-Request` and its value is `1`.
- Produces (Client):
  - `CookieAuthStateProvider : AuthenticationStateProvider`
  - `CsrfHeaderHandler : DelegatingHandler`
  - `IAuthService.LogoutAsync() : Task`

- [ ] **Step 1: Write the failing tests.**
```csharp
// ReturnUrlValidatorTests
[Theory]
[InlineData("/admin/orders", "/admin/orders")]
[InlineData("/admin/orders/5?tab=items", "/admin/orders/5?tab=items")]
[InlineData(null, "/admin")] [InlineData("", "/admin")]
[InlineData("//evil.com", "/admin")] [InlineData("/\\evil.com", "/admin")]
[InlineData("https://evil.com/admin", "/admin")] [InlineData("%2F%2Fevil.com", "/admin")]
[InlineData("javascript:alert(1)", "/admin")] [InlineData("admin", "/admin")]
public void ToLocalOrDefault_accepts_only_local_paths(string? input, string expected)
    => Assert.Equal(expected, ReturnUrlValidator.ToLocalOrDefault(input));

// AuthSchemesTests
[Fact] public void Selects_bearer_when_authorization_header_has_bearer_token() // header "Bearer x" → AuthSchemes.Bearer
[Fact] public void Selects_cookie_otherwise()                                // no header → AuthSchemes.Cookie

// AdminCsrfHeaderTests
[Fact] public void Cookie_authenticated_api_post_without_header_is_rejected()   // ("POST","/api/products",Cookie,{}) → false
[Fact] public void Cookie_authenticated_api_post_with_header_is_allowed()       // X-NSI-Request: 1 → true
[Fact] public void Get_requests_are_always_allowed()                            // ("GET",...) → true
[Fact] public void Bearer_authenticated_requests_are_allowed_without_header()   // scheme Bearer → true
[Fact] public void Anonymous_cart_items_post_requires_header()                  // ("POST","/api/cart/items",null,{}) → false

// ValidateCredentialsTests (InMemory DB + real PasswordHasher<AdminUser>)
[Fact] public async Task Returns_user_for_correct_password()
[Fact] public async Task Returns_null_for_wrong_password_or_unknown_user()
```
- [ ] **Step 2: Run the tests to confirm they fail.** `dotnet test --filter "FullyQualifiedName~Auth"`. Expected: FAIL to compile, because the types are missing.
- [ ] **Step 3: Implement the Api side.**
  - `ReturnUrlValidator`: URL-decode once; accept only values that start with a single `/`, not `//` or `/\`, and are relative URIs; otherwise return the fallback.
  - `AdminCsrfHeaderMiddleware`:
    - `IsAllowed` returns false for POST, PUT, PATCH and DELETE to `/api/**` when `authenticatedScheme == AuthSchemes.Cookie` or the path is `/api/cart/items`, unless `X-NSI-Request` is `1`.
    - `InvokeAsync` returns 400 `{ "error": "Невалидна заявка." }` when the request isn't allowed.
    - Register it after `UseAuthentication`.
  - `AuthService.LoginAsync` uses `ValidateCredentialsAsync` internally.
  - `Program.cs`:
    - `AddAuthentication(AuthSchemes.Policy)`, then `.AddPolicyScheme(AuthSchemes.Policy, null, o => o.ForwardDefaultSelector = AuthSchemes.Select)`, then `.AddCookie(AuthSchemes.Cookie, ...)`, then the existing `.AddJwtBearer(...)`.
    - Cookie settings: name `nsi_admin`, HttpOnly, `SecurePolicy = Always`, `SameSite = Strict`, `ExpireTimeSpan = 8h`, `SlidingExpiration = true`, `LoginPath = "/admin/login"`, `AccessDeniedPath = "/admin/login"`.
    - `Events.OnRedirectToLogin` and `OnRedirectToAccessDenied` set 401 or 403 (no redirect) for `/api` paths.
  - `AuthController`:
    - `[Authorize] [HttpGet("me")]` returns `User.FindFirst("username")?.Value ?? User.Identity!.Name`.
    - `[HttpPost("logout")]` calls `SignOutAsync(AuthSchemes.Cookie)`.
    - Fix the typo "иметo" → "името" on line 24.
- [ ] **Step 4: Implement `Components/Pages/Admin/Login.razor`** (static SSR).
  - Directives: `@page "/admin/login"`, `@attribute [ExcludeFromInteractiveRouting]`, `@layout LoginLayout`.
  - `[SupplyParameterFromForm(FormName = "login")] LoginForm Model`, with `[Required]` username and password and the existing Bulgarian messages.
  - `[SupplyParameterFromQuery] string? ReturnUrl`.
  - `[CascadingParameter] HttpContext`.
  - On a valid submit:
    - Call `ValidateCredentialsAsync`.
    - On failure, show "Невалидно потребителско име или парола." above the form.
    - On success, call `SignInAsync(AuthSchemes.Cookie, principal)`, where `principal` has claims `sub` and `username`, and `IsPersistent` is true.
    - Then `NavigationManager.NavigateTo(ReturnUrlValidator.ToLocalOrDefault(ReturnUrl), forceLoad: true)`.
  - Style: the existing look for now (restyled in Task 16).
- [ ] **Step 5: Implement the Client side.**
  - `CookieAuthStateProvider`:
    - `GET api/auth/me`, cached for the app's lifetime and reset by `NotifyChanged()`.
    - 200 gives a `ClaimsIdentity` with `ClaimTypes.Name` and authentication type `"cookie"`; anything else gives anonymous.
  - `CsrfHeaderHandler` adds `X-NSI-Request: 1` to every request.
  - `Program.cs`:
    - Register both, plus `AddCascadingAuthenticationState()`.
    - Remove `CustomAuthStateProvider` and `AuthTokenHandler`.
  - `IAuthService` keeps only `LogoutAsync()`: it POSTs `api/auth/logout`, then `NavigateTo("/admin/login", forceLoad: true)`.
  - `RedirectToLogin` does `NavigateTo($"/admin/login?ReturnUrl={Uri.EscapeDataString(relativeUrl)}", forceLoad: true)`.
  - In `AdminLayout`, "Към сайта" gets `data-enhance-nav="false"`.
- [ ] **Step 6: Run the tests to confirm they pass.** `dotnet test`. Expected: all pass.
- [ ] **Step 7: Browser check.** With Playwright:
  - Signed out, `/admin/orders` redirects to `/admin/login?ReturnUrl=%2Fadmin%2Forders`; logging in lands on Orders.
  - F5 on `/admin/orders` stays signed in.
  - `/admin/login?ReturnUrl=//evil.com` lands on `/admin` after login.
  - Изход returns to login, and `/admin` then redirects.
  - Admin "Към сайта" loads `/` fully, and the header link back to `/admin` loads fully (Review Focus 4).
  - Creating a category and confirming an order still work.
  - Swagger with a Bearer token still authorizes `GET /api/orders`.
- [ ] **Step 8: Commit.** `git commit -m "feat(auth): cookie auth for admin with policy scheme, SSR login, CSRF header"`

---

## Phase 3: Foundation

### Task 4: Tokens, fonts, formatting and parsing

**Files:**
- Create: `scripts/download-fonts.ps1`
- Create: `src/NaturalStoneImpex.Client/wwwroot/fonts/` (woff2 files + `fonts.css` + `OFL.txt`)
- Create: `src/NaturalStoneImpex.Client/wwwroot/lib/bootstrap-icons/` (1.11.3 css + woff2)
- Create: `src/NaturalStoneImpex.Client/wwwroot/css/nsi/tokens.css`, `base.css`
- Create: `src/NaturalStoneImpex.Client/Formatting/PriceFormat.cs`
- Create: `src/NaturalStoneImpex.Api/Storefront/DecimalInput.cs`, `Storefront/QuantityCalculator.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/PriceFormatTests.cs`, `DecimalInputTests.cs`, `QuantityCalculatorTests.cs`

**Interfaces:**
- Produces:
  - `PriceFormat.Money(decimal value) : string` gives `"1204.65 €"`.
  - `PriceFormat.Quantity(decimal value, string unitDisplay) : string` gives `"13.20 м²"`.
  - `PriceFormat.Signed(decimal value, string unitDisplay) : string` gives `"−8.20 м²"`, with U+2212 for negatives.
  - `DecimalInput.TryParse(string? text, out decimal value) : bool`
  - `enum QuantityMode { Dimensions, Area, Kilograms }`
  - `record QuantityInput(QuantityMode Mode, string? Length, string? Width, string? Area, string? Kilograms, bool AddWaste)`
  - `record QuantityResult(bool IsValid, decimal Quantity, string? Error, string? ErrorField)`, where `ErrorField` is one of `"Length"`, `"Width"`, `"Area"` or `"Kilograms"`.
  - `QuantityCalculator.Calculate(QuantityInput input) : QuantityResult`
  - `QuantityCalculator.WasteFactor = 1.10m`
  - `QuantityCalculator.MaxQuantity = 10000m`
  - CSS custom properties exactly as in Global Constraints, plus `--font-display`, `--font-text`, `--ease`.

- [ ] **Step 1: Write the failing tests.**
```csharp
// PriceFormatTests: run each under CultureInfo("bg-BG") as CurrentCulture
[Theory] [InlineData(1204.65, "1204.65 €")] [InlineData(0.41, "0.41 €")] [InlineData(20, "20.00 €")]
public void Money_uses_dot_and_two_decimals(decimal v, string expected)
[Fact] public void Signed_negative_uses_unicode_minus() // Signed(-8.2m,"м²") == "−8.20 м²"

// DecimalInputTests
[Theory]
[InlineData("4,5", 4.5)] [InlineData("4.5", 4.5)] [InlineData(" 12 ", 12)]
[InlineData("1 204,5", 1204.5)] [InlineData("1 204,5", 1204.5)] [InlineData("0,01", 0.01)]
public void Parses_bulgarian_and_invariant_forms(string text, decimal expected)
[Theory] [InlineData(null)] [InlineData("")] [InlineData("abc")] [InlineData("1,2,3")] [InlineData("-3")] [InlineData("1.2.3")]
public void Rejects_invalid(string? text)

// QuantityCalculatorTests
[Fact] public void Dimensions_with_waste_4x3_is_13_20()          // ("4,00","3,00", waste) → 13.20
[Fact] public void Dimensions_without_waste_4x3_is_12()           // → 12.00
[Fact] public void Area_with_waste_rounds_up_to_cent()            // Area "10,01" waste → 11.02 (11.011 rounds up)
[Fact] public void Kilograms_ignore_waste()                       // Kilograms "25", AddWaste=true → 25
[Fact] public void Missing_width_reports_width_field()            // ErrorField == "Width", Error == "Въведете ширина в метри."
[Fact] public void Zero_or_negative_is_invalid()                  // Area "0" → IsValid false, ErrorField "Area"
[Fact] public void Above_max_is_invalid()                         // Area "20000" → Error "Количеството е твърде голямо."
```
- [ ] **Step 2: Run the tests to confirm they fail.** `dotnet test --filter "FullyQualifiedName~Storefront"`. Expected: FAIL to compile.
- [ ] **Step 3: Implement.**
  - `DecimalInput`: strip regular, no-break and narrow no-break spaces; change `,` to `.`; parse with `NumberStyles.AllowDecimalPoint` and `CultureInfo.InvariantCulture`; reject negatives.
  - `QuantityCalculator`:
    - Raw value: Dimensions = length × width, Area = area, Kilograms = kg.
    - Apply waste only to the m² modes.
    - Round up with `Math.Ceiling(q * 100) / 100`.
    - Error messages:
      - Length: "Въведете дължина в метри."
      - Width: "Въведете ширина в метри."
      - Area: "Въведете площ в м²."
      - Kilograms: "Въведете количество в кг."
      - Over the maximum: "Количеството е твърде голямо."
  - `PriceFormat` uses `ToString("0.00", CultureInfo.InvariantCulture)`.
  - The test project already references Api, which references Client, so it can see `PriceFormat`.
- [ ] **Step 4: Run the tests to confirm they pass.** `dotnet test --filter "FullyQualifiedName~Storefront"`. Expected: PASS.
- [ ] **Step 5: Add fonts and icons.**
  - `scripts/download-fonts.ps1`:
    - Requests `https://fonts.googleapis.com/css2?family=Geologica:wght@300..500&family=Wix+Madefor+Text:wght@400..600&display=swap` with a current Chrome User-Agent.
    - Downloads only the `cyrillic` and `latin` subset woff2 files into `wwwroot/fonts/`.
    - Writes `fonts.css`, which has local `url()`s and keeps each `unicode-range`.
  - Run the script, and commit its output plus `OFL.txt`.
  - Copy Bootstrap Icons 1.11.3 `bootstrap-icons.min.css` and `fonts/bootstrap-icons.woff2` into `lib/bootstrap-icons/`, from the npm package `bootstrap-icons@1.11.3` (`npm pack`, or a download from cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3).
- [ ] **Step 6: Write `tokens.css` and `base.css`.**
  - `tokens.css`:
    - Colour, type, spacing, radius and motion tokens.
    - The Bootstrap mapping: `--bs-body-bg: var(--paper)`, `--bs-body-color: var(--ink)`, `--bs-border-color: var(--rule)`, `--bs-link-color: var(--ink)`, `--bs-primary: var(--accent)`, `--bs-border-radius: 2px` (and the -sm/-lg variants), `--bs-focus-ring-color` set to the accent at 40%.
    - Fallback `@font-face` rules `"Geologica Fallback"` and `"Wix Madefor Text Fallback"` on `local("Arial")`. Set `size-adjust`, `ascent-override` and `descent-override` by comparing a rendered Cyrillic paragraph in each face; record the measured values in a comment.
  - `base.css`:
    - Body text: Wix Madefor 15px with line-height 1.55. Headings: Geologica with `text-wrap: balance`.
    - `.num { font-variant-numeric: tabular-nums }`.
    - Focus ring: 2px solid `--accent`, offset 2px.
    - `.skip-link`.
    - The reduced-motion rule.
- [ ] **Step 7: Check in a browser.** Link `fonts.css`, `tokens.css` and `base.css` in `App.razor` after Bootstrap. Remove the Google Fonts and jsDelivr links, and point the Bootstrap Icons link at the local copy. Then, in Playwright on `/`:
  - `document.fonts.check('16px "Wix Madefor Text"')` is true after load.
  - The Network panel shows no request to `fonts.googleapis.com` or `cdn.jsdelivr.net`.
- [ ] **Step 8: Commit.** `git commit -m "feat(design): tokens, self-hosted Bulgarian-capable fonts, PriceFormat, DecimalInput, QuantityCalculator"`

### Task 5: Cart cookie, cart endpoints, flash messages

**Files:**
- Create: `src/NaturalStoneImpex.Api/Storefront/CartCookie.cs`, `Storefront/CartStore.cs`, `Storefront/Flash.cs`, `Endpoints/CartEndpoints.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs` (register `ICartStore` scoped, `AddHttpContextAccessor`, `app.MapCartEndpoints()`)
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/CartCookieTests.cs`, `CartStoreTests.cs`

**Interfaces:**
- Consumes: `DecimalInput.TryParse`, `QuantityCalculator.MaxQuantity`, `AdminCsrfHeaderMiddleware` (it already guards `/api/cart/items`)
- Produces:
  - `record CartLine(int ProductId, decimal Quantity)`
  - `CartCookie.Name = "nsi_cart"`
  - `CartCookie.MaxLines = 50`
  - `CartCookie.Parse(string? value) : IReadOnlyList<CartLine>`
  - `CartCookie.Serialize(IEnumerable<CartLine> lines) : string`, in the format `"12:13.2|7:1"`
  - `interface ICartStore`, with members:
    - `IReadOnlyList<CartLine> Lines`
    - `int Count`
    - `void Add(int productId, decimal quantity)`, which merges into an existing line
    - `void SetQuantity(int productId, decimal quantity)`
    - `void Remove(int productId)`
    - `void Clear()`
  - `Flash.Set(HttpResponse, string code)`
  - `Flash.Take(HttpContext) : string?`
  - Flash codes: `"added"` and `"removed"`
  - Endpoints, all with form bodies and antiforgery:
    - `POST /cart/add`, with fields `productId`, `quantity` (default 1) and `returnUrl`. It redirects to `returnUrl` if that is local, else `/cart`, and sets flash `added`.
    - `POST /cart/update`, with fields `productId` and `quantity`. It redirects to `/cart`.
    - `POST /cart/remove`, with field `productId`. It redirects to `/cart` and sets flash `removed`.
    - `POST /api/cart/items`, with a JSON body `{ productId, quantity }`. It returns 204, or 400 `{ error }`.
  - Every endpoint ignores product IDs that don't exist, are inactive or are priced 0, and sets no flash for them.

- [ ] **Step 1: Write the failing tests.**
```csharp
// CartCookieTests
[Fact] public void Round_trips_lines()                       // Serialize→Parse equals input
[Fact] public void Uses_invariant_decimal_point()            // 13.2m under bg-BG → "12:13.2"
[Fact] public void Garbage_returns_empty()                   // Parse("%%%") → empty; Parse(null) → empty
[Fact] public void Drops_invalid_quantities()                 // "1:-2|2:0|3:abc|4:20000" → only valid lines (none here)
[Fact] public void Merges_duplicate_ids()                     // "5:1|5:2.5" → single line 5:3.5
[Fact] public void Caps_at_50_lines()                         // 500 lines → 50

// CartStoreTests (DefaultHttpContext with request cookie set; read Set-Cookie from response)
[Fact] public void Add_merges_and_writes_cookie()
[Fact] public void Cookie_is_httponly_secure_lax_30_days()
[Fact] public void Clear_expires_cookie()
```
- [ ] **Step 2: Run the tests to confirm they fail.** `dotnet test --filter "FullyQualifiedName~Cart"`. Expected: FAIL.
- [ ] **Step 3: Implement.**
  - `CartStore` reads `HttpContext.Request.Cookies` once per request.
  - It writes with `Response.Cookies.Append(CartCookie.Name, value, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Expires = now + 30 days, IsEssential = true, Path = "/" })`.
  - The endpoints check products through `AppDbContext` (`IsActive && PriceWithVat > 0`).
- [ ] **Step 4: Run the tests to confirm they pass.** Expected: PASS.
- [ ] **Step 5: Check from the command line.** With the host running:
  - `curl -k -X POST https://localhost:5001/api/cart/items -H "Content-Type: application/json" -d "{\"productId\":43,\"quantity\":2}"` returns 400, because the CSRF header is missing.
  - The same request with `-H "X-NSI-Request: 1"` returns 204 and a `Set-Cookie: nsi_cart=...` header.
- [ ] **Step 6: Commit.** `git commit -m "feat(cart): cookie cart store, cart endpoints, flash messages"`

### Task 6: Storefront layout shell, SEO head, 404 and error pages

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Layout/MainLayout.razor`, `Components/Shared/SeoHead.razor`, `Components/Shared/PageScript.razor`, `Components/Shared/Toast.razor`, `Components/Pages/NotFound.razor`, `Components/Pages/Error.razor`, `Components/_Imports.razor` (extend)
- Create: `src/NaturalStoneImpex.Client/wwwroot/css/nsi/components.css`, `css/nsi/storefront.css`, `js/storefront/page-script.js`, `js/storefront/menu.js`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`: `UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true)` and `UseExceptionHandler("/error", createScopeForErrors: true)` for non-`/api` paths
- Modify: `src/NaturalStoneImpex.Api/Components/App.razor`. Load `lib/bootstrap/bootstrap.bundle.min.js`, `js/modals.js` and `js/print.js` only when `PageRenderMode` is not null (admin pages). Load `js/visualizer.js` only when the request path is `/visualizer`. Always load `js/storefront/focus.js`.
- Create: `src/NaturalStoneImpex.Client/wwwroot/js/storefront/focus.js`. On `Blazor.addEventListener('enhancedload', ...)`, it moves focus to the page's `h1`, which gets `tabindex="-1"`. This covers spec §8.

**Interfaces:**
- Consumes: `ICartStore.Count`, `Flash.Take`
- Produces:
  - `MainLayout`, the storefront layout: skip link, header, `main#main`, footer and toast.
  - `<SeoHead Title Description CanonicalPath ImagePath? JsonLd? />`. It renders `<PageTitle>` as "{Title} | Natural Stone Impex", plus the meta description, the canonical link (absolute, built from the request scheme and host), the `og:*` tags and an optional `<script type="application/ld+json">`.
  - `<PageScript Src="js/storefront/x.js" />`, which loads a module that exports `onLoad`, `onUpdate` and `onDispose` and survives enhanced navigation (the documented pattern for JS in static SSR).
  - The shop contact constants, in `Components/ShopInfo.cs`: `Phone`, `PhoneHref`, `Email`, `Address`, `Hours` (two lines) and `MapUrl`. Until the owner supplies real values, these hold the current placeholders and a `// TODO(owner): real contact data` marker.

- [ ] **Step 1: Build `MainLayout`** to spec §6.6 Header:
  - The wordmark links to `/`.
  - Nav links: Каталог `/catalog`, Визуализатор `/visualizer`, Контакти `/contacts`, and Количка `/cart`, whose count badge appears only when the count is above 0.
  - Active link: compare `NavigationManager` paths and add a class that draws a 2px accent underline.
  - Phones: a `<details class="menu">` "Меню" disclosure, plus `menu.js` to close it on navigation.
  - Footer: the shop name, contact lines from `ShopInfo`, links Каталог and Контакти, and "© {year} Natural Stone Impex".
  - Every link that leads into `/admin` or to `/visualizer` carries `data-enhance-nav="false"`. Both pages need a full load, for WASM and for `visualizer.js`.
- [ ] **Step 2: Build `NotFound.razor` and `Error.razor`.**
  - `NotFound.razor`: `@page "/not-found"`, `[ExcludeFromInteractiveRouting]`, text "Тази страница не съществува.", links Начало and Каталог.
  - `Error.razor`: `@page "/error"`, text "Нещо се обърка. Опитайте отново или ни се обадете." followed by `ShopInfo.Phone`.
- [ ] **Step 3: Write `components.css`.**
  - Buttons: `.btn-primary` with the accent fill, `.btn-outline` with a 1px ink outline, `.link-quiet`.
  - Inputs: label above; `.field-help` and `.field-error` share one slot; minimum height 44px.
  - Segmented switch: `.seg`.
  - Toast: `.toast-line`.
  - Hairline lists.
- [ ] **Step 4: Check in a browser.**
  - `/nope` returns HTTP 404 with the designed page, verified with `curl -k -I`.
  - Tab from the top: the skip link appears first and focus moves to `main`.
  - After an enhanced navigation, focus is on the new page's `h1`.
  - At 375px the menu opens and closes.
  - The page loads no `.wasm`, `bootstrap.bundle.min.js` or `visualizer.js`.
- [ ] **Step 5: Commit.** `git commit -m "feat(storefront): layout shell, SEO head, page-script loader, 404 and error pages"`

---

## Phase 4: Storefront pages

Each page task below makes four changes:
- It deletes the old Client page with the same route in the same commit, which avoids an ambiguous route.
- It adds `@attribute [ExcludeFromInteractiveRouting]` and `@layout MainLayout`.
- It uses `SeoHead`.
- It uses only the classes from `components.css` and `storefront.css`, so no inline styles.

### Task 7: Storefront query service

**Files:**
- Create: `src/NaturalStoneImpex.Api/Storefront/StorefrontQueryService.cs` (with interface `IStorefrontQueryService` in the same folder)
- Modify: `Program.cs` (`AddScoped<IStorefrontQueryService, StorefrontQueryService>()`)
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/StorefrontQueryServiceTests.cs`

**Interfaces:**
- Produces (`IStorefrontQueryService`):
  - `Task<IReadOnlyList<ProductListDto>> GetFeaturedAsync(int count)`: active products with an image and `PriceWithVat > 0`. Categories are ordered by name and take turns (round-robin); products within a category are ordered by name.
  - `Task<ProductDto?> GetActiveProductAsync(int id)`: null if the product is missing or inactive.
  - `Task<IReadOnlyList<ProductListDto>> GetByIdsAsync(IReadOnlyCollection<int> ids)`: includes inactive products, with the `IsActive` flag kept.
  - `Task<IReadOnlyDictionary<int, string>> GetCategoryPhotosAsync()`: category ID → `ImagePath` of the first active product with an image, by name.
  - `Task<IReadOnlyList<(int Id, DateTime UpdatedAt)>> GetSitemapProductsAsync()`

- [ ] **Step 1: Write the failing tests** (InMemory, same seeding style as `VisualizerProductsTests`).
```csharp
[Fact] public async Task Featured_round_robins_categories_and_skips_unpriced_or_imageless()
// categories A(a1,a2,a3), B(b1), C(c1 price 0), D(d1 no image) → count 4 → [a1, b1, a2, a3]
[Fact] public async Task Active_product_lookup_hides_inactive()   // inactive id → null
[Fact] public async Task GetByIds_returns_inactive_with_flag()     // IsActive false preserved, unknown id omitted
[Fact] public async Task Category_photos_pick_first_by_name_with_image()
```
- [ ] **Step 2: Run them to confirm they fail.** `dotnet test --filter "FullyQualifiedName~StorefrontQuery"`. Expected: FAIL.
- [ ] **Step 3: Implement** with `AsNoTracking`, projecting into the existing DTOs the same way `ProductService` does, with `UnitDisplay` set to "кг" or "м²". Do not run parallel queries on one context.
- [ ] **Step 4: Run them to confirm they pass.** Expected: PASS.
- [ ] **Step 5: Commit.** `git commit -m "feat(storefront): query service for home, product, cart, sitemap"`

### Task 8: Home page

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Pages/Home.razor`, `Components/Shared/CategoryIndex.razor`, `Components/Shared/ProductCard.razor`
- Delete: `src/NaturalStoneImpex.Client/Pages/Public/Home.razor`

**Interfaces:**
- Consumes: `IStorefrontQueryService.GetFeaturedAsync` and `GetCategoryPhotosAsync`, `ICategoryService.GetAllAsync`, `PriceFormat.Money`, `ShopInfo`
- Produces:
  - `<CategoryIndex Categories Photos SelectedId? />`: rows of a 52px thumbnail, the name and a tabular count. Each row links to `/catalog?categoryId={id}`, and "Всички" links to `/catalog`.
  - `<ProductCard Product ReturnUrl />`, built to spec §6.6:
    - The add button is a form that posts to `/cart/add`, with `<AntiforgeryToken/>`, `data-enhance`, hidden `productId`, `quantity=1` and `returnUrl`.
    - When priced 0, the card shows "Цена при запитване" and an "Запитване" link to `/contacts` instead.
    - The image has `loading="lazy"`, `width="600"`, `height="600"`, and alt set to the product name.

- [ ] **Step 1: Build the page sections from spec §7.1.**
  - Hero, using the exact copy from the spec: the headline, then "Гнайс, варовик, мрамор, гранит и речни камъни. Цени на м² и на кг, с ДДС.", then the button "Разгледайте каталога".
  - 2×2 sample board: the first 4 featured products, with captions under the photos.
  - "Видове камък": `CategoryIndex` in 3 columns on desktop and 1 on phones, showing only categories with a product count above 0.
  - "Избрани продукти": up to 8 `ProductCard`s, 4 or 2 per row.
  - Visualizer band: a photo and "Вижте камъка върху снимка на вашия двор.", with the link "Опитайте визуализатора" (full load: `data-enhance-nav="false"`).
  - Contact strip from `ShopInfo`.
  - SEO: Title "Естествен камък за настилки и облицовки", with a one-sentence description.
- [ ] **Step 2: Check in a browser.**
  - `/` at 375, 768, 1024 and 1440px matches the artifact's Direction A home mockup.
  - It loads no `.wasm`.
  - Clicking "+" on a card redirects back to `/`, the toast reads "Добавено в количката", and the header count goes up.
  - Refresh keeps the count.
- [ ] **Step 3: Commit.** `git commit -m "feat(storefront): home page in SSR"`

### Task 9: Catalog page

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Pages/Catalog.razor`, `Components/Shared/Pagination.razor`, `src/NaturalStoneImpex.Client/wwwroot/js/storefront/catalog-search.js`
- Delete: `src/NaturalStoneImpex.Client/Pages/Public/Catalog.razor`, `Components/Pagination.razor` (only if nothing in the admin uses it; check with grep first)
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/CatalogUrlTests.cs`

**Interfaces:**
- Consumes: `IProductService.GetAllAsync(categoryId, search, page, 24, includeInactive: false)`, `ICategoryService.GetAllAsync`, `CategoryIndex`, `ProductCard`
- Produces: `CatalogUrl.Build(int? categoryId, string? search, int page) : string`, in `Storefront/CatalogUrl.cs`. It omits defaults (no `page=1`, no empty `search`) and escapes the search text.

- [ ] **Step 1: Write the failing test.**
```csharp
[Theory]
[InlineData(null, null, 1, "/catalog")]
[InlineData(3, null, 1, "/catalog?categoryId=3")]
[InlineData(3, "мрамор плочи", 2, "/catalog?categoryId=3&search=%D0%BC%D1%80%D0%B0%D0%BC%D0%BE%D1%80%20%D0%BF%D0%BB%D0%BE%D1%87%D0%B8&page=2")]
[InlineData(null, "  ", 1, "/catalog")]
public void Build_omits_defaults(int? cat, string? q, int page, string expected)
```
- [ ] **Step 2: Run it to confirm it fails, implement `CatalogUrl.Build`, then run it to confirm it passes.** Expected: FAIL first, then PASS.
- [ ] **Step 3: Build `Catalog.razor`.**
  - Parameters: `[SupplyParameterFromQuery] int? CategoryId`, `string? Search`, `int Page`. A value below 1 is treated as 1.
  - Search: a GET form (`method="get" action="/catalog" data-enhance`) with `name="search"` and `data-permanent` on the input, plus a hidden `categoryId` when one is set.
  - Layout per spec §7.2. Pagination uses links built by `CatalogUrl.Build`.
  - Empty state: "Няма продукти за „{Search}“", with an "Изчисти търсенето" link (`CatalogUrl.Build(CategoryId, null, 1)`) and `CategoryIndex`.
  - The canonical path is `CatalogUrl.Build(...)`.
  - An unknown `CategoryId` returns an empty grid with the "Всички" link (not a 404).
  - `catalog-search.js` submits through `Blazor.navigateTo(url, { replaceHistoryEntry: true })` 300ms after the last input.
- [ ] **Step 4: Check in a browser.**
  - A home category link lands filtered.
  - Back and refresh keep the category, search and page.
  - Typing "мрамор" updates the results without losing focus.
  - It works with JavaScript disabled (submit with Enter).
- [ ] **Step 5: Commit.** `git commit -m "feat(storefront): catalog with URL state, SSR search and pagination"`

### Task 10: Product page with calculator

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Pages/ProductDetail.razor`, `Storefront/ProductJsonLd.cs`, `src/NaturalStoneImpex.Client/wwwroot/js/storefront/calculator.js`, `js/storefront/zoom.js`
- Delete: `src/NaturalStoneImpex.Client/Pages/Public/ProductDetail.razor`
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/ProductJsonLdTests.cs`

**Interfaces:**
- Consumes: `IStorefrontQueryService.GetActiveProductAsync`, `QuantityCalculator`, `ICartStore.Add`, `Flash.Set`, `PriceFormat`, `ShopInfo`
- Produces: `ProductJsonLd.Build(ProductDto p, string absoluteUrl, string? absoluteImageUrl) : string`

- [ ] **Step 1: Write the failing tests.**
```csharp
[Fact] public void Includes_offer_price_in_eur_when_priced()       // "price":"20.45","priceCurrency":"EUR"
[Fact] public void Omits_price_when_zero()                          // no "price" property
[Fact] public void Availability_instock_when_stock_positive()        // "https://schema.org/InStock"
[Fact] public void Availability_backorder_when_stock_zero_or_negative()
[Fact] public void Has_no_sku()
```
- [ ] **Step 2: Run them to confirm they fail, implement with `System.Text.Json` (`Utf8JsonWriter`, invariant numbers), then run them to confirm they pass.**
- [ ] **Step 3: Build `ProductDetail.razor`** (`@page "/products/{Id:int}"`).
  - If `GetActiveProductAsync` returns null, call `NavigationManager.NotFound()`.
  - Layout and copy exactly as spec §7.3. Availability reads:
    - Stock above 0: "В наличност: {PriceFormat.Quantity}".
    - Otherwise: "Доставка по заявка. Ще ви се обадим за срок."
  - The description is shown as plain text with line breaks preserved.
  - The calculator form:
    - `<EditForm FormName="add-to-cart" Enhance Model="Calc" OnValidSubmit="Add">`.
    - `[SupplyParameterFromForm] CalcForm Calc`, whose fields are `Mode`, `Length`, `Width`, `Area`, `Kilograms` and `Waste` (default true).
    - The m² mode switch shows either the dimension fields or the area field. Without JS, both groups render and the selected radio decides which one counts.
    - kg products show only the Kilograms field.
  - On submit, call `QuantityCalculator.Calculate`:
    - If invalid, show the error under `ErrorField`.
    - If valid, call `ICartStore.Add(Id, quantity)` and `Flash.Set(HttpContext.Response, "added")`, then `NavigateTo($"/products/{Id}")`.
    - The result line has `aria-live="polite"`.
  - The button text is "Добави {PriceFormat.Quantity(q, unit)} в количката", computed server-side from the posted or default values (4 × 3 with waste on the first render).
  - When priced 0: no form. Show an enquiry block, "Цена при запитване. Обадете се на {ShopInfo.Phone}.", with a `tel:` link.
  - `SeoHead`: Title is the product name; description is "{Category}, {Price}/{unit} с ДДС.", or "Цена при запитване."; the JSON-LD comes from `ProductJsonLd`.
  - The photo uses `fetchpriority="high"`, with no lazy loading.
  - `calculator.js` mirrors `DecimalInput` and `QuantityCalculator` for the live result and button label only. `zoom.js` opens the full image in a `<dialog>`.
- [ ] **Step 4: Check in a browser.**
  - Entering 4 by 3 with waste shows 13.20 м² and 269.94 € for product 43 (20.45 €).
  - Typing "4,5" gives the same result as "4.5".
  - An empty width shows "Въведете ширина в метри." under Ширина after submitting with JS off.
  - `/products/999999` returns 404, and so does an inactive product ID.
  - View-source contains `application/ld+json`.
- [ ] **Step 5: Commit.** `git commit -m "feat(storefront): product page with m² calculator, JSON-LD, 404"`

### Task 11: Cart page

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Pages/Cart.razor`, `Storefront/CartViewModel.cs`, `src/NaturalStoneImpex.Client/wwwroot/js/storefront/cart.js`
- Delete: `src/NaturalStoneImpex.Client/Pages/Public/Cart.razor`, `Components/CartIcon.razor`
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/CartViewModelTests.cs`

**Interfaces:**
- Consumes: `ICartStore.Lines`, `IStorefrontQueryService.GetByIdsAsync`, `/cart/update`, `/cart/remove`, `PriceFormat`
- Produces:
  - `CartViewModel.Build(IReadOnlyList<CartLine> lines, IReadOnlyList<ProductListDto> products) : CartViewModel`, with:
    - `Rows`: each row has `Product`, `Quantity`, `LineTotal`, `bool Orderable`, `string? Problem`, `bool BackOrder`.
    - `SubtotalWithoutVat`, `Vat`, `TotalWithVat`: summed over orderable rows.
    - `bool HasOrderable`.
  - The `Problem` texts: inactive gives "Продуктът вече не се предлага.", priced 0 gives "Цена при запитване.", and an unknown ID drops the row.
  - `BackOrder` is true when the quantity is greater than the stock.

- [ ] **Step 1: Write the failing tests.**
```csharp
[Fact] public void Totals_cover_only_orderable_rows()
[Fact] public void Inactive_and_unpriced_rows_are_flagged_not_counted()
[Fact] public void Unknown_ids_are_dropped()
[Fact] public void Quantity_above_stock_is_backorder_but_orderable()
[Fact] public void Totals_are_quantity_times_snapshot_prices()     // 13.2 × 20.45 = 269.94, VAT 13.2 × 3.41 = 45.012 → shown via PriceFormat as 45.01
// add to CartStoreTests (Task 5 file):
[Fact] public void Update_with_step_adds_and_floors_at_one_cent()  // qty 1, step -1 → 0.01; qty 2.5, step 1 → 3.5
```
- [ ] **Step 2: Run them to confirm they fail. Implement `CartViewModel`, and add the optional `step` field to `/cart/update` (quantity + step, minimum 0.01). Run them to confirm they pass.**
- [ ] **Step 3: Build `Cart.razor`** to spec §7.4.
  - Each row is a `<form method="post" action="/cart/update" data-enhance>` with a quantity input (`inputmode="decimal"`, value shown via `PriceFormat` without a unit) and − / + buttons. Without JS, the buttons submit a `step` field of −1 or 1, which the endpoint adds to the quantity (minimum 0.01).
  - The remove form posts to `/cart/remove`.
  - `cart.js` posts the row form on change, debounced 400ms.
  - BackOrder rows show "доставка по заявка".
  - The summary reads "Без ДДС", "ДДС", "Общо с ДДС", then "Доставка: уточнява се по телефона".
  - "Към поръчката" is disabled unless `HasOrderable`.
  - Empty state: "Количката е празна", a "Към каталога" link and `CategoryIndex`.
- [ ] **Step 4: Check in a browser.**
  - Changing a quantity to "2,5" updates the line and the totals.
  - The remove link removes the line.
  - Editing the `nsi_cart` cookie in devtools to `1:abc|999999:2` still renders the page.
- [ ] **Step 5: Commit.** `git commit -m "feat(storefront): cookie cart page with per-row enhanced forms"`

### Task 12: Checkout and confirmation

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Pages/Checkout.razor`, `Components/Pages/OrderConfirmation.razor`, `Storefront/CheckoutHandler.cs`, `Storefront/CheckoutForm.cs`, `src/NaturalStoneImpex.Client/wwwroot/js/storefront/checkout.js`, `js/storefront/copy.js`
- Delete: `src/NaturalStoneImpex.Client/Pages/Public/Checkout.razor`, `Pages/Public/OrderConfirmation.razor`
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/CheckoutHandlerTests.cs`

**Interfaces:**
- Consumes: `ICartStore`, `CartViewModel`, `IOrderService.CreateAsync(CreateOrderRequest)`
- Produces:
  - `CheckoutForm`, which ports the current Client checkout model field for field, with the same DataAnnotations and Bulgarian messages. Read it from the deleted `Checkout.razor` first.
  - New check on `CheckoutForm`: when the customer is a company, `Eik` must match `^\d{9}(\d{4})?$`, with the message "ЕИК трябва да съдържа 9 или 13 цифри."
  - `CheckoutHandler.PlaceAsync(CheckoutForm form, CartViewModel cart) : Task<CheckoutOutcome>`
  - `record CheckoutOutcome(bool Success, string? OrderNumber, string? Error)`
  - The handler sends only orderable rows. When there are none, it returns `Error = "Количката е празна."` without calling the order service. On success, the caller clears the cart.

- [ ] **Step 1: Write the failing tests** (InMemory DB with `.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))`, using the real `OrderService`).
```csharp
[Fact] public async Task Places_order_with_only_orderable_rows()          // inactive row excluded from CreateOrderRequest.Items
[Fact] public async Task Empty_cart_creates_no_order()                     // Orders.Count() stays 0 (Review Focus 3)
[Fact] public async Task Stock_zero_does_not_block_order()                 // product stock 0 → Success true
[Fact] public void Company_eik_must_be_9_or_13_digits()                    // Validator.TryValidateObject on CheckoutForm
```
- [ ] **Step 2: Run them to confirm they fail, implement, then run them to confirm they pass.**
- [ ] **Step 3: Build `Checkout.razor`.**
  - The page uses `<EditForm FormName="checkout" Enhance>`, `DataAnnotationsValidator` and a `ValidationSummary`. The summary sits at the top and links each message to its field id.
  - Segmented switches for customer type and delivery method. The address group renders only when delivery is selected; `checkout.js` toggles it instantly.
  - `autocomplete` values: `name`, `tel`, `organization`, `street-address`. ЕИК has `inputmode="numeric"`.
  - The summary column comes from `CartViewModel`. On phones a sticky bar shows the total and the "Изпрати поръчката" button.
  - On success: `ICartStore.Clear()`, then `NavigateTo($"/order-confirmation/{OrderNumber}")`.
  - If `HasOrderable` is false on GET, redirect to `/cart`.
  - `checkout.js` disables the submit button and sets `aria-busy` on submit.
- [ ] **Step 4: Build `OrderConfirmation.razor`** with the exact copy from spec §7.6.
  - The copy button uses `copy.js` and shows "Копирано" for 2 seconds.
  - Below it, `ShopInfo` phone, address and hours.
  - `SeoHead` adds `noindex`, through an extra `Robots` parameter on `SeoHead` (add it).
- [ ] **Step 5: Check in a browser.**
  - Place orders as an individual (pickup) and as a company (delivery with ЕИК "123456789").
  - Both reach the confirmation page, and the cart is empty afterwards.
  - With JS off, a double-click on submit still produces one order. The second POST finds an empty cart and redirects to `/cart`.
  - The order appears in the admin list.
- [ ] **Step 6: Commit.** `git commit -m "feat(storefront): SSR checkout with autofill and EIK check, truthful confirmation"`

### Task 13: Contacts, sitemap, robots

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Pages/Contacts.razor`, `Storefront/SitemapBuilder.cs`, `Endpoints/SeoEndpoints.cs`
- Delete: `src/NaturalStoneImpex.Client/Pages/Public/Contacts.razor`
- Modify: `Program.cs` (`app.MapSeoEndpoints()`)
- Test: `tests/NaturalStoneImpex.Api.Tests/Storefront/SitemapBuilderTests.cs`

**Interfaces:**
- Produces:
  - `SitemapBuilder.Build(string baseUrl, IEnumerable<(int Id, DateTime UpdatedAt)> products) : string`
  - `SitemapBuilder.Robots(string baseUrl) : string`
  - `GET /sitemap.xml` (`application/xml`) and `GET /robots.txt` (`text/plain`)

- [ ] **Step 1: Write the failing tests.**
```csharp
[Fact] public void Sitemap_lists_static_pages_and_products()   // contains /, /catalog, /contacts, /visualizer, /products/43; lastmod yyyy-MM-dd
[Fact] public void Robots_disallows_private_paths_and_points_to_sitemap()
// contains "Disallow: /admin", "/cart", "/checkout", "/api", and "Sitemap: https://host/sitemap.xml"
```
- [ ] **Step 2: Run them to confirm they fail, implement, then run them to confirm they pass.**
- [ ] **Step 3: Build `Contacts.razor`** to spec §7.7: the `ShopInfo` fields, the phone as a `tel:` link and selectable text, the email, a map link, and the cookie note "Използваме само необходими бисквитки: за количката и за вход в администрацията."
- [ ] **Step 4: Check.** `curl -k https://localhost:5001/sitemap.xml` returns valid XML with product URLs. `curl -k https://localhost:5001/robots.txt` matches the test.
- [ ] **Step 5: Commit.** `git commit -m "feat(storefront): contacts page, sitemap.xml, robots.txt"`

### Task 14: Storefront performance and accessibility pass

**Files:**
- Modify: storefront pages and CSS as the findings require

- [ ] **Step 1: Run Lighthouse mobile** (chrome-devtools MCP `lighthouse_audit`) on `/`, `/catalog` and `/products/43`. Expected: Performance ≥ 90, Accessibility ≥ 90, CLS < 0.1.
- [ ] **Step 2: Check the budget.** For each of those pages, sum the transferred HTML, CSS, JS and fonts, excluding images. Expected: under 250 KB, with zero `.wasm` and zero `_framework/*.dll` requests (`list_network_requests`).
- [ ] **Step 3: Fix the findings and re-run until the expectations hold.** Typical fixes are image `width`/`height`, preloading the two text-font files, and contrast.
- [ ] **Step 4: Commit.** `git commit -m "perf(storefront): meet Lighthouse and transfer budgets"`

---

## Phase 5: Visualizer

### Task 15: Visualizer island and new cart

**Files:**
- Create: `src/NaturalStoneImpex.Api/Components/Pages/Visualizer.razor`
- Move: `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor` → `src/NaturalStoneImpex.Client/Components/VisualizerApp.razor` (remove `@page`, `PageTitle` and the h1/intro)
- Create: `src/NaturalStoneImpex.Client/Services/CartApi.cs` (+ `ICartApi`)
- Delete: `src/NaturalStoneImpex.Client/Services/CartService.cs`, `Models/CartItem.cs`, `Layout/MainLayout.razor`, `Layout/Footer.razor`, `Components/ProductCard.razor`, and any other Client file that `grep` shows is unused after this
- Modify: `src/NaturalStoneImpex.Client/Program.cs`, `Routes.razor` (`DefaultLayout` → `AdminLayout`)

**Interfaces:**
- Consumes: `POST /api/cart/items` (Task 5), `CsrfHeaderHandler` (Task 3)
- Produces: `ICartApi.AddAsync(int productId, decimal quantity) : Task<string?>`, which returns null on success or a Bulgarian error

- [ ] **Step 1: Build the SSR page.**
  - `@page "/visualizer"`, `[ExcludeFromInteractiveRouting]`, `MainLayout`, `SeoHead` ("Визуализатор на настилки").
  - The h1 "Визуализатор" and the intro sentence from the current page, followed by `<VisualizerApp @rendermode="new InteractiveWebAssemblyRenderMode(prerender: false)" />`.
  - A static skeleton block of the same height as the uploader sits inside the island's placeholder, so the layout doesn't jump.
- [ ] **Step 2: Rewire `VisualizerApp`.**
  - Replace `CartService.AddItem` with `ICartApi.AddAsync(product.Id, quantity)`.
  - On success, show the toast "Добавено в количката" with a link "Към количката" (`href="/cart"`, `data-enhance-nav="false"`). On error, show the message inline.
- [ ] **Step 3: Restyle the island** with the tokens and `components.css` classes. Replace `card`/`alert` styles with spec components and remove inline `style` attributes. Behaviour stays the same.
- [ ] **Step 4: Build and test.** `dotnet build` and `dotnet test`. Expected: green.
- [ ] **Step 5: Check the network.** `/` and `/catalog` load no `.wasm`; `/visualizer` does.
- [ ] **Step 6: Check in a browser.**
  - Upload the sample photo, tap an area, pick a stone and add to the cart.
  - The toast appears. "Към количката" fully loads `/cart`, which shows the item.
  - From the cart, the header "Визуализатор" link fully loads the visualizer again (Review Focus 4).
- [ ] **Step 7: Commit.** `git commit -m "feat(visualizer): WASM island on SSR page, cookie cart via /api/cart/items"`

---

## Phase 6: Admin

Shared rules for Tasks 16–20:
- Pages stay in `Client/Pages/Admin`, keep `@attribute [Authorize]`, and keep their logic.
- Change markup classes only, plus the behaviour each task names.
- Remove every inline `style="..."` and `var(--nsi-...)` use.

### Task 16: Admin shell, admin CSS, login and receipt layouts

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Layout/AdminLayout.razor` (top bar, replacing the sidebar), `src/NaturalStoneImpex.Api/Components/Layout/LoginLayout.razor`, `Components/Pages/Admin/Login.razor` (markup)
- Create: `src/NaturalStoneImpex.Client/wwwroot/css/nsi/admin.css`, `src/NaturalStoneImpex.Client/Layout/PrintLayout.razor`
- Modify: `src/NaturalStoneImpex.Client/Pages/Admin/Receipt.razor` (`@layout PrintLayout`; its print CSS rules are kept)

- [ ] **Step 1: Build the top bar** to spec §6.7.
  - Wordmark "NSI Администрация".
  - Links Табло `/admin`, Продукти, Категории, Поръчки, Доставки `/admin/invoices`.
  - On the right: "Към сайта" (full load) and an "Изход" button that calls `IAuthService.LogoutAsync`.
  - It scrolls horizontally on phones, and the active link gets a 2px accent underline.
- [ ] **Step 2: Write `admin.css`.**
  - Body text 14px; content on `--surface`.
  - `.data-table`: hairline rows, `th` in `--stone` at 13px, `.num` cells right-aligned.
  - `.status`: a small 8px square marker in `--ok`, `--warn` or `--danger`, followed by the status text.
  - `.inline-confirm`.
  - Form grid.
  - The skeleton row shape, shown after 300ms.
- [ ] **Step 3: Restyle the login** to the Direction A login style: a centred 360px panel, the wordmark, labels above the fields, and the primary button "Вход".
- [ ] **Step 4: Check in a browser.** Login, the dashboard shell at 375 and 1440px, and print preview of a receipt, which shows no chrome.
- [ ] **Step 5: Commit.** `git commit -m "feat(admin): top bar shell, admin styles, login and print layouts"`

### Task 17: Dashboard

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Pages/Admin/Dashboard.razor`

- [ ] **Step 1: Restyle to spec §7.9.**
  - Summary line `<h1>Табло</h1>`, then "{Pending} чакащи · {Confirmed} потвърдени · {Completed} завършени · {TotalProducts} продукта", using `OrderStatsDto` with tabular figures.
  - Two columns:
    - "Чакащи поръчки": `GetRecentAsync`, filtered to pending, with columns Номер, Клиент, Получена (`dd.MM.yyyy, HH:mm`), Сума (`PriceFormat.Money`), and a link to the order.
    - "Ниска наличност": `GetLowStockAsync(10)`, with negative values in `--danger` via `PriceFormat.Signed`.
  - Skeleton rows replace the spinner. Each section keeps its own error and empty states: "Няма чакащи поръчки." and "Няма продукти с ниска наличност."
- [ ] **Step 2: Check in a browser** at 375 and 1440px, with data, with no data, and with the API stopped (the error state).
- [ ] **Step 3: Commit.** `git commit -m "feat(admin): action-first dashboard"`

### Task 18: Orders, inline confirm, negative stock

**Files:**
- Modify: `src/NaturalStoneImpex.Api/Services/IOrderService.cs`, `Services/OrderService.cs` (`ConfirmAsync`, new `GetStockShortagesAsync`), `Controllers/OrdersController.cs` (new action, drop the `OrderConfirmErrorDto` branch)
- Modify: `src/NaturalStoneImpex.Client/Services/IOrderService.cs`, `Services/OrderService.cs`, `Pages/Admin/Orders.razor`, `Pages/Admin/OrderDetail.razor`
- Test: `tests/NaturalStoneImpex.Api.Tests/OrderConfirmTests.cs`

**Interfaces:**
- Produces:
  - API `IOrderService.GetStockShortagesAsync(int id) : Task<List<InsufficientStockDetail>>`: the lines where `stock − ordered < 0`, with `Available` = current stock.
  - Endpoint: `[Authorize] GET /api/orders/{id}/stock-check` returns that list.
  - `ConfirmAsync` no longer returns `OrderConfirmErrorDto`. It always decrements stock and confirms a pending order.
  - Client: `IOrderService.GetStockShortagesAsync(int id) : Task<List<StockShortageDetail>>`.

- [ ] **Step 1: Write the failing tests** (InMemory with `TransactionIgnoredWarning` ignored).
```csharp
[Fact] public async Task Confirm_with_zero_stock_confirms_and_goes_negative()   // stock 0, qty 8.2 → Status Confirmed, Stock -8.2
[Fact] public async Task Stock_check_lists_lines_that_would_go_negative()        // stock 5, qty 8.2 → one detail, Available 5
[Fact] public async Task Stock_check_is_empty_when_enough_stock()
[Fact] public async Task Confirm_non_pending_still_throws()                      // InvalidOperationException unchanged
```
- [ ] **Step 2: Run them to confirm they fail.** `dotnet test --filter "FullyQualifiedName~OrderConfirm"`. Expected: the first test fails, because today's code returns the shortage error.
- [ ] **Step 3: Implement the API change.** Delete the shortage block in `ConfirmAsync`. Add `GetStockShortagesAsync` and the controller action.
- [ ] **Step 4: Run them to confirm they pass.** Expected: PASS, and the full `dotnet test` stays green.
- [ ] **Step 5: Build the inline confirm in `OrderDetail` and `Orders`.**
  - Clicking "Потвърди" calls `GetStockShortagesAsync`, then swaps the button for `.inline-confirm` with one of these texts:
    - With shortages: "{ProductName} ще стане {PriceFormat.Signed(Available − Ordered, Unit)}. Потвърди? Да / Не", one line per shortage.
    - Without: "Потвърди поръчката? Да / Не".
  - "Да" calls `ConfirmAsync` and refreshes. "Не" restores the button. Escape also cancels.
  - Remove the old shortage-error UI.
  - Restyle the orders list and the detail page with `.data-table` and `.status`.
- [ ] **Step 6: Check in a browser.** Confirm an order whose product has stock 0. The warning names the product and the negative amount, "Да" confirms, and the dashboard's low-stock list shows the negative stock in red.
- [ ] **Step 7: Commit.** `git commit -m "feat(orders): confirmation never blocks; inline confirm warns about negative stock"`

### Task 19: Products, product form, categories

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Pages/Admin/Products.razor`, `ProductForm.razor`, `Categories.razor`

- [ ] **Step 1: Restyle.**
  - Lists use `.data-table`, with prices via `PriceFormat` and stock via `PriceFormat.Signed` in `--danger` when negative.
  - Forms are single-column, with labels above, helper and error text in one slot, and a sticky save bar at the bottom ("Запази" and "Отказ").
  - The product form's price auto-calculation (`PriceWithVat = PriceWithoutVat + VatAmount`) stays exactly as it is.
  - The existing modals stay, restyled to tokens.
- [ ] **Step 2: Check in a browser.** Create, edit (including image and texture upload) and deactivate a product. Create, rename and delete a category. The uploads still work, because they send the CSRF header through the handler.
- [ ] **Step 3: Commit.** `git commit -m "feat(admin): restyle products and categories"`

### Task 20: Deliveries (invoices) and old CSS removal

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Pages/Admin/Invoices.razor`, `InvoiceCreate.razor`, `InvoiceDetail.razor`
- Delete: `src/NaturalStoneImpex.Client/wwwroot/css/site.css`, and the parts of `wwwroot/css/app.css` other than `#blazor-error-ui` (move that rule into `base.css`, then delete `app.css`); unused Client components (`ToastNotification.razor` if unused; check with `grep`)
- Modify: `src/NaturalStoneImpex.Api/Components/App.razor` (drop the old CSS links)

- [ ] **Step 1: Restyle the three invoice pages** the same way as Task 19.
- [ ] **Step 2: Remove the old CSS.** Delete the files and links, then grep for leftovers: `grep -rnE "nsi-|animate-fade|heading-accent|feature-icon|category-gradient" src`. Expected: no matches outside `css/nsi/*`.
- [ ] **Step 3: Click through every admin page and storefront page at 1440px.** Expected: no unstyled elements.
- [ ] **Step 4: Commit.** `git commit -m "feat(admin): restyle deliveries; remove legacy CSS"`

---

## Phase 7: Verify

### Task 21: Full verification and docs

**Files:**
- Modify: `CLAUDE.md`, `README.md`, `docs/technical-specification.md` (architecture, auth, cart, render modes), `docs/api-endpoints.md` (new endpoints: `/api/auth/me`, `/api/auth/logout`, `/api/cart/items`, `/api/orders/{id}/stock-check`; confirm no longer returns shortages)

- [ ] **Step 1: Run the build and tests.** `dotnet build` (expected: no new warnings) and `dotnet test` (expected: all green).
- [ ] **Step 2: Run the Playwright pass** with screenshots of every storefront and admin page at 375, 768, 1024 and 1440px. Save them to `.playwright-mcp/verify/` (gitignored).
- [ ] **Step 3: Run the scenario list from spec §10.**
  - The full order flow: from the product page and from the visualizer, then refresh, then checkout as an individual and as a company, then the confirmation, then admin confirm with stock 0, then the negative stock check.
  - Admin refresh and a deep link to `/admin/orders`.
  - The ReturnUrl attack.
  - The network check: no `.wasm` on the storefront.
  - The fallback-font check: `document.fonts` shows both faces loaded, and a Cyrillic heading's computed font is Geologica.
- [ ] **Step 4: Re-run Lighthouse** on home, catalog and product (Task 14 thresholds).
- [ ] **Step 5: Update the docs** listed above to match what was built.
- [ ] **Step 6: Commit.** `git commit -m "docs: document SSR storefront, cookie auth, cart, new endpoints"`
