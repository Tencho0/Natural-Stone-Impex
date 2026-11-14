# Technical Specification — Natural Stone Impex

## 1. Project Overview

**Natural Stone Impex** is a web-based inventory and order management system for a building materials shop in Bulgaria. The system consists of two parts:

- **Customer-facing storefront** (no authentication required) — allows customers to browse products, view prices, place orders, and view contact information.
- **Admin panel** (single admin account, password-protected) — allows the shop owner to manage inventory, categories, products, incoming deliveries (invoices), and customer orders.

**Language**: Bulgarian only (all UI text, labels, buttons, messages in Bulgarian).
**Currency**: Euro (EUR), displayed as `XX.XX €`.
**Date format**: DD.MM.YYYY (Bulgarian standard).

---

## 2. Tech Stack

| Layer       | Technology                                      |
|-------------|--------------------------------------------------|
| Host        | One ASP.NET Core 10 Blazor Web App (`NaturalStoneImpex.Api`): API controllers, Razor components and static assets on one origin |
| Frontend    | Storefront: static SSR Razor components. Admin and visualizer: Blazor WebAssembly (`NaturalStoneImpex.Client`). Bootstrap 5 with the design tokens |
| Database    | SQL Server + Entity Framework Core (Code First)  |
| Auth        | Admin cookie for the browser; JWT Bearer for API clients (single admin user) |
| Images      | Local file storage (`wwwroot/uploads`)           |

### 2.1 Render modes

| Area | Mode |
|---|---|
| `/`, `/catalog`, `/products/{id}`, `/cart`, `/checkout`, `/order-confirmation/{OrderNumber}`, `/contacts`, `/not-found`, `/error` | Static SSR. Pages opt out of interactive routing with `@attribute [ExcludeFromInteractiveRouting]` |
| `/visualizer` | Static SSR page with one WebAssembly island, `VisualizerApp` (prerender off) |
| `/admin/login` | Static SSR form |
| `/admin/**` (all other admin pages and the receipt) | Interactive WebAssembly routing, prerender off |

- `App.razor` sets the render mode of `Routes` from the matched page, so the storefront ships no .NET runtime: storefront pages load `blazor.web.js` and small page scripts, never `dotnet.*.wasm` or assemblies.
- Storefront pages call the API services (`IProductService`, `IOrderService`, ...) in-process; there is no HTTP hop. The WebAssembly pages call the API over HTTP on the same origin.
- Storefront forms (cart, checkout, product calculator) are enhanced form posts with antiforgery and Post/Redirect/Get. Every storefront page works without JavaScript.
- Links between the storefront and the admin are full page loads (`data-enhance-nav="false"`), because the render mode of the whole page changes.
- Missing or inactive products return HTTP 404 with the designed not-found page; other HTML errors show the designed error page. `/api` keeps the JSON error middleware.
- Build-time assets (CSS, JS, the WebAssembly framework files) are served by `MapStaticAssets`: brotli or gzip, fingerprinted names, immutable caching. Fonts are referenced by plain paths on purpose (they are preloaded) and are served `max-age=3600, must-revalidate`, uncompressed (woff2).
- `wwwroot/uploads` is runtime data: it is excluded from the static web assets and from the publish (`Content Remove` in the Api csproj), and `UseUploadsStaticFiles` serves `/uploads` from disk. A file added at runtime is served at once; a deleted one returns 404. A deployment keeps its own uploads folder; `DbSeeder` copies the seed photos into it only when it seeds an empty database.

---

## 3. User Roles

### 3.1 Customer (Anonymous)
- No login required.
- Can browse catalog, view product details, add items to cart, place orders. The cart is kept in a cookie (section 4.4).
- Must provide personal or company information at checkout.

### 3.2 Admin (Single Account)
- Logs in with username + password on `/admin/login`, which sets an HttpOnly cookie (section 5.1).
- Credentials seeded into the database on first run.
- Default credentials: `admin` / `Admin123!` (must be changed on first login — optional, can be a future feature).
- Full access to inventory management, order management, invoice entry.

---

## 4. Customer-Facing Pages

