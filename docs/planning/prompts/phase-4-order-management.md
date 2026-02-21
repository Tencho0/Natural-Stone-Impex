# Phase 4: Order Management (Admin) — Exact Claude Code Prompts

## Prerequisites

- Phase 3 completed and committed
- Orders exist in the database (placed via the customer checkout flow)
- Place at least 3-4 test orders before starting this phase:
  - 1 individual customer + pickup
  - 1 individual customer + delivery
  - 1 company customer + delivery
  - 1 with multiple items
- Fresh Claude Code session

---

## Session 4.1 — Order Management API Endpoints (Epic 07, Story 7.1)

### Prompt 1 (Plan)
```
Read docs/conventions.md, docs/api-endpoints.md (Orders section — ALL
endpoints: GET list, GET detail, PUT confirm, PUT complete, PUT cancel,
PUT delivery-fee, GET stats, GET recent), docs/database-schema.md
(Order section), and planning/epics/07-order-management.md.

I want to implement Story 7.1 — all order management API endpoints
for the admin.

Before writing any code, tell me:
- What new DTOs will you create?
- How will the confirm endpoint decrement stock (step by step)?
- What happens if stock is insufficient for one or more items?
- What validation will each endpoint perform?
- What database transaction strategy will you use for confirm?
- What are all the Bulgarian error messages you'll return?

Don't write any code yet.
```

> **Wait for response. Carefully verify:**
> - GET /api/orders returns customerName (FullName for individual, CompanyName for company)
> - GET /api/orders/{id} returns computed totals: subtotalWithoutVat, totalVat, subtotalWithVat, grandTotal
> - grandTotal = subtotalWithVat + deliveryFee (if set)
> - Confirm: checks ALL items' stock BEFORE decrementing any. If any insufficient, return all shortages in one error.
> - Confirm: uses a database transaction
> - Cancel: only for Pending orders, does NOT affect stock
> - Complete: only for Confirmed orders
> - Delivery fee: only for orders with DeliveryMethod = Delivery
> - All display fields included: statusDisplay, customerTypeDisplay, deliveryMethodDisplay, unitDisplay
>
> **Correct if needed, then:**

