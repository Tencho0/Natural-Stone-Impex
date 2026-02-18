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
| Frontend    | Blazor WebAssembly (Standalone) + Bootstrap 5    |
| Backend     | ASP.NET Core 8 Web API                           |
| Database    | SQL Server + Entity Framework Core (Code First)  |
| Auth        | JWT Bearer tokens (single admin user)            |
| Images      | Local file storage (wwwroot/uploads on API)      |

---

## 3. User Roles

### 3.1 Customer (Anonymous)
- No login required.
- Can browse catalog, view product details, add items to cart, place orders.
- Must provide personal or company information at checkout.

### 3.2 Admin (Single Account)
- Logs in with username + password.
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
  - Price with ДДС (e.g., `24.00 €`)
  - ДДС amount displayed (e.g., `вкл. 4.00 € ДДС`)
  - Unit (кг or м²)
  - "Добави в количката" (Add to cart) button
- Search bar to filter products by name.
- Pagination (e.g., 12 products per page).
- If a product is out of stock (quantity = 0), show "Изчерпан" (Out of stock) instead of the add-to-cart button.

### 4.3 Product Detail Page (Детайли за продукт)
- Full product image (larger view).
- Product name.
- Category name (as a breadcrumb or label).
- Description (if available).
- **Price with ДДС**: e.g., `24.00 €`
- **Price without ДДС**: e.g., `20.00 €`
- **ДДС amount**: e.g., `4.00 €`
- Unit of measurement (кг or м²).
- Quantity input + "Добави в количката" button.
- Stock availability indicator (e.g., "В наличност" / "Изчерпан").

### 4.4 Cart Page (Количка)
- List of all items added to cart:
  - Product name
  - Unit price (with ДДС)
  - Quantity (editable)
  - Row total (unit price × quantity)
- Remove item button per row.
- **Subtotal** (sum of all row totals, with ДДС).
- **Total ДДС** (sum of all VAT amounts).
- "Продължи към поръчка" (Proceed to checkout) button.
- "Продължи пазаруването" (Continue shopping) link back to catalog.
- Cart state stored in browser memory (Blazor in-memory state or localStorage via JS interop). Cart is lost on page refresh unless persisted via localStorage.

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
- Display all cart items with prices.
- Subtotal, total ДДС, grand total (without delivery fee — note that delivery fee is pending if delivery selected).
- Customer information summary.
- "Потвърди поръчката" (Confirm order) button.
- On success: show confirmation message with order number. Clear the cart.

### 4.6 Contacts Page (Контакти)
- Shop name: Natural Stone Impex
- Address (to be provided by shop owner — use placeholder).
- Phone number (placeholder).
- Email (placeholder).
- Working hours (placeholder).
- Embedded Google Maps iframe (optional, placeholder coordinates).

---

## 5. Admin Panel

All admin pages are under the `/admin` route prefix and require JWT authentication.

### 5.1 Admin Login (Вход в администрацията)
- Simple login form: username + password.
- On success: store JWT token, redirect to admin dashboard.
- On failure: show error message "Невалидно потребителско име или парола."

### 5.2 Admin Dashboard (Табло)
- Overview page with quick stats:
  - Total products count
  - Total orders count (pending / confirmed / completed)
  - Low stock alerts (products with quantity below a configurable threshold, e.g., 10)
  - Recent orders (last 5)
- Navigation sidebar with links to all admin sections.

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
      - If any item has insufficient stock, show error and do not confirm.
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
| POST   | /api/auth/login   | Admin login       | No   |

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

### Orders
| Method | Endpoint                        | Description              | Auth  |
|--------|---------------------------------|--------------------------|-------|
| GET    | /api/orders                     | List all orders          | Admin |
| GET    | /api/orders/{id}                | Get order detail         | Admin |
| POST   | /api/orders                     | Place new order          | No    |
| PUT    | /api/orders/{id}/confirm        | Confirm order            | Admin |
| PUT    | /api/orders/{id}/complete       | Mark as completed        | Admin |
| PUT    | /api/orders/{id}/cancel         | Cancel order             | Admin |
| PUT    | /api/orders/{id}/delivery-fee   | Set delivery fee         | Admin |

### Invoices
| Method | Endpoint             | Description           | Auth  |
|--------|----------------------|-----------------------|-------|
| GET    | /api/invoices        | List all invoices     | Admin |
| GET    | /api/invoices/{id}   | Get invoice detail    | Admin |
| POST   | /api/invoices        | Create invoice (auto-updates stock) | Admin |

---

## 8. Non-Functional Requirements

### 8.1 Performance
- Product catalog page should load within 2 seconds.
- Pagination: default 12 items per page (catalog), 20 items per page (admin tables).

### 8.2 Security
- Admin password stored as bcrypt/PBKDF2 hash (ASP.NET Core Identity default).
- JWT tokens expire after 8 hours.
- API validates all input (DTOs with DataAnnotations).
- CORS configured to allow only the Blazor client origin.
- File upload validation: JPG/PNG only, max 5MB.

### 8.3 Error Handling
- API returns consistent error responses: `{ "error": "message" }` with appropriate HTTP status codes.
- Blazor client shows user-friendly Bulgarian error messages.
- Global exception handler in API middleware.

### 8.4 Responsive Design
- Customer-facing pages must be mobile-friendly (Bootstrap 5 responsive grid).
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
