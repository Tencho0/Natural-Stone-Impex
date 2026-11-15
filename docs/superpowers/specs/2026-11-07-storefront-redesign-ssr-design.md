# Storefront redesign and static SSR storefront: design

Date: 07.11.2026
Status: revised after architecture research and builder review; awaiting owner approval
Visual reference: proposals page v2, https://claude.ai/artifact/NtVjHieQNGj8uLbQitm6A5 (Direction A "Образец", product page mockup, experience plan, admin mockup)

## 1. Intent

Natural Stone Impex sells natural stone (gneiss, limestone, marble, granite, river stones, decorative pebbles) to Bulgarian homeowners and builders, by m² or kg. The current UI was generated from a generic "premium earthy" template and reads as AI-made: bronze/gold/cream palette, gradients, icon circles, fade-ins everywhere, and brand fonts (Fraunces, Outfit) that contain no Cyrillic, so the site actually renders in Georgia and Segoe UI.

The owner wants a site that looks like the work of a first-rate designer and works better for the people buying stone. In the same effort the public storefront moves from Blazor WebAssembly to Blazor static server-side rendering (SSR): faster first load, real HTML for Google, and no WebAssembly download for shoppers. Today's Release publish ships 186 files, about 11 MB raw and 3.35 MB Brotli (measured 07.11.2026).

The two changes ship as one project because every storefront page is rewritten during the SSR move anyway. Restyling the WASM pages first and porting them afterwards would build each page twice.

## 2. Goals