### Prompt 2 (Execute)
```
Proceed with the implementation. Add these to the existing OrderService
and OrdersController:

DTOs to create in Models/DTOs/:
- OrderListDto: Id, OrderNumber, CreatedAt, CustomerName (string — FullName
  or CompanyName depending on type), CustomerType (int), CustomerTypeDisplay
  (string — "Физическо лице" or "Фирма"), DeliveryMethod (int),
  DeliveryMethodDisplay ("Вземане от обекта" or "Доставка"), Status (int),
  StatusDisplay ("Чакаща" / "Потвърдена" / "Завършена"), IsCancelled,
  TotalWithVat (decimal), ItemCount (int)
- OrderDetailDto: all fields from OrderListDto PLUS: DeliveryFee (nullable),
  ConfirmedAt, CompletedAt, CustomerInfo (object with all customer fields),
  Items (list of OrderItemDto), SubtotalWithoutVat, TotalVat, SubtotalWithVat,
  GrandTotal
- OrderItemDto: Id, ProductId, ProductName, Quantity, UnitPriceWithoutVat,
  VatAmount, UnitPriceWithVat, Unit (int), UnitDisplay, RowTotalWithoutVat,
  RowVatTotal, RowTotalWithVat
- CustomerInfoDto: FullName, Phone, Address, CompanyName, Eik, Mol,
  ContactPerson, ContactPhone
- SetDeliveryFeeRequest: DeliveryFee (decimal, required, >= 0)
- OrderStatsDto: TotalProducts, PendingOrders, ConfirmedOrders, CompletedOrders
- OrderConfirmErrorDto: Error (string), Details (list of { ProductName,
  Ordered (decimal), Available (decimal), Unit (string) })

Service methods to add to IOrderService/OrderService:
- GetAllAsync(status?, page, pageSize) → PaginatedResponse<OrderListDto>
  - Sorted by CreatedAt descending
  - Filter by status if provided
  - Compute CustomerName, TotalWithVat, ItemCount in the query or mapping
- GetByIdAsync(id) → OrderDetailDto
  - Include CustomerInfo and Items with navigation properties
  - Compute all row totals and order totals
  - GrandTotal = SubtotalWithVat + (DeliveryFee ?? 0)
- ConfirmAsync(id) → within a DB TRANSACTION:
  1. Load order with items, verify Status == Pending and !IsCancelled
  2. Load all referenced products
  3. Check stock for ALL items first. Collect all shortages.
  4. If ANY shortage: return 400 with error "Недостатъчна наличност за
     следните продукти:" and details array listing each shortage
     { ProductName, Ordered, Available, Unit }
  5. If all stock OK: decrement each product's StockQuantity
  6. Set Status = Confirmed, ConfirmedAt = DateTime.UtcNow
  7. Save within transaction
- CompleteAsync(id):
  - Verify Status == Confirmed and !IsCancelled
  - Set Status = Completed, CompletedAt = DateTime.UtcNow
  - Error if wrong status: "Само потвърдени поръчки могат да бъдат завършени."
- CancelAsync(id):
  - Verify Status == Pending and !IsCancelled
  - Set IsCancelled = true
  - Do NOT affect stock
  - Error if not pending: "Само чакащи поръчки могат да бъдат отказани."
- SetDeliveryFeeAsync(id, deliveryFee):
  - Verify order exists and DeliveryMethod == Delivery
  - Verify !IsCancelled
  - Set DeliveryFee
  - Error: "Цена за доставка може да се задава само за поръчки с доставка."
- GetStatsAsync() → OrderStatsDto
  - Count total active products, pending orders, confirmed orders, completed orders
- GetRecentAsync(count) → List<OrderListDto>
  - Top N orders sorted by CreatedAt desc

Controller endpoints (ALL [Authorize]):
- GET /api/orders?status={0,1,2}&page=1&pageSize=20
- GET /api/orders/{id}
- PUT /api/orders/{id}/confirm
- PUT /api/orders/{id}/complete
- PUT /api/orders/{id}/cancel
- PUT /api/orders/{id}/delivery-fee
- GET /api/orders/stats
- GET /api/orders/recent?count=5
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
```

Test via Swagger (make sure you have test orders from Phase 3):

**List and filter:**
- [ ] GET /api/orders → returns all orders, newest first, with correct CustomerName and TotalWithVat
- [ ] GET /api/orders?status=0 → only pending orders
- [ ] GET /api/orders without auth token → 401

**Detail:**
- [ ] GET /api/orders/{id} → full detail with computed totals
- [ ] Verify SubtotalWithVat = sum of all RowTotalWithVat
- [ ] Verify TotalVat = sum of all RowVatTotal
- [ ] Verify GrandTotal = SubtotalWithVat + DeliveryFee (or SubtotalWithVat if no fee)
- [ ] CustomerInfo correctly populated based on customer type
- [ ] Items have UnitDisplay ("кг" / "м²")
- [ ] StatusDisplay shows Bulgarian text

**Delivery fee:**
- [ ] PUT /api/orders/{id}/delivery-fee with `{"deliveryFee": 25.00}` on a delivery order → 200
- [ ] GET the order again → deliveryFee = 25.00, grandTotal updated
- [ ] Try on a pickup order → 400 with Bulgarian error
- [ ] Try on a cancelled order → 400

**Confirm (the critical test):**
- [ ] Note the stock quantities of ordered products BEFORE confirming
- [ ] PUT /api/orders/{id}/confirm on a pending order → 200
- [ ] Check product stock quantities → decremented by ordered amounts
- [ ] GET the order → Status = 1, StatusDisplay = "Потвърдена", ConfirmedAt set
- [ ] Try confirming the same order again → 400 "Само чакащи поръчки..."