### 4.1 Home Page (Начална страница)
- Welcome/landing page for the shop.
- Brief description of the business (building materials, natural stone, etc.).
- Featured products or categories section (optional — can show top categories with links to catalog).
- Call-to-action button leading to the catalog page.
- Shop contact summary (phone, address) in footer or on page.

### 4.2 Catalog Page (Каталог)
- Displays all products grouped or filterable by category.
- Left sidebar or top bar with category filter (list of all categories).
- Each product shown as a card:
  - Product image (thumbnail)
  - Product name
  - Price with ДДС (e.g., `24.00 €`) and the note "за м², с ДДС" (or "за кг, с ДДС")
  - A "+" icon button labelled "Добави {name} в количката" (priced 0: "Цена при запитване" and "обадете се за оферта", no button)
- Search bar to filter products by name.
- Pagination (24 products per page).
- Stock never blocks ordering (owner decision, 07.11.2026). A product without stock can still be added; the cart and checkout note "Доставка по заявка."
- A product priced 0 shows "Цена при запитване" and cannot be added to the cart.

### 4.3 Product Detail Page (Детайли за продукт)
- Full product image (larger view).
- Product name.
- Category name (as a breadcrumb or label).
- Description (if available).
- **Price with ДДС**: e.g., `24.00 €`
- **Price without ДДС**: e.g., `20.00 €`
- **ДДС amount**: e.g., `4.00 €`
- Unit of measurement (кг or м²).
- Quantity calculator: by dimensions (length × width, with an optional 10% waste allowance, on by default) or a direct quantity. Decimal input accepts "4,5" and "4.5". The server computes the result on submit (`QuantityCalculator`); a small script mirrors it live.
- "Добави ... в количката" button with the calculated quantity. Products priced 0 show an enquiry block with the shop phone instead.
- Stock line: "В наличност: 12.00 м²" when stock > 0, otherwise "Доставка по заявка. Ще ви се обадим за срок."
- JSON-LD `Product` with an `Offer` in EUR (no price for products priced 0).

### 4.4 Cart Page (Количка)
- List of all items added to cart:
  - Product name
  - Unit price (with ДДС)
  - Quantity (editable)
  - Row total (unit price × quantity)
- Remove item button per row.
- **Subtotal** (sum of all row totals, with ДДС).
- **Total ДДС** (sum of all VAT amounts).
- "Към поръчката" (Proceed to checkout) button.
- "Към каталога" (Continue shopping) link back to catalog.
- Lines that cannot be ordered (inactive, priced 0) are marked with the reason and left out of the total and the order.
- Cart state is a cookie, `nsi_cart`: up to 50 `productId:quantity` pairs, no prices or names. HttpOnly, Secure, SameSite=Lax. Expiry is 30 days from the last change. Malformed pairs are ignored and lines beyond 50 are dropped. Prices always come from the database (`GetByIdsAsync`). The cart survives refresh and is cleared after an order is placed.
- Each row posts its own change (`/cart/update`, `/cart/remove`); the visualizer island adds through `POST /api/cart/items`. The header cart count is rendered on the server from the cookie.

### 4.5 Checkout Page (Поръчка)

#### Step 1 — Customer Type Selection
The customer selects one of two options:
- **Физическо лице** (Individual / Regular client)
- **Фирма** (Company)

#### Step 2 — Customer Information

**If Физическо лице:**
| Field             | Bulgarian Label       | Required | Validation          |
|-------------------|-----------------------|----------|---------------------|
| Full Name         | Име и фамилия         | Yes      | Min 2 characters    |
| Phone Number      | Телефонен номер       | Yes      | Valid phone format   |
| Delivery Address  | Адрес за доставка     | Conditional | Required if delivery selected |

**If Фирма:**
| Field              | Bulgarian Label         | Required | Validation            |
|--------------------|-------------------------|----------|-----------------------|
| Company Name       | Име на фирмата          | Yes      | Min 2 characters      |
| EIK / Bulstat      | ЕИК / Булстат           | Yes      | 9 or 13 digits        |
| MOL                | МОЛ                     | Yes      | Min 2 characters      |
| Delivery Address   | Адрес за доставка       | Conditional | Required if delivery selected |
| Contact Person     | Лице за контакт         | Yes      | Min 2 characters      |
| Contact Phone      | Телефон за контакт      | Yes      | Valid phone format     |