1. Storefront pages (home, catalog, product, cart, checkout, confirmation, contacts, not-found) are static SSR HTML and never load the .NET WebAssembly runtime.
2. The admin panel and the visualizer stay interactive (WebAssembly).
3. The whole UI follows Direction A "Образец" (section 6) and shows none of the retired patterns in 6.8.
4. The experience fixes in section 7 are in: catalog state in the URL, an m² calculator with waste allowance, decimal input that accepts commas, a cart that survives refresh, a checkout the browser can autofill, a truthful confirmation, and price-on-request for products priced 0.
5. Search engines get titles, descriptions, canonical URLs, product structured data, a sitemap and correct 404 status codes.
6. Every current feature keeps working: ordering, admin CRUD, order confirmation and stock changes, deliveries (Доставки / invoices), receipt printing, the visualizer.
7. The admin stays signed in across refreshes and deep links (a side effect of 5.3, and a fix for today's behaviour).

## 3. Non-goals

- No database schema change and no change to existing endpoint routes or DTO shapes. Additions are limited to the list in 5.7.
- No dark mode in this project. Tokens are structured so it can be added later.
- No new product data: no extra photos, no product-code field, no slugs, no package sizes. The data has one photo per product; the design does not pretend otherwise.
- No i18n framework. Bulgarian strings stay hardcoded.
- No change to the visualizer's interaction model. It is restyled, wrapped as an island and connected to the new cart. A JavaScript port that removes WebAssembly from it entirely is a possible later project.

## 4. Anti-goals (it fails if...)

- ...a storefront page loads `dotnet.*.wasm` or any .NET assembly. (`_framework/blazor.web.js`, the small Blazor Web App script for enhanced navigation and forms, is allowed.)
- ...any visible text renders in a fallback font because the chosen face lacks a glyph.
- ...it still looks templated: gradient fills, icon circles, a gold bar under headings, uppercase eyebrow labels above every section, staggered fade-ups, every card lifting on hover, three equal feature cards.
- ...a customer can put a 0.00 € product in the cart, or the cart empties on refresh.
- ...the cart cookie can change what a customer pays. Prices always come from the database.
- ...a stock level of 0 or below blocks a customer from ordering.
- ...an existing admin workflow or the visualizer stops working.

## 5. Architecture

### 5.1 One host (decided after architecture research)

```
src/
  NaturalStoneImpex.Api      becomes the single Blazor Web App host: existing controllers + Razor components (static SSR storefront)
  NaturalStoneImpex.Client   becomes the Web App's WebAssembly project (admin, visualizer island, their services)
```

- `NaturalStoneImpex.Api` keeps every controller, service, middleware and test. It adds `AddRazorComponents().AddInteractiveWebAssemblyComponents()`, `App.razor`, `Routes.razor`, the storefront pages and components, the layout shell, and all static assets (CSS, fonts, JS, favicon).
- `NaturalStoneImpex.Client` is converted to a Web App client project (`StaticWebAssetProjectMode` Default, no `index.html`, no root components, no DevServer package). It keeps the admin pages, the visualizer component and their HttpClient services, which now call the API on the same origin.
- Storefront pages inject the existing API services (`IProductService`, `ICategoryService`, `IOrderService`) in-process and work with the API DTOs. No HTTP hop and no typed clients on the server.
- One process, one origin. CORS, the `Access-Control-Allow-Origin: *` static-file header and the client's `ResolveImageUrl` are removed. Image paths stay relative (`/uploads/products/...`).
- Dev: `dotnet run --project src/NaturalStoneImpex.Api` serves everything on https://localhost:5001. Port 5002 is retired.

Why: same-origin cookies, image URLs and WebGL textures with no CORS; in-process data access; a single deployment. A separate host would bring back CORS, cross-origin cookies and an HTTP layer for a one-admin shop.

### 5.2 Render modes and routing

| Area | Mode |
|---|---|
| `/`, `/catalog`, `/products/{id}`, `/cart`, `/checkout`, `/order-confirmation/{OrderNumber}`, `/contacts`, `/not-found` | Static SSR (default) |
| `/visualizer` | Static SSR page containing one island, `<VisualizerApp @rendermode="new InteractiveWebAssemblyRenderMode(prerender: false)" />` |
| `/admin/**` (all admin pages and the receipt) | Interactive WebAssembly routing, prerender off |

- `App.razor` sets the `Routes` render mode from the matched page's `RenderModeAttribute` (the documented "static SSR pages in an interactive app" pattern). Admin pages therefore route and lay out interactively inside WebAssembly, so `AdminLayout`'s logout and navigation handling keep working.
- `/admin/login` is a static SSR form page (5.3).
- Links that cross between the storefront and the admin use `data-enhance-nav="false"` (full page load), because switching the top-level render mode needs a reload.
- Existing routes stay unchanged.
- The receipt page gets its own minimal print layout under the admin; today it falls back to the public `MainLayout`.

### 5.3 Admin authentication (decided after architecture research)

In a Blazor Web App the server enforces `[Authorize]` on refresh and deep links, and it cannot see a JWT held in browser memory. Admin auth therefore moves to a cookie.

- Cookie authentication (HttpOnly, Secure, SameSite=Strict, sliding expiry of 8 hours) with `LoginPath = /admin/login` and `AccessDeniedPath` the same.
- A policy scheme selects JWT Bearer when an `Authorization` header is present and the cookie otherwise. `POST /api/auth/login` keeps issuing JWTs, so existing tests and tools still work.
- `/admin/login` is a static SSR `EditForm` that validates the credentials through the existing `IAuthService` and signs in the cookie. `ReturnUrl` is accepted only if it is a local path, which fixes today's open redirect.
- New `GET /api/auth/me` returns the admin's name. The WASM `AuthenticationStateProvider` calls it to know who is signed in. `POST /api/auth/logout` signs the cookie out.
- `@attribute [Authorize]` stays on every admin page, as CLAUDE.md requires. The server now enforces it.
- The WASM HttpClient sends the cookie automatically (same origin). The in-memory token handler (`AuthTokenHandler`) and `CustomAuthStateProvider` are replaced.
- CSRF: the admin cookie is SameSite=Strict. Cookie-authenticated state-changing API calls (including multipart uploads) also require a custom request header (`X-NSI-Request: 1`) that the WASM client always sends.
- Middleware order: `UseAuthentication`, `UseAuthorization`, then `UseAntiforgery`.

### 5.4 Cart

- A cookie, `nsi_cart`: a compact list of `productId:quantity` pairs, with no prices or names. HttpOnly, Secure, SameSite=Lax, 30-day sliding expiry, at most 50 lines. Malformed or oversized cookies are discarded silently.
- A `CartStore` service reads and writes it. Every render that shows prices loads the products in one query, through a new service method `GetByIdsAsync`. Inactive products and products priced 0 are flagged on the cart page and excluded from the total and checkout, with a plain Bulgarian reason.
- Stock never blocks the cart or checkout (section 7.3).
- Storefront pages change the cart with enhanced form posts (`Enhance`, `FormName`, `[SupplyParameterFromForm]`, antiforgery), then redirect (Post/Redirect/Get). Pages that set cookies do not use stream rendering.
- The visualizer island adds items through `POST /api/cart/items` on the same host, protected by antiforgery and sharing `CartStore`. The in-memory `CartService` singleton is removed.
- The header cart count is rendered server-side from the cookie on every request.
- The cookie is cleared after an order is created.
- Cart and admin cookies are strictly necessary, so no consent banner is needed. Contacts gets a short cookie note.

### 5.5 Interactivity on SSR pages

Every storefront page works without JavaScript. Small vanilla modules (`wwwroot/js/*.js`, under 8 KB combined) attach through the documented page-script pattern (`onLoad`, `onUpdate`, `onDispose`) so they survive enhanced navigation:

- The m² calculator updates live. The server computes the same result on POST (7.3).
- Catalog search: a GET form; with JS, it calls `Blazor.navigateTo(url, { replaceHistoryEntry: true })` 300 ms after typing stops. The input carries `data-permanent` so focus and text survive the update.
- Cart rows: one enhanced form per row, each with a unique `FormName`.
- Product photo: tap-to-zoom.
- Confirmation: a copy button for the order number.

Forms validate on the server after submit (.NET 10 static SSR has no client-side validation). Native HTML attributes (`required`, `inputmode`, `pattern`, `autocomplete`) give instant feedback where they can. No InteractiveServer circuits anywhere. `bootstrap.bundle.min.js` is not loaded on storefront pages.

### 5.6 SEO and delivery

- Every page: `<PageTitle>`, meta description, canonical URL (catalog canonical drops `page=1` and empty parameters), and Open Graph title, description and image. `<html lang="bg">`.
- Product pages: JSON-LD `Product` with name, image, category and description, and an `Offer` in EUR with availability. "InStock" when stock > 0, otherwise "BackOrder". Products priced 0 get no price. No `sku` (there is no code field).
- `/sitemap.xml` (home, catalog, contacts, visualizer, every active product) and `/robots.txt` (disallow `/admin`, `/cart`, `/checkout`, `/api`).
- Missing or inactive products return HTTP 404 with the designed page: `NavigationManager.NotFound()`, `Router NotFoundPage`, and `UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true)`. The storefront checks `IsActive` itself, because `GetByIdAsync` does not filter it.
- `ExceptionHandlingMiddleware` is scoped to `/api` (`UseWhen`). HTML pages use `UseExceptionHandler` with a designed error page.
- `MapStaticAssets` and `@Assets[...]` for build-time compression, fingerprints and immutable caching. `UseStaticFiles` stays for runtime uploads (`/uploads`).
- Prices and quantities are formatted by one shared helper with invariant culture ("12.50 €"), so server and WebAssembly print identically.
- SSR pages never run parallel queries on the scoped `DbContext`.

### 5.7 Additions to the API project (complete list)

1. Razor components hosting, `App.razor`, `Routes.razor`, storefront pages and components, static assets.
2. Cookie authentication, the policy scheme, `GET /api/auth/me`, `POST /api/auth/logout`, and the static SSR `/admin/login`.
3. `CartStore` and `POST /api/cart/items`.
4. New service methods for the storefront: `GetByIdsAsync`, the home product selection, and an active-only product lookup. Existing endpoints keep their behaviour.
5. Order confirmation allows stock to go below zero (7.9). This is the only business-rule change.
6. `QuantityCalculator`, the price formatting helper, `/sitemap.xml`, `/robots.txt`.
7. Middleware changes: `ExceptionHandlingMiddleware` scoped to `/api`, `UseExceptionHandler`, status-code re-execution, antiforgery, `MapStaticAssets`; CORS removed.

### 5.8 Performance budget

On a storefront page, cold cache, excluding product photos:

- HTML, CSS, JS (including `blazor.web.js`) and fonts together: under 250 KB compressed.
- No `dotnet.*.wasm` or assembly downloads.
- Lighthouse mobile: LCP under 2.5 s, CLS under 0.1.
- Every `<img>` has `width`/`height` or a fixed aspect-ratio box. Images below the first screen use `loading="lazy"`.

## 6. Design system (Direction A "Образец")

The design reads as a materials library: each stone is a labelled sample on a cool mineral ground. Photography carries the warmth; the interface stays quiet and precise. Taste-skill dials: variance 6, motion 2, density 4 on the storefront and 7 in the admin.

### 6.1 Colour tokens

```
--paper        #E8E9E6   page ground
--surface      #F4F5F3   inputs, raised panels, admin content
--ink          #1C2023   text
--stone        #5A6165   secondary text (meets 4.5:1 on --paper and --surface)
--rule         #C7CBCA   hairlines, input borders
--accent       #2F4A5C   slate blue: primary action, active state, cart count
--accent-hover #253C4B
--on-accent    #F4F5F3
semantic: --ok #3D6B4A, --warn #8A6418, --danger #A1352B (text, borders, small markers; never large fills)
```

One accent only. No gradients. Bootstrap is themed through its `--bs-*` variables (body bg and colour, border colour, link colour, primary, focus ring), so its utilities keep working.

### 6.2 Typography

- Headlines: **Geologica** 300 and 400, tracking -0.02 to -0.03 em, `text-wrap: balance`.
- Everything else: **Wix Madefor Text** 400, 500, 600.
- Prices, quantities, order numbers and table numbers use `font-variant-numeric: tabular-nums`. No monospace face.
- Both faces contain Bulgarian letterforms (`locl` for `BGR`), active because pages declare `lang="bg"`. This was verified from the font files and by rendering `bg` against `ru` in a browser.
- Self-hosted `woff2`, subset to Latin + Cyrillic with `locl` kept, `font-display: swap`, and metric-matched fallback `@font-face` rules (`size-adjust`, `ascent-override`) to avoid layout shift. Nothing loads from Google or jsDelivr.
- Scale (px): 13, 14, 15 (body), 16, 18, 22, 26, 34, 48 (hero; 34 on phones). Body line-height 1.55; text measure under 70 characters.
- Sentence case everywhere. No all-caps labels.

### 6.3 Shape, space, depth

- Radius 2px on controls and photos. No pills.
- Hairlines (`1px --rule`) separate groups. No card shadows; one shadow token for floating menus only.
- Spacing scale (px): 4, 8, 12, 16, 20, 24, 32, 48, 56, 72. Container max 1200px; side gutter 20px on phones, 32px on desktop.

### 6.4 Motion

- One easing, `cubic-bezier(0.2, 0.7, 0.2, 1)`. Durations: 120 ms press, 160 ms hover and colour, 240 ms menus.
- Motion only answers an action: button press (1px down), cart count change, menu open, toast. No entrance or scroll animations. `prefers-reduced-motion` disables transitions.

### 6.5 Photography

- Square crops in grids, 4:3 on the product page, `object-fit: cover`, 2px radius.
- Captions sit under the photo, never over it.
- Each category is shown with a representative photo: the first active product with an image in that category, by name.

### 6.6 Components (storefront)

- **Header:** wordmark "Natural Stone Impex" in Geologica 500. Links: Каталог, Визуализатор, Контакти, Количка with a count. The active link gets a 2px accent underline. Phones get a "Меню" toggle.
- **Product card:** square photo; a meta line (category on the left, unit on the right); the name; the price "20.45 €" with "за м², с ДДС"; and a 40px square "+" add button (`aria-label="Добави в количката"`) that posts quantity 1. When priced 0, the card shows "Цена при запитване" and an "Запитване" link to Contacts instead, with no add button. Stock does not change the card.
- **Buttons:** primary has the accent fill; secondary has a 1px ink outline; tertiary is an underlined text link. Minimum height 44px.
- **Inputs:** label above, helper text below; an error replaces the helper in the same place. Minimum height 44px. Visible 2px accent focus ring.
- **Segmented switch:** for "По размери / Знам м²", "Физическо лице / Фирма" and the delivery method.
- **Toast:** one line ("Добавено в количката" plus a "Към количката" link), 4 s, `role="status"`.
- **Skeletons:** only where content loads after first paint (admin, visualizer). Shown after 300 ms, never for faster responses.
- **Icons:** Bootstrap Icons, self-hosted, outline only, used sparingly. No icon circles.

### 6.7 Admin variant

Same tokens at higher density: 14px body, `--surface` content on `--paper` chrome. A top bar replaces the dark sidebar. It holds the wordmark "NSI Администрация", the links Табло, Продукти, Категории, Поръчки, Доставки, and Изход on the right, and scrolls horizontally on phones.

### 6.8 Retired patterns

Remove all of these:
- The gradient hero and gradient category tiles.
- Icon circles.
- The "Защо да изберете нас" feature cards.
- The `heading-accent` bars.
- The `animate-fade-in-up` and stagger classes, and the hover lift on cards.
- Pill badges for units.
- The gold full-width add-to-cart button on cards.
- Inline `style="color: var(--nsi-...)"` in admin pages.
- The Fraunces/Outfit font link and the jsDelivr Bootstrap Icons link.

## 7. Pages and behaviour

### 7.1 Home `/`

1. A split hero:
   - Headline "Естествен камък за настилки, облицовки и дворове".
   - One subtext line listing the stone types, plus "Цени на м² и на кг, с ДДС.".
   - Primary button "Разгледайте каталога".
   - On the right, a 2×2 sample board of product photos with captions (name, category).
2. "Видове камък": every category with products, as an index of thumbnail, name and product count, linking to `/catalog?categoryId={id}`.
3. A product row: up to 8 active products that have an image and a price above 0. Categories take turns, and products within a category are in name order. 4 per row on desktop, 2 on phones. The row has no "most popular" claim.
4. A visualizer band: one sentence, a photo and the link "Опитайте визуализатора".
5. A contact strip: phone, email, address and opening hours.

The sample board uses the first four products of the same selection.

### 7.2 Catalog `/catalog`

- The page reads `categoryId`, `search` and `page` from the query string and writes them back on every change. Defaults are left out of the URL.
- Desktop: a category index on the left (name and count, "Всички" first) and the grid on the right (3 columns at 1024px, 4 at 1280px and wider). Phones: the categories scroll horizontally above a 2-column grid.
- Search uses a GET form, with the JS behaviour from 5.5.
- Pagination uses links, so search engines can crawl every page.
- When nothing matches, the page says "Няма продукти за „{search}“", offers a "Изчисти търсенето" link, and shows the category index.

### 7.3 Product `/products/{id}`

- Breadcrumb: Каталог / {category} / {name}.
- Left: the photo at 4:3. Clicking opens it full size, in a lightbox with JS or as a link to the image without.
- Right:
  - The category, plus "продава се на м²" or "на кг".
  - The name in Geologica 300.
  - The price with VAT, and the VAT amount once in small text.
  - Availability:
    - Stock above 0: "В наличност: 46.50 м²".
    - Stock at or below 0: "Доставка по заявка. Ще ви се обадим за срок."
    - A negative number is never shown.
- The product description is shown under the purchase block. It currently holds codes and notes such as the m³ price.
- m² products get the calculator:
  - A mode switch: "По размери" (Дължина, м; Ширина, м) or "Знам м²" (Площ, м²).
  - The checkbox "Добави 10% за фира и рязане", on by default.
  - A result line with the m² and the total.
  - A primary button that names the amount, e.g. "Добави 13.20 м² в количката".
  - The quantity rounds up to 0.01.
- kg products get a single "Количество, кг" field and no waste allowance.
- Number fields use `inputmode="decimal"`. The server and the JS both accept "4,5" and "4.5" and ignore spaces. Errors appear in Bulgarian under the field.
- The calculation lives in one pure C# class, `QuantityCalculator`, used by the POST handler. The JS mirrors it for live updates only; the server result is authoritative.
- Priced 0: no calculator and no add button. An enquiry block shows the shop phone instead.
- Under the purchase block: "Вземане от склада или доставка до обекта. Цената на доставката се уточнява по телефона. След поръчката ще ви се обадим, за да я потвърдим."

### 7.4 Cart `/cart`

- One row per line:
  - Photo and name.
  - Unit price.
  - A quantity field: decimal, accepts a comma, steppers move by 1.
  - Line total.
  - A remove link.
- Each row posts its own change.
- Summary: subtotal without VAT, VAT, and total with VAT, plus "Доставка: уточнява се по телефона".
- Lines that can't be ordered (inactive, priced 0) are marked with the reason and left out of the total and checkout.
- Lines without enough stock stay orderable and carry the note "доставка по заявка".
- Empty cart: "Количката е празна", a link to the catalog, and the category index.

### 7.5 Checkout `/checkout`

- One page: a static SSR `EditForm` with `Enhance`, `[SupplyParameterFromForm]` and `DataAnnotationsValidator`. Fields, labels, validation rules and the request DTO stay the same as today.
- Customer type and delivery method use segmented switches. The address field appears only when delivery is chosen: the server renders it after the switch posts, and JS may toggle it instantly.
- `autocomplete` values: `name`, `tel`, `organization`, `street-address`. ЕИК gets `inputmode="numeric"` and a 9 or 13 digit check.
- After submit, each error appears under its field, and a summary at the top links to each one.
- Desktop shows an order-summary column. On phones, the total and "Изпрати поръчката" sit in a sticky bottom bar.
- On success: clear the cart cookie and redirect to `/order-confirmation/{OrderNumber}`.

### 7.6 Confirmation `/order-confirmation/{OrderNumber}`

- Text: "Поръчка {NSI-YYYYMMDD-XXXX} е получена. Ще ви се обадим в работно време на посочения телефон, за да я потвърдим."
- The order number has a copy button.
- Below it, the shop's phone, address and opening hours.
- No order details: there is no public order lookup, and none is added.
- No exclamation marks.

### 7.7 Contacts, not found, error

- Contacts:
  - Phone, as a `tel:` link and as selectable text.
  - Email (kept from today's page).
  - Address and opening hours.
  - A map link.
  - The cookie note.
  - Real values only.
- Not found: HTTP 404, "Тази страница не съществува.", with links to home and the catalog.
- Error: "Нещо се обърка. Опитайте отново или ни се обадете.", with the phone number.

### 7.8 Visualizer `/visualizer`

- A static SSR page: title and intro text (indexable), then the `VisualizerApp` island.
- The behaviour stays the same, restyled with the new tokens.
- "Добави в количката" calls `POST /api/cart/items` and a toast confirms.
- Links out of the page use full page loads.

### 7.9 Admin

- Every admin page is restyled with the admin variant (6.7):
  - Tables use hairline rows and right-aligned tabular numbers.
  - Statuses are text with a small square marker in the semantic colour.
- The dashboard opens with:
  - A one-line summary in tabular figures: "3 чакащи · 5 потвърдени · 41 завършени · 115 продукта".
  - "Чакащи поръчки": number, customer, received, total, action.
  - "Ниска наличност", including any negative stock.
- **Order confirmation:**
  - Confirming always succeeds and reduces stock, which may go below zero (the owner's decision, 07.11.2026).
  - It asks inline, with no modal: "Потвърди" turns into "Потвърди поръчката? Да / Не".
  - When any line would leave stock below zero, the question names it: "Металик 5L ще стане −8.20 м². Потвърди? Да / Не".
  - The current shortage error in `OrderService.ConfirmAsync` becomes a non-blocking warning.
- Negative stock is shown in the admin in `--danger` text.
- Receipt: keeps its print layout and gets its own minimal admin layout on screen.
- Login: a static SSR page (5.3).

### 7.10 Copy rules

- Sentence case and active voice. No exclamation marks, no filler ("Защо да изберете нас" goes).
- Errors say what went wrong and what to do.
- One label per intent across the site (e.g. "Добави в количката" everywhere).
- Bulgarian copy may be edited where these rules require it.

## 8. Accessibility floor

WCAG 2.2 AA:
- 4.5:1 text contrast.
- 44px touch targets.
- Visible focus on every control.
- A label for every input.
- A skip-to-content link.
- Landmarks: `header`, `nav`, `main`, `footer`.
- Focus moves to the `h1` after enhanced navigation.
- `aria-live="polite"` on calculator results and toasts.
- Bulgarian alt text on every meaningful image (at least the product name).

## 9. Delivery order

Each phase ends with a working app.

1. **Guardrails.** Rewrite `.claude/skills/frontend-design/SKILL.md` to this spec. Mark `docs/planning/prompts/design-phase-*.md` and `docs/planning/design-plan.md` as superseded. Update the design rules in `docs/conventions.md`, and the stack, commands and structure sections in `CLAUDE.md` and `README.md`.
2. **Host.** Turn the Api project into the Blazor Web App host and Client into its WebAssembly project. Run every current page (storefront included) as interactive WebAssembly inside the new host, with the current look and current auth. Remove CORS and `ResolveImageUrl`.
3. **Auth.** Cookie auth, policy scheme, `/api/auth/me`, `/api/auth/logout`, the static SSR login (with the redirect fix), CSRF header, and the replaced WASM auth provider.
4. **Foundation.** Tokens, the Bootstrap variable mapping, fonts, base and component CSS, the layout shell (header, footer, skip link), formatting helper, middleware for 404, error and antiforgery. The new CSS lives beside the old `site.css` until phase 7.
5. **Storefront.** Rebuild home, catalog, product (with `QuantityCalculator`), cart (`CartStore`), checkout, confirmation, contacts, not-found and error as static SSR in the new design. Add the SEO endpoints and JS modules. Delete the WASM versions of these pages and the `CartService` singleton.
6. **Visualizer.** Make it an island, restyle it, and switch it to `POST /api/cart/items`.
7. **Admin.** Top bar, dashboard, inline confirm with the negative-stock warning and the `ConfirmAsync` change, tables, forms and receipt layout. Delete the old `site.css`, the `app.css` leftovers and unused components.
8. **Verify** (section 10).

## 10. Verification

- `dotnet build` with no new warnings; `dotnet test` green.
- New tests in `tests/NaturalStoneImpex.Api.Tests`:
  - `QuantityCalculator`: dimensions, area, waste on and off, rounding up, comma and dot input, invalid input.
  - `CartStore`: round-trip, 50-line cap, malformed cookie, merging quantities.
  - `ConfirmAsync`: allows negative stock and reports the lines that went negative.
  - Auth: the policy scheme picks Bearer with a header and the cookie without; `/api/auth/me` returns 401 when signed out.
  - Sitemap and robots content.
  - The price formatter under the bg-BG culture.
- Playwright, at 375, 768, 1024 and 1440 px, with before and after screenshots of every storefront and admin page:
  - A network check that storefront pages load no `.wasm` or .NET assemblies.
  - The full order flow:
    1. Add from the product page and from the visualizer.
    2. Refresh, and the cart is kept.
    3. Check out as an individual and as a company.
    4. See the confirmation.
    5. Confirm in the admin with stock 0 and see the negative stock.
  - Admin refresh and a deep link to `/admin/orders` stay signed in.
  - `/admin/login?ReturnUrl=https://example.com` does not redirect off-site.
- Lighthouse mobile on home, catalog and a product page:
  - Performance and Accessibility at least 90.
  - CLS under 0.1.
  - The budget in 5.8 is met.
- No text on any page renders in a system fallback font.

## 11. Open inputs and calls

### Needed from the owner before release

Development can continue with placeholders; release needs these:
- The real phone number, email, address and opening hours. The placeholder email in `ShopInfo` is misspelt (`info@naturalstonimpex.bg`, without the "e" of "stone"); take the real address from the owner, not from it.
- A logo, if one exists. Otherwise the typeset wordmark is final.

### Owner decisions (07.11.2026)

- Direction A, the m² calculator, the experience plan and Bulgarian letterforms are approved.
- Storefront static SSR; admin and visualizer stay interactive.
- Out-of-stock never blocks ordering, and stock may go below zero on confirmation.
- The visualizer keeps WebAssembly for now, as an island.

### Calls made by the designer, for the owner to check

- Host everything in the existing Api project instead of adding a new host project. Taken from the architecture research.
- Admin auth moves to an HttpOnly cookie, with JWT kept for API clients. This is required for server-enforced `[Authorize]`, and it also stops refresh logging the admin out.
- Bulgarian copy may be edited under the rules in 7.10. Taken as part of the approval.
- Cart in a cookie, holding IDs and quantities only.
- Products priced 0 cannot be added to the cart; they show "Цена при запитване".
- 10% waste allowance, on by default.
- No product code shown, and no `sku` in structured data. The product description stays visible.
- Light theme only for now.
- Bootstrap Icons kept, self-hosted.
- Confirming with negative stock shows a non-blocking warning naming the lines.

## 12. Left to the builder

- The component breakdown and file names beyond those named here, and the CSS file split.
- Admin skeleton shapes.
- How the cart rows' enhanced forms are wired.
- Lighthouse tuning and test fixtures.
- The exact wording of secondary messages, within the rules in 7.10.