**Confirm with insufficient stock:**
- [ ] Create an order with a quantity larger than available stock (e.g., order 99999 кг of something)
- [ ] Try to confirm → 400 with "Недостатъчна наличност..." and details listing which products are short
- [ ] Verify NO stock was decremented (transaction rolled back)

**Complete:**
- [ ] PUT /api/orders/{id}/complete on a confirmed order → 200
- [ ] GET the order → Status = 2, StatusDisplay = "Завършена", CompletedAt set
- [ ] Try completing a pending order → 400

**Cancel:**
- [ ] PUT /api/orders/{id}/cancel on a pending order → 200, IsCancelled = true
- [ ] Verify stock NOT affected
- [ ] Try cancelling a confirmed order → 400

**Stats and recent:**
- [ ] GET /api/orders/stats → correct counts
- [ ] GET /api/orders/recent?count=3 → last 3 orders

### Commit
```bash
git add .
git commit -m "Epic 07: Story 7.1 — All order management API endpoints"
```

---

## Session 4.2 — Order List Admin Page (Epic 07, Story 7.2)

### Prompt 1
```
Read docs/conventions.md and planning/epics/07-order-management.md.

Implement Story 7.2 — Order List Page in the admin panel.

1. Client Services:
- Create Services/IOrderService.cs and Services/OrderService.cs in the
  Blazor client project (or extend if they already exist from checkout):
  - GetAllAsync(status?, page, pageSize) → PaginatedResponse<OrderListDto>
  - GetByIdAsync(id) → OrderDetailDto
  - ConfirmAsync(id) → response
  - CompleteAsync(id) → response
  - CancelAsync(id) → response
  - SetDeliveryFeeAsync(id, deliveryFee) → response
  - GetStatsAsync() → OrderStatsDto
  - GetRecentAsync(count) → list
- Create matching DTO classes in client Models/ folder

2. Order List Page at /admin/orders:
- Page title: "Поръчки"
- @attribute [Authorize]

- Status summary bar at top (Bootstrap row with 4 small cards or badges):
  - "Чакащи: {count}" — badge bg-warning
  - "Потвърдени: {count}" — badge bg-info
  - "Завършени: {count}" — badge bg-success
  - "Общо: {total}" — badge bg-secondary
  - Fetch counts from GET /api/orders/stats

- Status filter tabs (Bootstrap nav-tabs or btn-group):
  - Всички / Чакащи / Потвърдени / Завършени
  - Clicking a tab filters the order list
  - Active tab highlighted

- Orders table:
  - Columns: №, Номер, Дата, Клиент, Тип, Метод, Статус, Обща сума, Действия
  - №: row number (sequential on page)
  - Номер: OrderNumber (e.g., NSI-20260219-0001)
  - Дата: CreatedAt formatted as DD.MM.YYYY
  - Клиент: CustomerName
  - Тип: CustomerTypeDisplay — small badge (bg-light text-dark)
  - Метод: DeliveryMethodDisplay
  - Статус: StatusDisplay as color-coded badge:
    - Чакаща: bg-warning text-dark
    - Потвърдена: bg-info text-white
    - Завършена: bg-success text-white
    - If IsCancelled: bg-danger text-white "Отказана" (overrides status)
  - Обща сума: TotalWithVat formatted as "XX.XX €"
  - Действия: "Виж" button (btn-outline-primary btn-sm) → navigates to
    /admin/orders/{id}

- Pagination (20 per page, reuse Pagination component)
- Loading spinner while data loads
- Empty state per filter: "Няма поръчки с този статус."

All text in Bulgarian. Bootstrap 5 only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Login → /admin/orders shows order list
- [ ] Status summary shows correct counts (matches database)
- [ ] Table shows all orders with correct data
- [ ] Dates formatted as DD.MM.YYYY
- [ ] Status badges correct colors (yellow for pending, blue for confirmed, etc.)
- [ ] Cancelled orders show red "Отказана" badge
- [ ] Prices formatted as "XX.XX €"
- [ ] Click "Всички" tab → all orders shown
- [ ] Click "Чакащи" tab → only pending orders
- [ ] Click "Потвърдени" tab → only confirmed orders
- [ ] Click "Завършени" tab → only completed orders
- [ ] Click "Виж" → navigates to /admin/orders/{id}
- [ ] Pagination works (if enough orders)
- [ ] Loading spinner appears on load

### Commit
```bash
git add .
git commit -m "Epic 07: Story 7.2 — Order list admin page with status filter"
```

---

## Session 4.3 — Order Detail Admin Page (Epic 07, Story 7.3)

### Prompt 1
```
Read docs/conventions.md, docs/api-endpoints.md (GET /api/orders/{id}
response shape), and planning/epics/07-order-management.md.

