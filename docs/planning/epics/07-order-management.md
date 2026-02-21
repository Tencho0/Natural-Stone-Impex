# Epic 07: Order Management (Admin)

## Description
Admin can view all customer orders, inspect order details, add delivery fees, confirm orders (which decrements stock), mark orders as completed, and cancel pending orders.

## Dependencies
- Epic 06 (Cart & Checkout) must be completed — orders must exist in the database.

## Stories

---

### Story 7.1: Order List API Endpoints

**As** the admin, **I want** API endpoints to list and manage orders **so that** I can process customer orders.

**Acceptance Criteria:**
- [ ] `GET /api/orders` — list all orders (admin only)
  - Query params: `status` (optional filter: Pending/Confirmed/Completed), `page`, `pageSize` (default 20)
  - Returns paginated list with: OrderNumber, CreatedAt, CustomerName (or CompanyName), Status, DeliveryMethod, Total (with ДДС), IsCancelled
  - Sorted by CreatedAt descending (newest first)
- [ ] `GET /api/orders/{id}` — get full order details (admin only)
  - Returns: all order fields, customer info, all order items with prices, delivery fee, totals
- [ ] `PUT /api/orders/{id}/confirm` — confirm order (admin only)
  - Changes status from Pending to Confirmed
  - **Decrements stock** for each order item
  - If any product has insufficient stock: return HTTP 400 with error listing which products are short (e.g., "Недостатъчна наличност за: Цимент (поръчани: 100, налични: 50)")
  - Cannot confirm if already confirmed/completed or cancelled
- [ ] `PUT /api/orders/{id}/complete` — mark as completed (admin only)
  - Changes status from Confirmed to Completed
  - Cannot complete if not confirmed
- [ ] `PUT /api/orders/{id}/cancel` — cancel order (admin only)
  - Sets `IsCancelled = true`
  - Only allowed for Pending orders (not confirmed — stock was already decremented)
  - Does NOT affect stock
- [ ] `PUT /api/orders/{id}/delivery-fee` — set delivery fee (admin only)
  - Request: `{ "deliveryFee": 25.00 }`
  - Only allowed for orders with DeliveryMethod = Delivery
  - Can be set at any status (before or after confirmation)

**Tasks:**
- Create DTOs: `OrderListDto`, `OrderDetailDto`, `SetDeliveryFeeRequest`
- Extend `Services/OrderService.cs` with list, detail, confirm, complete, cancel, setDeliveryFee methods
- Extend `Controllers/OrdersController.cs` with all endpoints
- Implement stock decrement logic in confirm (within a database transaction)
- Add proper validation and error messages in Bulgarian

---

### Story 7.2: Order List Page (Admin)

**As** the admin, **I want** to see all orders in a table **so that** I can track and process them.

**Acceptance Criteria:**
- [ ] Page at `/admin/orders` with title "Поръчки"
- [ ] Table columns: №, Номер, Дата, Клиент, Тип (Физ. лице / Фирма), Метод, Статус, Обща сума, Действия
- [ ] Status filter tabs or dropdown: Всички / Чакащи / Потвърдени / Завършени
- [ ] Status displayed with color-coded badges:
  - Чакаща: yellow/warning
  - Потвърдена: blue/info
  - Завършена: green/success
  - Отказана (cancelled): red/danger — shown as crossed-out or separate badge
- [ ] Pagination (20 per page)
- [ ] "Виж" (View) button per row → navigates to order detail
- [ ] Newest orders shown first
- [ ] Loading state while data loads
- [ ] Show total count per status (e.g., "Чакащи: 5 | Потвърдени: 3 | Завършени: 12")

**Tasks:**
- Create `Services/IOrderService.cs` and `Services/OrderService.cs` in Blazor client
- Create `Pages/Admin/Orders.razor`
- Implement status filter
- Implement table with Bootstrap styling and badges
- Add pagination
- Wire up to API

---

### Story 7.3: Order Detail Page (Admin)

**As** the admin, **I want** to see full order details and take actions **so that** I can process each order.

**Acceptance Criteria:**
- [ ] Page at `/admin/orders/{id}` with title "Поръчка {OrderNumber}"
- [ ] **Customer info section:**
  - Customer type badge (Физическо лице / Фирма)
  - All customer fields displayed (name, phone, address, company fields if applicable)
  - Delivery method: Вземане от обекта / Доставка
- [ ] **Order items table:**
  - Columns: №, Продукт, Мерна ед., Количество, Ед. цена без ДДС, ДДС, Ед. цена с ДДС, Общо
  - Row totals calculated
- [ ] **Totals section:**
  - Сума без ДДС (subtotal without VAT)
  - Общо ДДС (total VAT)
  - Цена за доставка (delivery fee — if applicable, or "Не е определена" if not set yet)
  - **Обща сума с ДДС** (grand total, bold)
- [ ] **Action buttons** (shown based on current status):

  **If Чакаща:**
  - Delivery fee input (shown only if delivery method = Доставка): decimal input + "Задай цена за доставка" button
  - "Потвърди поръчката" button (green) — shows confirmation dialog
  - "Откажи поръчката" button (red) — shows confirmation dialog
  - On confirm: if stock insufficient, show detailed error message

  **If Потвърдена:**
  - "Маркирай като завършена" button (green)
  - "Принтирай разписка" button (blue) → opens receipt (Epic 10)
  - Delivery fee still editable if not yet set

  **If Завършена:**
  - Read-only, no action buttons except "Принтирай разписка"

  **If Cancelled:**
  - Read-only, status shown as "Отказана"

- [ ] "Обратно към поръчките" link
- [ ] Success/error alerts for all actions

**Tasks:**
- Create `Pages/Admin/OrderDetail.razor`
- Implement customer info display section
- Implement order items table with totals
- Implement delivery fee input and set action
- Implement confirm action with stock validation error handling
- Implement complete action
- Implement cancel action with confirmation dialog
- Add status-based conditional rendering of action buttons
- Wire up all API calls with loading states and error handling