#### Step 3 — Delivery Method
- **Вземане от обекта** (Pickup from shop) — no delivery fee.
- **Доставка** (Delivery) — delivery fee will be added by the admin after order placement. Display message: _"Цената за доставка ще бъде определена и добавена от нас след получаване на поръчката."_

#### Step 4 — Order Summary & Confirmation
- All steps are on one static SSR page (`/checkout`), validated on the server after submit; each error appears under its field and in a summary at the top.
- An order-summary column (a sticky bottom bar on phones) shows the items, subtotal, total ДДС and grand total; delivery is priced later by phone.
- "Изпрати поръчката" (Send order) button.
- On success: clear the cart cookie and redirect to `/order-confirmation/{OrderNumber}`: "Поръчка NSI-YYYYMMDD-XXXX е получена. Ще ви се обадим в работно време на посочения телефон, за да я потвърдим.", with a copy button and the shop's contact details. There is no public order lookup.

### 4.6 Contacts Page (Контакти)
- Shop name: Natural Stone Impex
- Address (to be provided by shop owner — use placeholder).
- Phone number (placeholder).
- Email (placeholder).
- Working hours (placeholder).
- Map link (placeholder coordinates).
- A short note about the cart and admin cookies (strictly necessary, so there is no consent banner).
- Contact details live in one place, `ShopInfo`.

---

## 5. Admin Panel

All admin pages are under the `/admin` route prefix and carry `@attribute [Authorize]`. The server enforces it on every full page load (refresh, deep link) through the admin cookie, and redirects a signed-out visitor to `/admin/login?ReturnUrl=...`.

### 5.1 Admin Login (Вход в администрацията)
- Simple login form: username + password. A static SSR page; credentials are checked by `IAuthService`.
- On success: sign in the `nsi_admin` cookie (HttpOnly, Secure, SameSite=Strict, 8 hours sliding) and redirect to `ReturnUrl` if it is a local path, otherwise to `/admin`. Off-site values such as `https://example.com` or `//example.com` are ignored.
- On failure: show error message "Невалидно потребителско име или парола."
- The WebAssembly admin learns who is signed in from `GET /api/auth/me`; "Изход" calls `POST /api/auth/logout`.

### 5.2 Admin Dashboard (Табло)
- Overview page with quick stats:
  - Total products count
  - Total orders count (pending / confirmed / completed)
  - Low stock alerts (products with quantity below a configurable threshold, e.g., 10)
  - A one-line summary, e.g. "3 чакащи · 5 потвърдени · 41 завършени · 115 продукта".
  - "Чакащи поръчки" (pending orders) and "Ниска наличност" (low stock, including negative stock in the danger colour).
- A top bar with links to all admin sections.

### 5.3 Category Management (Категории)
- List all categories in a table: ID, Name, Product Count.
- "Добави категория" (Add category) button → opens form/modal.
- Edit button per row → inline edit or modal.
- Delete button per row → confirmation dialog. Cannot delete if category has products (show error message).
- Category fields:
  - Name (Име) — required, unique, min 2 characters.

### 5.4 Product Management (Продукти)

#### Product List
- Table with columns: Image (thumbnail), Name, Category, Price with ДДС, Unit, Stock Quantity, Actions.
- Filter by category (dropdown).
- Search by name.
- Pagination.
- "Добави продукт" (Add product) button.
- Edit / Delete buttons per row.