Implement Story 7.3 — Order Detail Page in the admin panel.

Page at /admin/orders/{id:int}:
- @attribute [Authorize]
- Page title: "Поръчка {OrderNumber}"
- Fetches order detail from GET /api/orders/{id} on load

Layout — Bootstrap card sections:

**Section 1 — Order Header:**
- Row with: OrderNumber (h4), Status badge (same colors as list),
  CreatedAt (DD.MM.YYYY HH:mm)
- If cancelled: show red "ОТКАЗАНА" alert at top

**Section 2 — "Данни за клиента" (Customer Info) — Bootstrap card:**
- Customer type badge: "Физическо лице" or "Фирма"
- If Individual:
  - Име: {FullName}
  - Телефон: {Phone}
  - Адрес: {Address} (only if delivery)
- If Company:
  - Фирма: {CompanyName}
  - ЕИК: {Eik}
  - МОЛ: {Mol}
  - Лице за контакт: {ContactPerson}
  - Телефон: {ContactPhone}
  - Адрес: {Address} (only if delivery)
- Delivery method: "Метод: Вземане от обекта" or "Метод: Доставка"

**Section 3 — "Артикули" (Order Items) — Bootstrap table:**
- Columns: №, Продукт, Мерна ед., Количество, Ед. цена без ДДС, ДДС,
  Ед. цена с ДДС, Общо с ДДС
- All prices formatted as "XX.XX €"
- Row for each item

**Section 4 — "Обобщение" (Totals) — right-aligned Bootstrap card:**
- Сума без ДДС: {SubtotalWithoutVat} €
- Общо ДДС: {TotalVat} €
- Сума с ДДС: {SubtotalWithVat} €
- Цена за доставка: {DeliveryFee} € — OR "Не е определена" if null,
  OR "Не се прилага" if pickup order
- Separator line
- **Обща сума: {GrandTotal} €** (bold, larger, text-primary)

**Section 5 — "Действия" (Actions) — conditional based on status:**

If Status == Pending (Чакаща) AND NOT cancelled:
  - Delivery fee input section (only if DeliveryMethod == Delivery):
    - Label: "Цена за доставка (€)"
    - Decimal input (min 0, step 0.01) + "Задай" button (btn-outline-primary btn-sm)
    - On click: call PUT /api/orders/{id}/delivery-fee
    - On success: refresh order data, show alert-success "Цената за доставка е зададена."
  - "Потвърди поръчката" button (btn-success btn-lg)
    - On click: show confirmation modal:
      "Сигурни ли сте, че искате да потвърдите тази поръчка?
      Наличностите ще бъдат намалени."
    - On confirm: call PUT /api/orders/{id}/confirm
    - On success: refresh order data, show alert-success "Поръчката е потвърдена."
    - On error (insufficient stock): show alert-danger with the full error
      message AND the details table showing which products are short:
      Продукт | Поръчано | Налично | Мерна ед.
      Display this as a Bootstrap table inside the alert.
  - "Откажи поръчката" button (btn-outline-danger)
    - On click: confirmation modal: "Сигурни ли сте, че искате да откажете
      тази поръчка?"
    - On confirm: call PUT /api/orders/{id}/cancel
    - On success: refresh, show alert-warning "Поръчката е отказана."

