# Epic 08: Invoice & Delivery Management

## Description
Admin can record incoming deliveries (invoices from suppliers). Each invoice contains a list of products and quantities received. Saving an invoice automatically increments stock for each product. The admin can view all past deliveries.

## Dependencies
- Epic 04 (Product Management) must be completed — products must exist to reference in invoices.
- Independent of Epics 05–07 (can be built in parallel with catalog/orders if desired).

## Stories

---

### Story 8.1: Invoice Entities and Migration

**As** a developer, **I want** the invoice entities in the database **so that** deliveries can be recorded and tracked.

**Acceptance Criteria:**
- [ ] `Invoice` entity created:
  - `Id` (int, PK)
  - `SupplierName` (string, required, max 200)
  - `InvoiceNumber` (string, required, max 50)
  - `InvoiceDate` (DateTime) — date on the supplier's invoice
  - `EntryDate` (DateTime) — auto-set to current date/time on creation
  - `CreatedAt` (DateTime)
- [ ] `InvoiceItem` entity created:
  - `Id` (int, PK)
  - `InvoiceId` (int, FK to Invoice)
  - `ProductId` (int, FK to Product)
  - `Quantity` (decimal(18,2), required, > 0)
  - `PurchasePrice` (decimal(18,2), required, ≥ 0) — price per unit from supplier
- [ ] Navigation properties: `Invoice.Items`, `InvoiceItem.Invoice`, `InvoiceItem.Product`
- [ ] `DbSet<Invoice>` and `DbSet<InvoiceItem>` added to `AppDbContext`
- [ ] EF Core migration created and applied

**Tasks:**
- Create `Models/Entities/Invoice.cs` and `Models/Entities/InvoiceItem.cs`
- Configure relationships in `AppDbContext`
- Create and apply migration

---

### Story 8.2: Invoice API Endpoints

**As** the admin, **I want** API endpoints to create and view invoices **so that** I can record deliveries and review history.

**Acceptance Criteria:**
- [ ] `GET /api/invoices` — list all invoices (admin only)
  - Returns: Id, SupplierName, InvoiceNumber, InvoiceDate, EntryDate, TotalItems (count of line items), TotalQuantity (sum)
  - Sorted by EntryDate descending (newest first)
  - Pagination: `page`, `pageSize` (default 20)
- [ ] `GET /api/invoices/{id}` — get invoice detail (admin only)
  - Returns: all invoice fields + items list (product name, quantity, purchase price, row total)
  - Invoice total (sum of all row totals)
- [ ] `POST /api/invoices` — create invoice (admin only)
  - Request: `{ supplierName, invoiceNumber, invoiceDate, items: [{ productId, quantity, purchasePrice }] }`
  - Validation:
    - SupplierName required
    - InvoiceNumber required
    - InvoiceDate required, not in the future
    - At least 1 item required
    - Each item: productId must exist, quantity > 0, purchasePrice ≥ 0
  - On save:
    - Create Invoice and InvoiceItem records
    - **Automatically increment StockQuantity** for each product by the delivered quantity
    - All within a database transaction
  - Returns: created invoice with HTTP 201
- [ ] Invoices are **immutable** — no PUT or DELETE endpoints

**Tasks:**
- Create DTOs: `InvoiceListDto`, `InvoiceDetailDto`, `InvoiceItemDto`, `CreateInvoiceRequest`, `CreateInvoiceItemRequest`
- Create `Services/IInvoiceService.cs` and `Services/InvoiceService.cs`
- Create `Controllers/InvoicesController.cs`
- Implement stock increment logic within a transaction
- Test via Swagger

---

### Story 8.3: Invoice List Page (Admin)

**As** the admin, **I want** to see all deliveries/invoices **so that** I can track incoming stock.

**Acceptance Criteria:**
- [ ] Page at `/admin/invoices` with title "Доставки"
- [ ] Table columns: №, Доставчик, Номер на фактура, Дата на фактура, Дата на въвеждане, Брой артикули, Действия
- [ ] Dates formatted as DD.MM.YYYY
- [ ] "Нова доставка" button → navigates to `/admin/invoices/new`
- [ ] "Виж" (View) button per row → navigates to `/admin/invoices/{id}`
- [ ] Pagination (20 per page)
- [ ] Sorted by entry date (newest first)
- [ ] Loading state

**Tasks:**
- Create `Services/IInvoiceService.cs` and `Services/InvoiceService.cs` in Blazor client
- Create `Pages/Admin/Invoices.razor`
- Implement table with pagination
- Wire up to API

---

### Story 8.4: New Invoice Form (Admin)

**As** the admin, **I want** a form to record a new delivery **so that** incoming stock is logged and inventory is automatically updated.

**Acceptance Criteria:**
- [ ] Page at `/admin/invoices/new` with title "Нова доставка"
- [ ] **Header fields:**
  - Доставчик (text input, required)
  - Номер на фактура (text input, required)
  - Дата на фактура (date picker, required, cannot be in the future)
- [ ] **Items section** (dynamic list):
  - Each row: Продукт (dropdown of all active products), Количество (decimal input), Покупна цена (decimal input), Row total (auto-calculated, read-only)
  - "Добави артикул" button to add a new row
  - Remove button (✕) per row (must keep at least 1 row)
  - Product dropdown shows: "{Product Name} ({Category Name}) — {Unit}"
- [ ] **Invoice total** displayed at the bottom (sum of all row totals)
- [ ] "Запази" (Save) button:
  - Validates all fields
  - On success: redirect to invoice list with success message "Доставката е записана. Наличностите са обновени."
  - On error: show validation errors
- [ ] "Отказ" (Cancel) button → back to invoice list
- [ ] Loading state on save
- [ ] Confirmation dialog before save: "Сигурни ли сте? След запазване наличностите ще бъдат автоматично обновени. Доставката не може да бъде редактирана."

**Tasks:**
- Create `Pages/Admin/InvoiceForm.razor`
- Load product list for dropdowns
- Implement dynamic item rows (add/remove)
- Implement auto-calculation of row totals and invoice total
- Implement form validation
- Add save confirmation dialog
- Wire up to POST API endpoint
- Handle success/error states

---

### Story 8.5: Invoice Detail Page (Admin)

**As** the admin, **I want** to view the details of a past delivery **so that** I can review what was received and at what price.

**Acceptance Criteria:**
- [ ] Page at `/admin/invoices/{id}` with title "Доставка №{InvoiceNumber}"
- [ ] **Header info:**
  - Доставчик: {SupplierName}
  - Номер на фактура: {InvoiceNumber}
  - Дата на фактура: {InvoiceDate} (DD.MM.YYYY)
  - Дата на въвеждане: {EntryDate} (DD.MM.YYYY)
- [ ] **Items table:**
  - Columns: №, Продукт, Мерна ед., Количество, Покупна цена, Общо
  - Row totals
- [ ] **Invoice total** at the bottom
- [ ] Entirely read-only (no edit buttons, no actions)
- [ ] Info note: "Доставките не могат да бъдат редактирани. При грешка, коригирайте наличностите ръчно от продуктовата страница."
- [ ] "Обратно към доставките" link

**Tasks:**
- Create `Pages/Admin/InvoiceDetail.razor`
- Call `GET /api/invoices/{id}` on load
- Display all invoice data in read-only format
- Style with Bootstrap