#### Product Form (Add / Edit)
| Field              | Bulgarian Label          | Type         | Required | Notes                          |
|--------------------|--------------------------|--------------|----------|--------------------------------|
| Name               | Име на продукта          | Text         | Yes      | Min 2 chars, unique per category |
| Category           | Категория                | Dropdown     | Yes      | Select from existing categories |
| Description        | Описание                 | Textarea     | No       | Optional product description   |
| Price without VAT  | Цена без ДДС             | Decimal      | Yes      | e.g., 20.00                    |
| VAT Amount         | ДДС                      | Decimal      | Yes      | e.g., 4.00                     |
| Price with VAT     | Цена с ДДС               | Decimal      | Yes      | e.g., 24.00                    |
| Unit               | Мерна единица            | Dropdown     | Yes      | "кг" or "м²"                   |
| Stock Quantity     | Налично количество       | Decimal      | Yes      | Current stock. Default 0.      |
| Image              | Снимка                   | File upload  | No       | JPG/PNG, max 5MB               |

**VAT note**: All three price fields are entered manually by the admin. The system should auto-calculate Price with VAT = Price without VAT + VAT Amount as a convenience, but the admin can override. Validation: Price with VAT must equal Price without VAT + VAT Amount.

**Delete product**: Soft delete recommended (mark as inactive) to preserve order history. Or prevent deletion if product appears in any order.

### 5.5 Order Management (Поръчки)

#### Order List
- Table with columns: Order Number, Date, Customer Name/Company, Status, Total, Actions.
- Filter by status: Всички (All) / Чакаща / Потвърдена / Завършена.
- Sort by date (newest first by default).
- Pagination.

#### Order Detail
- Full customer information (type, name/company data, phone, address).
- Delivery method (pickup or delivery).
- Items table: Product Name, Unit, Quantity, Unit Price (with ДДС), ДДС Amount, Row Total.
- Subtotal, total ДДС, delivery fee (if applicable), grand total.
- **Status actions:**
  - If **Чакаща** (Pending):
    - "Потвърди поръчката" (Confirm order) button.
      - On confirm: status changes to Потвърдена. **Stock is decremented** for each item in the order.
      - Stock never blocks confirmation (owner decision, 07.11.2026): stock may go below zero. Before confirming, the button asks inline; when a line would leave stock below zero, the question names the product and the resulting amount, e.g. "Металик 5L ще стане −8.20 м². Потвърди? Да / Не" (from `GET /api/orders/{id}/stock-check`).
    - If delivery selected: input field to enter delivery fee (Цена за доставка) before or during confirmation.
    - "Откажи поръчката" (Cancel/Reject order) — optional. Sets a "cancelled" flag but doesn't change the 3 main statuses. Stock is NOT decremented for cancelled orders.
  - If **Потвърдена** (Confirmed):
    - "Маркирай като завършена" (Mark as completed) button → status changes to Завършена.
    - "Принтирай разписка" (Print receipt) button → opens printable receipt view.
  - If **Завършена** (Completed):
    - Read-only. Print receipt still available.

#### Receipt (Стокова разписка)
A print-friendly page/component rendered via `window.print()` (JS interop). Contains:
- **Header**: Shop name (Natural Stone Impex), address, phone, date of receipt.
- **Customer info**: Name or Company name + ЕИК + МОЛ (depending on type).
- **Items table**:
  | № | Продукт | Мерна ед. | Количество | Ед. цена без ДДС | ДДС | Ед. цена с ДДС | Общо |
  |---|---------|-----------|------------|-------------------|-----|-----------------|------|
- **Subtotal without ДДС**.
- **Total ДДС**.
- **Delivery fee** (if applicable, as a separate line).
- **Grand total with ДДС**.
- Footer: "Стокова разписка — не е официален данъчен документ" (This is not an official tax document).
- Styled with simple CSS for clean print output (no colors, borders only).

### 5.6 Invoice / Delivery Management (Доставки / Фактури)

#### Invoice List
- Table with columns: Invoice Number, Supplier, Invoice Date, Entry Date, Total Items, Actions.
- Sort by date (newest first).
- Pagination.
- "Нова доставка" (New delivery) button.

#### Invoice Form (New Delivery)
| Field           | Bulgarian Label     | Type     | Required | Notes                      |
|-----------------|---------------------|----------|----------|----------------------------|
| Supplier Name   | Доставчик           | Text     | Yes      | Name of the supplier       |
| Invoice Number  | Номер на фактура    | Text     | Yes      | Supplier's invoice number  |
| Invoice Date    | Дата на фактура     | Date     | Yes      | Date on the supplier invoice |