If Status == Confirmed (Потвърдена) AND NOT cancelled:
  - Delivery fee input (same as above, if not yet set and DeliveryMethod == Delivery)
  - "Маркирай като завършена" button (btn-success)
    - On click: confirmation modal: "Маркирай поръчката като завършена?"
    - On confirm: call PUT /api/orders/{id}/complete
    - On success: refresh, show alert-success "Поръчката е завършена."
  - "Принтирай разписка" button (btn-outline-primary)
    - Opens /admin/orders/{id}/receipt in a new tab (target="_blank")
    - We'll build this page in Phase 6, for now just navigate (404 is OK)

If Status == Completed (Завършена):
  - No action buttons except "Принтирай разписка"
  - Show ConfirmedAt and CompletedAt dates

If Cancelled:
  - No action buttons. Show "Тази поръчка е отказана." alert-danger.

**Navigation:**
- "Обратно към поръчките" link at top → /admin/orders

**States:**
- Loading spinner on page load
- Loading overlay on action buttons while API call is in progress
- Alert messages auto-dismiss after 5 seconds (except errors — stay until dismissed)
- If order not found: "Поръчката не е намерена." with link back to /admin/orders

All text in Bulgarian. Bootstrap 5 only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test the FULL admin order flow in browser:

**View order detail:**
- [ ] Navigate to /admin/orders → click "Виж" on a pending order
- [ ] Customer info section shows correct data
- [ ] Items table shows correct products, quantities, prices
- [ ] All prices formatted as "XX.XX €"
- [ ] Totals section shows correct SubtotalWithoutVat, TotalVat, SubtotalWithVat
- [ ] GrandTotal correct (matches sum)

**Set delivery fee (on a delivery order):**
- [ ] Delivery fee input visible for delivery orders
- [ ] Enter 25.00 → click "Задай"
- [ ] Success message appears
- [ ] Totals section updates: DeliveryFee = 25.00 €, GrandTotal increased by 25
- [ ] Delivery fee input NOT visible for pickup orders

**Confirm order:**
- [ ] Note stock quantities of ordered products (check via /admin/products)
- [ ] Click "Потвърди поръчката" → confirmation modal appears
- [ ] Confirm → success message, status changes to "Потвърдена"
- [ ] Check product stock → decremented by ordered amounts
- [ ] Action buttons change: now shows "Маркирай като завършена" and "Принтирай разписка"
- [ ] "Потвърди" button gone

**Confirm with insufficient stock:**
- [ ] Place a new order (via customer storefront) with very large quantity
- [ ] Try to confirm → error alert shows with table:
  Product name | Ordered amount | Available amount | Unit
- [ ] Verify NO stock was decremented

**Complete order:**
- [ ] On a confirmed order, click "Маркирай като завършена" → confirm modal
- [ ] Confirm → success, status changes to "Завършена"
- [ ] ConfirmedAt and CompletedAt dates shown
- [ ] Only "Принтирай разписка" button remains

**Cancel order:**
- [ ] On a pending order, click "Откажи поръчката" → confirm modal
- [ ] Confirm → warning message, order marked as cancelled
- [ ] Red "ОТКАЗАНА" alert shown, all action buttons gone
- [ ] Verify stock NOT affected

**Navigation:**
- [ ] "Обратно към поръчките" link works
- [ ] Order list shows updated statuses

### Commit
```bash
git add .
git commit -m "Epic 07: Story 7.3 — Order detail admin page with all actions"
```

---

## Phase 4 Complete ✅

At this point you should have full order management:
- ✅ Order list with status filter and summary counts
- ✅ Order detail with complete customer info and item breakdown
- ✅ Set delivery fee for delivery orders
- ✅ Confirm order (decrements stock, with insufficient stock error handling)
- ✅ Complete order
- ✅ Cancel pending order
- ✅ All Bulgarian text, proper formatting

