# Phase 5: Invoice & Delivery Management — Exact Claude Code Prompts

## Prerequisites

- Phase 4 completed and committed
- Orders working end-to-end (place, confirm with stock decrement, complete)
- Products exist in database with known stock quantities (note them down —
  you'll verify they increase after invoice entry)
- Fresh Claude Code session

---

## Session 5.1 — Invoice Entities + API (Epic 08, Stories 8.1–8.2)

### Prompt 1 (Plan)
```
Read docs/conventions.md, docs/database-schema.md (Invoice and InvoiceItem
sections), docs/api-endpoints.md (Invoices section — all 3 endpoints),
and planning/epics/08-invoice-management.md.

I want to implement Stories 8.1 and 8.2 — Invoice and InvoiceItem entities,
migration, and all invoice API endpoints.

Before writing any code, tell me:
- What will the Invoice and InvoiceItem entities look like (all fields)?
- What relationships and constraints will you configure?
- What DTOs will you create?
- How will the POST endpoint auto-increment stock (step by step)?
- What validation will the POST endpoint perform?
- What happens inside the database transaction?

Don't write any code yet.
```

> **Wait for response. Verify:**
> - Invoice: Id, SupplierName (max 200), InvoiceNumber (max 50), InvoiceDate, EntryDate (auto-set), CreatedAt
> - InvoiceItem: Id, InvoiceId (FK), ProductId (FK), Quantity (decimal 18,2), PurchasePrice (decimal 18,2)
> - InvoiceItem → Product with DeleteBehavior.Restrict
> - Invoice → InvoiceItem with Cascade delete
> - InvoiceNumber is NOT unique (different suppliers may reuse numbers)
> - POST creates invoice + items + increments stock ALL in one transaction
> - No PUT or DELETE endpoints (invoices are immutable)
> - Validation: at least 1 item, all products exist and active, quantity > 0, purchasePrice >= 0
> - InvoiceDate cannot be in the future
>
> **Correct if needed, then:**

### Prompt 2 (Execute)
```
Proceed with the implementation:

1. Create Entities in Models/Entities/:

Invoice.cs:
- Id (int, PK, Identity)
- SupplierName (string, required, max 200)
- InvoiceNumber (string, required, max 50)
- InvoiceDate (DateTime, required)
- EntryDate (DateTime, required, auto-set to UTC now on creation)
- CreatedAt (DateTime, required, auto-set to UTC now)
- Navigation: ICollection<InvoiceItem> Items

InvoiceItem.cs:
- Id (int, PK, Identity)
- InvoiceId (int, FK to Invoice, required)
- ProductId (int, FK to Product, required)
- Quantity (decimal 18,2, required, > 0)
- PurchasePrice (decimal 18,2, required, >= 0)
- Navigation: Invoice Invoice, Product Product

2. AppDbContext:
- Add DbSet<Invoice>, DbSet<InvoiceItem>
- Configure in OnModelCreating:
  - Invoice.SupplierName: max 200, required
  - Invoice.InvoiceNumber: max 50, required
  - Index on Invoice.EntryDate (for sorting)
  - InvoiceItem decimal fields: HasPrecision(18, 2)
  - Invoice → InvoiceItem: one-to-many, cascade delete
  - InvoiceItem → Product: many-to-one, DeleteBehavior.Restrict
  - InvoiceItem → Invoice: many-to-one, cascade

3. Migration named "AddInvoiceEntities", apply it.

4. Create DTOs in Models/DTOs/:

InvoiceListDto:
- Id, SupplierName, InvoiceNumber, InvoiceDate, EntryDate,
  TotalItems (int — count of line items), TotalQuantity (decimal — sum of quantities),
  InvoiceTotal (decimal — sum of quantity × purchasePrice for each item)

InvoiceDetailDto:
- Id, SupplierName, InvoiceNumber, InvoiceDate, EntryDate,
  Items (list of InvoiceItemDto), InvoiceTotal (decimal)

InvoiceItemDto:
- Id, ProductId, ProductName, Unit (int), UnitDisplay ("кг" / "м²"),
  Quantity, PurchasePrice, RowTotal (Quantity × PurchasePrice)

CreateInvoiceRequest:
- SupplierName (string, required, min 2, max 200)
- InvoiceNumber (string, required, min 1, max 50)
- InvoiceDate (DateTime, required)
- Items: List<CreateInvoiceItemRequest>

CreateInvoiceItemRequest:
- ProductId (int, required)
- Quantity (decimal, required, > 0)
- PurchasePrice (decimal, required, >= 0)

5. Create Services/IInvoiceService.cs and Services/InvoiceService.cs:

GetAllAsync(page, pageSize) → PaginatedResponse<InvoiceListDto>:
- Sorted by EntryDate descending (newest first)
- Compute TotalItems, TotalQuantity, InvoiceTotal in the query/mapping

GetByIdAsync(id) → InvoiceDetailDto:
- Include Items with Product navigation (for ProductName and Unit)
- Compute RowTotal and InvoiceTotal

CreateAsync(CreateInvoiceRequest) → response:
- Within a DATABASE TRANSACTION:
  a. Validate SupplierName, InvoiceNumber not empty
  b. Validate InvoiceDate not in the future:
     Error: "Датата на фактурата не може да бъде в бъдещето."
  c. Validate at least 1 item:
     Error: "Трябва да добавите поне един артикул."
  d. For each item: validate product exists and is active:
     Error: "Продукт с ID {id} не е намерен."
  e. For each item: validate quantity > 0, purchasePrice >= 0
  f. Create Invoice record (EntryDate = DateTime.UtcNow)
  g. Create InvoiceItem records
  h. For EACH item: load Product, increment StockQuantity += item.Quantity,
     set Product.UpdatedAt = DateTime.UtcNow
  i. SaveChangesAsync within transaction
  j. Return { Id, Message: "Доставката е записана. Наличностите са обновени.",
     SupplierName, InvoiceNumber }

No update or delete methods — invoices are IMMUTABLE.

6. Create Controllers/InvoicesController.cs:
- GET /api/invoices — [Authorize], paginated list
- GET /api/invoices/{id} — [Authorize], detail
- POST /api/invoices — [Authorize], create with auto stock update

Register InvoiceService in DI.
```

### Verify
```bash
dotnet ef database update --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Api
```

**Before testing, note down current stock for 2-3 products:**
- Product "Гранит сив" (ID 1): Stock = ___
- Product "Цимент 25кг" (ID 3): Stock = ___
- Product "Пясък фин" (ID 4): Stock = ___

Test via Swagger:

**Create invoice:**
```json
{
  "supplierName": "Гранит БГ ООД",
  "invoiceNumber": "Ф-2026-0145",
  "invoiceDate": "2026-02-18",
  "items": [
    { "productId": 1, "quantity": 50.00, "purchasePrice": 15.00 },
    { "productId": 3, "quantity": 200.00, "purchasePrice": 3.50 },
    { "productId": 4, "quantity": 500.00, "purchasePrice": 0.05 }
  ]
}
```
- [ ] Returns 201 with success message in Bulgarian
- [ ] Check product stocks now:
  - Гранит сив: previous + 50 = ___  ✓
  - Цимент 25кг: previous + 200 = ___  ✓
  - Пясък фин: previous + 500 = ___  ✓
- [ ] Invoice record exists in database

**List invoices:**
- [ ] GET /api/invoices → returns the invoice with correct TotalItems (3),
  TotalQuantity (750), InvoiceTotal (50×15 + 200×3.5 + 500×0.05 = 1475.00)

**Invoice detail:**
- [ ] GET /api/invoices/{id} → returns all items with ProductName, UnitDisplay,
  Quantity, PurchasePrice, RowTotal
- [ ] InvoiceTotal = sum of all RowTotals = 1475.00

**Validation:**
- [ ] POST with empty supplierName → 400
- [ ] POST with future invoiceDate → 400 "Датата на фактурата не може да бъде в бъдещето."
- [ ] POST with empty items → 400 "Трябва да добавите поне един артикул."
- [ ] POST with non-existent productId → 400
- [ ] POST with quantity 0 → 400
- [ ] All endpoints without auth → 401

**Create a second invoice** (to verify list shows multiple):
```json
{
  "supplierName": "Камък Трейд ООД",
  "invoiceNumber": "1234",
  "invoiceDate": "2026-02-19",
  "items": [
    { "productId": 1, "quantity": 25.00, "purchasePrice": 16.00 }
  ]
}
```
- [ ] Stock for Гранит сив incremented by another 25
- [ ] GET /api/invoices now shows 2 invoices, newest first

### Commit
```bash
git add .
git commit -m "Epic 08: Stories 8.1-8.2 — Invoice entities, migration, API with auto stock update"
```

---

## Session 5.2 — Invoice List + Detail Pages (Epic 08, Stories 8.3 + 8.5)

### Prompt 1
```
Read docs/conventions.md and planning/epics/08-invoice-management.md.

Implement Stories 8.3 and 8.5 — Invoice List Page and Invoice Detail Page.

1. Client Services:
- Create Services/IInvoiceService.cs and Services/InvoiceService.cs in
  the Blazor client:
  - GetAllAsync(page, pageSize) → PaginatedResponse<InvoiceListDto>
  - GetByIdAsync(id) → InvoiceDetailDto
  - CreateAsync(CreateInvoiceRequest) → response
- Create matching DTO classes in client Models/ folder

2. Invoice List Page at /admin/invoices:
- Page title: "Доставки"
- @attribute [Authorize]
- "Нова доставка" button (btn-primary) at top → navigates to /admin/invoices/new

- Table columns:
  - №: row number
  - Доставчик: SupplierName
  - Номер на фактура: InvoiceNumber
  - Дата на фактура: InvoiceDate formatted DD.MM.YYYY
  - Дата на въвеждане: EntryDate formatted DD.MM.YYYY
  - Брой артикули: TotalItems
  - Обща стойност: InvoiceTotal formatted "XX.XX €"
  - Действия: "Виж" button (btn-outline-primary btn-sm) → /admin/invoices/{id}

- Pagination (20 per page)
- Sorted by entry date, newest first (API already handles this)
- Loading spinner
- Empty state: "Няма записани доставки."

3. Invoice Detail Page at /admin/invoices/{id:int}:
- Page title: "Доставка №{InvoiceNumber}"
- @attribute [Authorize]
- Fetches invoice from GET /api/invoices/{id} on load

- Header info section (Bootstrap card or dl list):
  - Доставчик: {SupplierName}
  - Номер на фактура: {InvoiceNumber}
  - Дата на фактура: {InvoiceDate} formatted DD.MM.YYYY
  - Дата на въвеждане: {EntryDate} formatted DD.MM.YYYY

- Items table:
  - Columns: №, Продукт, Мерна ед., Количество, Покупна цена, Общо
  - Покупна цена and Общо formatted as "XX.XX €"
  - RowTotal = Quantity × PurchasePrice

- Invoice total below table:
  - **Обща стойност: {InvoiceTotal} €** (bold)

- Info note (Bootstrap alert-info):
  "Доставките не могат да бъдат редактирани. При грешка, коригирайте
  наличностите ръчно от страницата за продукти."

- "Обратно към доставките" link → /admin/invoices

- Entirely READ-ONLY — no edit buttons, no action buttons.

- Loading spinner on load
- Not found state: "Доставката не е намерена." with link back

All text in Bulgarian. Bootstrap 5 only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Login → navigate to /admin/invoices
- [ ] Table shows invoices created via Swagger in Session 5.1
- [ ] Newest invoice first
- [ ] Dates formatted DD.MM.YYYY
- [ ] InvoiceTotal correctly calculated and formatted as "XX.XX €"
- [ ] "Нова доставка" button visible (navigates to /admin/invoices/new — page may not exist yet)
- [ ] Click "Виж" on an invoice → /admin/invoices/{id}
- [ ] Detail page shows: Доставчик, Номер, dates
- [ ] Items table shows product names, units, quantities, prices, row totals
- [ ] Invoice total correct at bottom
- [ ] Info note about immutability visible
- [ ] "Обратно към доставките" link works
- [ ] No edit or delete buttons anywhere
- [ ] Navigate to /admin/invoices/99999 → "Доставката не е намерена."
- [ ] Loading spinners work

### Commit
```bash
git add .
git commit -m "Epic 08: Stories 8.3+8.5 — Invoice list and detail admin pages"
```

---

## Session 5.3 — New Invoice Form (Epic 08, Story 8.4)

### Prompt 1
```
Read docs/conventions.md and planning/epics/08-invoice-management.md.

Implement Story 8.4 — New Invoice Form Page.

Page at /admin/invoices/new:
- Page title: "Нова доставка"
- @attribute [Authorize]

Layout — Bootstrap card with form:

**Header Fields:**
- Доставчик (text input, required, min 2 chars, max 200)
- Номер на фактура (text input, required, min 1, max 50)
- Дата на фактура (date input, required, cannot be in the future)
  - Default value: today's date

**Items Section — Dynamic List:**
- Header: "Артикули"
- Each row contains:
  - Продукт: dropdown select populated from ProductService.GetAllAsync()
    - Display format: "{ProductName} ({CategoryName}) — {UnitDisplay}"
    - Example: "Гранит сив (Натурален камък) — м²"
    - Only show active products
  - Количество: decimal input (min 0.01, step 0.01)
  - Покупна цена (€): decimal input (min 0, step 0.01)
  - Общо: read-only calculated field = Quantity × PurchasePrice, formatted "XX.XX €"
  - Remove button (× icon, btn-outline-danger btn-sm)
    - Cannot remove if only 1 row remains (must have at least 1 item)

- "Добави артикул" button (btn-outline-secondary) below the item rows
  — adds a new empty row
- Start with 1 empty row by default

**Invoice Total:**
- Below items: "Обща стойност: {sum of all row totals} €" (bold, live-calculated)
- Updates instantly as quantities and prices change

**Buttons:**
- "Запази" button (btn-success):
  1. Client-side validation:
     - SupplierName, InvoiceNumber not empty
     - InvoiceDate set and not in the future
     - At least 1 item with product selected, quantity > 0
     - Show Bulgarian validation messages per field
  2. Show confirmation modal BEFORE saving:
     "Сигурни ли сте? След запазване наличностите ще бъдат автоматично
     обновени. Доставката не може да бъде редактирана."
  3. On confirm: call POST /api/invoices
  4. On success: navigate to /admin/invoices with alert-success
     "Доставката е записана. Наличностите са обновени."
  5. On API error: show alert-danger with error message
- "Отказ" button (btn-outline-secondary) → navigate to /admin/invoices
  without saving

**Loading state:**
- "Запази" button shows spinner and is disabled while saving
- Products dropdown shows "Зареждане..." while products load

**Implementation notes:**
- Use a List<InvoiceItemFormModel> in the page code to manage dynamic rows
- InvoiceItemFormModel: ProductId (int?), Quantity (decimal?),
  PurchasePrice (decimal?), with computed RowTotal
- Product dropdown: load all active products once on page init, reuse for
  all rows
- Row total and invoice total recalculate on every input change
- Prevent selecting the same product in multiple rows (optional nice-to-have,
  not required)

All text in Bulgarian. Bootstrap 5 only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

**Before testing, note stock for products you'll test:**
- Product "Гранит сив": Stock = ___
- Product "Мрамор бял": Stock = ___

Test in browser:

**Page load:**
- [ ] Navigate to /admin/invoices → click "Нова доставка"
- [ ] Form loads with empty fields, date defaulted to today
- [ ] 1 empty item row visible
- [ ] Product dropdown populated with all active products in correct format

**Dynamic item rows:**
- [ ] Click "Добави артикул" → second row appears
- [ ] Click "Добави артикул" → third row appears
- [ ] Click remove (×) on a row → row removed
- [ ] Cannot remove last remaining row (button disabled or hidden)

**Live calculations:**
- [ ] Select a product, enter quantity 10, price 15.00 → Общо shows "150.00 €"
- [ ] Change quantity to 20 → Общо updates to "300.00 €"
- [ ] Add second row with quantity 5, price 10 → Общо = "50.00 €"
- [ ] Invoice total at bottom: 300.00 + 50.00 = "350.00 €"

**Validation:**
- [ ] Try to save with empty supplier → validation error
- [ ] Try to save with no product selected → validation error
- [ ] Try to save with quantity 0 → validation error
- [ ] Set date to tomorrow → validation error

**Save flow:**
- [ ] Fill in: Доставчик = "Тест Доставчик", Номер = "Ф-001", Date = today
- [ ] Add 2 items: Гранит сив qty 30 price 14.00, Мрамор бял qty 15 price 28.00
- [ ] Click "Запази" → confirmation modal appears with warning text
- [ ] Click confirm → loading spinner on button
- [ ] Redirected to /admin/invoices → success alert shown
- [ ] New invoice appears in the list
- [ ] Check product stocks:
  - Гранит сив: previous + 30 = ___  ✓
  - Мрамор бял: previous + 15 = ___  ✓

**Cancel:**
- [ ] Start filling form → click "Отказ" → back to invoice list, nothing saved

### Commit
```bash
git add .
git commit -m "Epic 08: Story 8.4 — New invoice form with dynamic rows and auto stock update"
```

---

## Phase 5 Complete ✅

At this point you should have full invoice/delivery management:
- ✅ Invoice entity and items in database
- ✅ Create invoice API with automatic stock increment (transactional)
- ✅ Invoice list page (sorted by date, paginated)
- ✅ Invoice detail page (read-only, all items with totals)
- ✅ New invoice form with dynamic item rows and live calculations
- ✅ Confirmation modal before saving
- ✅ Invoices immutable after creation

**Stock management is now fully automated:**
- Stock INCREASES when admin enters an invoice (delivery arrives)
- Stock DECREASES when admin confirms an order

Update planning/overview.md:
```markdown
| 08 | Invoice & Delivery Management | ✅ Completed | Epic 04         |
```

```bash
git add planning/overview.md
git commit -m "Update planning status: Phase 5 complete"
```

**Run a full stock cycle test:**
1. Note stock of "Гранит сив"
2. Create an invoice adding 100 м² → stock increases by 100
3. Place a customer order for 20 м² of Гранит сив
4. Confirm the order → stock decreases by 20
5. Final stock should be: original + 100 - 20
6. Verify the number matches

If this works, your inventory system is solid.

**Next**: Phase 6 — Landing Page, Contacts, Dashboard, and Receipt Printing. Start a fresh Claude Code session.

---

## Troubleshooting

### If stock doesn't increase after invoice creation:
```
After creating an invoice, the product stock quantities are not increasing.
Make sure the CreateAsync method in InvoiceService:
1. Loads each Product entity by ProductId
2. Adds the item quantity: product.StockQuantity += item.Quantity
3. Sets product.UpdatedAt = DateTime.UtcNow
4. All of this is inside a database transaction
5. SaveChangesAsync is called
Show me the current CreateAsync implementation in InvoiceService.
```

### If dynamic rows don't add/remove properly:
```
The dynamic item rows in the invoice form are not working correctly.
Make sure you're using a List<InvoiceItemFormModel> and that:
- "Добави артикул" adds a new item to the list
- Remove button removes the item at the correct index
- After add/remove, call StateHasChanged() if needed
- Each row has a unique @key to prevent Blazor re-rendering issues
Use @key="item" or @key="index" on the foreach loop.
Show me the current implementation.
```

### If product dropdown is empty:
```
The product dropdown in the invoice form is empty. Make sure:
1. Products are loaded in OnInitializedAsync by calling
   ProductService.GetAllAsync (no category filter, all active products)
2. The dropdown is bound correctly to each row's ProductId
3. The API GET /api/products returns active products
Show me how you're loading and displaying products in the dropdown.
```

### If row totals don't recalculate:
```
The row totals (Quantity × PurchasePrice) are not recalculating when
inputs change. Make sure:
- RowTotal is a computed property (get => Quantity * PurchasePrice)
  or recalculated on every input change event
- The invoice total is also a computed property summing all row totals
- Use @oninput or @bind with event handlers to trigger recalculation
- Call StateHasChanged() after recalculation if using manual events
```

### If invoice date allows future dates:
```
The invoice date input is accepting dates in the future. Add client-side
validation: compare the selected date with DateTime.Today and show
error "Датата на фактурата не може да бъде в бъдещето." if it's after
today. Also set max="{today's date}" on the HTML input element:
<input type="date" max="@DateTime.Today.ToString("yyyy-MM-dd")" />
The API also validates this server-side as a backup.
```

### If confirmation modal doesn't appear before save:
```
The confirmation modal must appear BEFORE the API call is made. The flow
should be:
1. User clicks "Запази"
2. Client-side validation runs
3. If valid: show Bootstrap modal with warning text
4. User clicks "Потвърди" in modal → THEN make the API call
5. User clicks "Отказ" in modal → close modal, do nothing
Make sure the "Запази" button triggers the modal, not the API call directly.
```