**Invoice Items** (dynamic list — admin adds rows):
| Field           | Bulgarian Label     | Type     | Required | Notes                          |
|-----------------|---------------------|----------|----------|--------------------------------|
| Product         | Продукт             | Dropdown | Yes      | Select from existing products  |
| Quantity        | Количество          | Decimal  | Yes      | Quantity delivered              |
| Purchase Price  | Покупна цена        | Decimal  | Yes      | Price per unit from supplier   |

**On save**:
- Invoice record is created.
- **Stock is automatically incremented** for each product by the delivered quantity.
- Entry date (дата на въвеждане) is set to current date/time automatically.

#### Invoice Detail (View)
- All invoice header fields.
- Items table with product name, quantity, purchase price, row total.
- Invoice total (sum of all row totals).
- Read-only after creation (invoices should not be editable to maintain audit integrity). If correction needed, admin creates a new invoice or manually adjusts stock via product edit.

---

## 6. Data Models (Summary)

Detailed schema in `database-schema.md`. High-level entities:

- **Category** — Id, Name
- **Product** — Id, Name, Description, CategoryId, PriceWithoutVat, VatAmount, PriceWithVat, Unit (enum: Kg, Sqm), StockQuantity, ImagePath, IsActive, CreatedAt, UpdatedAt
- **Order** — Id, OrderNumber (auto-generated), CustomerType (enum: Individual, Company), Status (enum: Pending, Confirmed, Completed), DeliveryMethod (enum: Pickup, Delivery), DeliveryFee, IsCancelled, CreatedAt, UpdatedAt
- **OrderCustomerInfo** — Id, OrderId, FullName, Phone, Address, CompanyName, Eik, Mol, ContactPerson, ContactPhone
- **OrderItem** — Id, OrderId, ProductId, ProductName (snapshot), Quantity, UnitPriceWithoutVat, VatAmount, UnitPriceWithVat, Unit
- **Invoice** — Id, SupplierName, InvoiceNumber, InvoiceDate, EntryDate, CreatedAt
- **InvoiceItem** — Id, InvoiceId, ProductId, Quantity, PurchasePrice
- **AdminUser** — Id, Username, PasswordHash