**The core business logic is now complete.** An order can flow from customer placement through admin confirmation to completion, with stock automatically tracked.

Update planning/overview.md:
```markdown
| 07 | Order Management (Admin)    | ✅ Completed  | Epic 06           |
```

```bash
git add planning/overview.md
git commit -m "Update planning status: Phase 4 complete"
```

**Run a full integration test now** before moving on:
1. Place an order as a customer (catalog → cart → checkout)
2. Login as admin
3. View the order in the admin panel
4. Set delivery fee (if delivery)
5. Confirm the order
6. Verify stock decreased
7. Complete the order
8. Place another order, cancel it, verify stock unchanged

If this flow works end-to-end, your core system is solid. Everything after this (invoices, dashboard, receipt) builds on this foundation.

**Next**: Phase 5 — Invoice Management. Start a fresh Claude Code session.

---

## Troubleshooting

### If confirm endpoint succeeds but stock doesn't change:
```
The order confirmation is returning 200 but product stock quantities
are not being decremented. Make sure the ConfirmAsync method:
1. Loads the actual Product entities from the database (not just OrderItems)
2. Modifies Product.StockQuantity -= OrderItem.Quantity for each item
3. Calls SaveChangesAsync() WITHIN the transaction
4. The transaction is committed (not rolled back)
Show me the current ConfirmAsync implementation.
```

### If confirm shows success even with insufficient stock:
```
The confirm endpoint is not checking stock before decrementing. The
implementation must:
1. First, check ALL items against available stock (collect all shortages)
2. If ANY shortage exists, return 400 with ALL shortages listed — do NOT
   decrement anything
3. Only if ALL items have sufficient stock, proceed with decrementing
Show me the stock validation logic in ConfirmAsync.
```

### If order totals are wrong:
```
The order detail totals (SubtotalWithoutVat, TotalVat, GrandTotal) are
incorrect. These should be computed from OrderItems:
- RowTotalWithVat = Quantity * UnitPriceWithVat
- RowTotalWithoutVat = Quantity * UnitPriceWithoutVat
- RowVatTotal = Quantity * VatAmount
- SubtotalWithVat = sum of all RowTotalWithVat
- TotalVat = sum of all RowVatTotal
- GrandTotal = SubtotalWithVat + (DeliveryFee ?? 0)
Make sure these are computed in the service when mapping to OrderDetailDto,
not stored in the database.
```

### If status badges show wrong colors:
```
The status badges in the order list and detail pages are showing wrong
colors. The mapping should be:
- Чакаща (Pending = 0): bg-warning text-dark (yellow)
- Потвърдена (Confirmed = 1): bg-info text-white (blue)
- Завършена (Completed = 2): bg-success text-white (green)
- Отказана (IsCancelled = true): bg-danger text-white (red) — overrides status
Check the conditional rendering in both Orders.razor and OrderDetail.razor.
```

### If delivery fee input appears on pickup orders:
```
The delivery fee input section should only appear when the order's
DeliveryMethod is Delivery (1). Check the conditional rendering:
@if (order.DeliveryMethod == 1) { /* show delivery fee input */ }
Do not show the input for pickup orders.
```

### If action buttons don't disappear after an action:
```
After confirming/completing/cancelling an order, the page should refresh
the order data from the API and the action buttons should update based
on the new status. Make sure after each successful action you call
the method that fetches the order detail again (e.g., await LoadOrder())
and that the conditional rendering checks the updated status.
```

### If insufficient stock error doesn't show product details:
```
When confirming an order with insufficient stock, the error should show
a table listing each product that is short. The API returns:
{ "error": "...", "details": [{ "productName": "...", "ordered": 100,
"available": 50, "unit": "кг" }] }
The Blazor page needs to deserialize this specific error format (not just
the error string) and render the details as a Bootstrap table inside an
alert-danger. Show me how you're handling the error response.
```