**Key design decisions:**
- OrderItem stores a **snapshot** of product name and prices at the time of order (so price changes don't affect historical orders).
- StockQuantity is a `decimal` to support fractional units (e.g., 2.5 кг, 3.75 м²).
- OrderNumber is auto-generated: format `NSI-YYYYMMDD-XXXX` (e.g., `NSI-20260218-0001`).

---

## 7. API Endpoints (Summary)

Detailed specification in `api-endpoints.md`. Overview:

### Auth
| Method | Endpoint          | Description       | Auth |
|--------|-------------------|-------------------|------|
| POST   | /api/auth/login   | Admin login (JWT for API clients) | No   |
| GET    | /api/auth/me      | Signed-in admin   | Admin |
| POST   | /api/auth/logout  | Sign the admin cookie out | No |

The admin UI signs in on the static SSR page `/admin/login` (cookie), not through `/api/auth/login`.

### Categories
| Method | Endpoint              | Description          | Auth  |
|--------|-----------------------|----------------------|-------|
| GET    | /api/categories       | List all categories  | No    |
| POST   | /api/categories       | Create category      | Admin |
| PUT    | /api/categories/{id}  | Update category      | Admin |
| DELETE | /api/categories/{id}  | Delete category      | Admin |

### Products
| Method | Endpoint                    | Description               | Auth  |
|--------|-----------------------------|---------------------------|-------|
| GET    | /api/products               | List products (paginated, filterable) | No |
| GET    | /api/products/{id}          | Get product detail        | No    |
| POST   | /api/products               | Create product            | Admin |
| PUT    | /api/products/{id}          | Update product            | Admin |
| DELETE | /api/products/{id}          | Soft-delete product       | Admin |
| POST   | /api/products/{id}/image    | Upload product image      | Admin |
| POST   | /api/products/{id}/texture  | Upload visualizer texture | Admin |
| GET    | /api/products/low-stock     | Low-stock products        | Admin |

### Orders
| Method | Endpoint                        | Description              | Auth  |
|--------|---------------------------------|--------------------------|-------|
| GET    | /api/orders                     | List all orders          | Admin |
| GET    | /api/orders/{id}                | Get order detail         | Admin |
| POST   | /api/orders                     | Place new order          | No    |
| GET    | /api/orders/{id}/stock-check    | Lines that would go below zero | Admin |
| PUT    | /api/orders/{id}/confirm        | Confirm order (never blocked by stock) | Admin |
| PUT    | /api/orders/{id}/complete       | Mark as completed        | Admin |
| PUT    | /api/orders/{id}/cancel         | Cancel order             | Admin |
| PUT    | /api/orders/{id}/delivery-fee   | Set delivery fee         | Admin |
| GET    | /api/orders/stats               | Dashboard counts         | Admin |
| GET    | /api/orders/recent              | Recent orders            | Admin |

### Invoices
| Method | Endpoint             | Description           | Auth  |
|--------|----------------------|-----------------------|-------|
| GET    | /api/invoices        | List all invoices     | Admin |
| GET    | /api/invoices/{id}   | Get invoice detail    | Admin |
| POST   | /api/invoices        | Create invoice (auto-updates stock) | Admin |

### Cart, visualizer and SEO
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST   | /api/cart/items | Add to the cart cookie (visualizer) | No (needs `X-NSI-Request: 1`) |
| POST   | /cart/add, /cart/update, /cart/remove | Storefront form posts (antiforgery, redirect) | No |
| GET    | /api/visualizer/products | Visualizer-enabled products | No |
| POST   | /api/visualizer/segment, /api/visualizer/segment/{sessionToken} | Segment a photo, refine the mask | No |
| GET    | /sitemap.xml, /robots.txt | SEO | No |
| GET    | /api/health | Health check | No |

---

## 8. Non-Functional Requirements

### 8.1 Performance
- Product catalog page should load within 2 seconds.
- Pagination: 24 items per page (catalog), 20 items per page (admin tables).

### 8.2 Security
- Admin password stored as bcrypt/PBKDF2 hash (ASP.NET Core Identity default).
- Admin cookie `nsi_admin`: HttpOnly, Secure, SameSite=Strict, 8-hour sliding expiry. JWT tokens (API clients) expire after 8 hours.
- CSRF: cookie-authenticated state-changing `/api` calls and `POST /api/cart/items` require the `X-NSI-Request: 1` header; storefront forms carry antiforgery tokens.
- `ReturnUrl` after login is accepted only as a local path.
- API validates all input (DTOs with DataAnnotations).
- One origin: no CORS.
- File upload validation: JPG/PNG only, max 5MB.

### 8.3 Error Handling
- API returns consistent error responses: `{ "error": "message" }` with appropriate HTTP status codes.
- The admin and the visualizer show user-friendly Bulgarian error messages; storefront forms show errors under each field.
- Exception handler middleware for `/api` (JSON). HTML pages use the designed error page ("Нещо се обърка. Опитайте отново или ни се обадете.") and a 404 page ("Тази страница не съществува.").

### 8.4 Responsive Design
- Customer-facing pages must be mobile-friendly (Bootstrap 5 responsive grid), checked at 375, 768, 1024 and 1440 px.
- Storefront budget: under 250 KB of HTML, CSS, JS and fonts (compressed) per page, Lighthouse mobile Performance and Accessibility at least 90, CLS under 0.1.
- Admin panel: desktop-first, but functional on tablets.

---

## 9. Future Considerations (Out of Scope for V1)

These features are explicitly **not** included in the first version but may be added later:
- Email/SMS notifications when order is placed or confirmed.
- Multiple admin users with role-based access.
- Product variants (e.g., different sizes/colors).
- Online payment integration.
- Export orders/invoices to Excel/PDF.
- Multilingual support (English).
- Advanced reporting and analytics dashboard.
- Customer accounts and order history.
- Automatic VAT calculation (currently admin enters all three price fields).
- Azure Blob Storage for images (currently local storage).
