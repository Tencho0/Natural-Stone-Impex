# Phase 3: Customer Storefront — Exact Claude Code Prompts

## Prerequisites

- Phase 2 completed and committed
- Categories and products in database (seed data + any manually added)
- Product API endpoints working (verified via Swagger)
- Fresh Claude Code session

---

## Session 3.1 — Catalog Page (Epic 05, Story 5.1)

### Prompt 1
```
Read docs/conventions.md, docs/api-endpoints.md (Products and Categories
sections), and planning/epics/05-public-catalog.md.

Implement Story 5.1 — Public Catalog Page.

This page is PUBLIC — no @attribute [Authorize]. Uses MainLayout.

1. Blazor Client Services (if not already created in Phase 2):
- Make sure Services/IProductService.cs and Services/ProductService.cs
  exist in the client with a method:
  GetAllAsync(categoryId?, search, page, pageSize) → PaginatedResponse<ProductListDto>
- Make sure Services/ICategoryService.cs has GetAllAsync() for the filter

2. Product Card Component:
- Create Components/ProductCard.razor
- Props: product (ProductListDto)
- Layout (Bootstrap card):
  - Product image (use img tag, show placeholder image if ImagePath is null.
    For placeholder use a Bootstrap bg-light div with text "Няма снимка")
  - Product name (clickable, links to /products/{id})
  - Price with ДДС: bold, e.g., "30.00 €"
  - ДДС info below price: "вкл. 5.00 € ДДС" (smaller, muted text)
  - Unit: "м²" or "кг" (badge)
  - If StockQuantity > 0: "Добави в количката" button (btn-primary)
  - If StockQuantity == 0: "Изчерпан" badge (bg-danger) instead of button
- The "Добави в количката" button doesn't do anything yet — just a placeholder.
  We'll connect it in Session 3.3.

3. Catalog Page at /catalog:
- Page title: "Каталог"
- Uses MainLayout (public)
- Desktop layout: category sidebar (left, col-md-3) + product grid (right, col-md-9)
- Mobile layout: category dropdown above product grid

- Category sidebar/dropdown:
  - "Всички категории" option (default, no filter)
  - List all categories from API
  - Currently selected category highlighted (active class)
  - Clicking a category filters products

- Search bar above product grid:
  - Placeholder: "Търсене на продукт..."
  - 300ms debounce — triggers API call after user stops typing
  - Resets to page 1 when search changes

- Product grid:
  - Responsive: 3 columns desktop (col-md-4), 2 tablet (col-sm-6), 1 mobile
  - Uses ProductCard component for each product
  - 12 products per page

- Pagination at bottom (reuse Components/Pagination.razor from Phase 2)

- Empty state: "Няма намерени продукти." centered message

- Loading spinner while products load (initial and on filter/search change)

All text in Bulgarian. Bootstrap 5 only. No custom CSS.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser (NO login needed):
- [ ] Navigate to /catalog — products displayed as cards in a grid
- [ ] Product images shown (or placeholder for products without images)
- [ ] Prices formatted as "30.00 €" with "вкл. 5.00 € ДДС" below
- [ ] Units shown as "кг" or "м²"
- [ ] Category sidebar visible on desktop, dropdown on mobile
- [ ] Click "Натурален камък" → only stone products shown
- [ ] Click "Всички категории" → all products shown
- [ ] Type "гранит" in search → filtered results after debounce
- [ ] Clear search → all products return
- [ ] Pagination works (may need to add more products to test — currently only 5 seed)
- [ ] "Добави в количката" button visible for in-stock products
- [ ] "Изчерпан" badge shown for out-of-stock products (set one to 0 via admin to test)
- [ ] Clicking product name navigates to /products/{id} (page may be empty — that's next)
- [ ] Loading spinner appears while data loads
- [ ] Mobile responsive: check at 375px width

### Commit
```bash
git add .
git commit -m "Epic 05: Story 5.1 — Public catalog page with category filter and search"
```

---

## Session 3.2 — Product Detail Page (Epic 05, Story 5.2)

### Prompt 1
```
Read docs/conventions.md, docs/api-endpoints.md (GET /api/products/{id}),
and planning/epics/05-public-catalog.md.

Implement Story 5.2 — Public Product Detail Page.

Page at /products/{id:int}:
- Public, no auth, uses MainLayout
- Calls GET /api/products/{id} on load

Layout (Bootstrap row):
- Left column (col-md-6): Product image (large). If no image, show
  placeholder with "Няма снимка" text.
- Right column (col-md-6): All product info

Breadcrumb at top:
- Каталог > {CategoryName} > {ProductName}
- "Каталог" links to /catalog
- Category name links to /catalog?categoryId={categoryId}

Product info section:
- Product name (h2)
- Category badge (Bootstrap badge bg-secondary)
- Description paragraph (if available, show below name)
- Price section (visually prominent):
  - Цена с ДДС: "30.00 €" — large, bold (h3 or similar)
  - Цена без ДДС: "25.00 € без ДДС" — smaller, muted
  - ДДС: "5.00 € ДДС" — smaller, muted
- Stock status:
  - If StockQuantity > 0: "В наличност" green badge
  - If StockQuantity == 0: "Изчерпан" red badge
- Unit: "Мерна единица: кг" or "Мерна единица: м²"

Order section (below prices):
- Quantity input: InputNumber<decimal>, min 0.01, step 0.01
  - Default value: 1
  - Label: "Количество ({unitDisplay})" e.g., "Количество (м²)"
- "Добави в количката" button (btn-primary btn-lg)
  - Disabled if out of stock
  - Doesn't function yet — placeholder until cart is built in Session 3.3

- "Обратно към каталога" link below → navigates to /catalog

Error/loading states:
- Loading spinner while product loads
- If product not found (404): show "Продуктът не е намерен." with
  link "Обратно към каталога"

All text in Bulgarian. Bootstrap 5 only. Mobile: image stacks above info.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Navigate to /catalog → click on a product → arrives at /products/{id}
- [ ] Breadcrumb shows: Каталог > Натурален камък > Гранит сив
- [ ] Breadcrumb links work (catalog, category-filtered catalog)
- [ ] Product image displayed (or placeholder)
- [ ] All three prices shown: with ДДС, without ДДС, ДДС amount
- [ ] Currency formatted as "XX.XX €"
- [ ] Stock status badge shown (green "В наличност")
- [ ] Quantity input works, allows decimals
- [ ] "Добави в количката" button present (doesn't need to work yet)
- [ ] Navigate to /products/99999 → "Продуктът не е намерен." shown
- [ ] "Обратно към каталога" link works
- [ ] Mobile responsive: image stacks above text

### Commit
```bash
git add .
git commit -m "Epic 05: Story 5.2 — Product detail page with full pricing display"
```

---

## Session 3.3 — Cart Service + Cart Icon + Integration (Epic 06, Stories 6.1–6.3)

### Prompt 1
```
Read docs/conventions.md and planning/epics/06-cart-and-checkout.md.

Implement Stories 6.1, 6.2, and 6.3 — Cart state management, cart icon,
and integration with catalog/product detail.

Story 6.1 — Cart Service:
- Create Models/CartItem.cs in the Blazor client:
  ProductId (int), ProductName (string), UnitPriceWithVat (decimal),
  VatAmount (decimal), UnitPriceWithoutVat (decimal), Unit (int),
  UnitDisplay (string), Quantity (decimal), ImagePath (string, nullable)
- Create Services/CartService.cs (NOT an interface — concrete class):
  - Registered as SINGLETON in Program.cs (in Blazor WASM, singleton = per tab)
  - Private List<CartItem> _items
  - public event Action OnCartChanged — fired on every modification
  - Methods:
    - AddItem(CartItem item) — if product already in cart, increase quantity
    - UpdateQuantity(int productId, decimal quantity) — update existing item
    - RemoveItem(int productId)
    - ClearCart()
    - GetItems() → IReadOnlyList<CartItem>
    - GetTotalWithVat() → decimal (sum of qty × unitPriceWithVat)
    - GetTotalVat() → decimal (sum of qty × vatAmount)
    - GetTotalWithoutVat() → decimal (sum of qty × unitPriceWithoutVat)
    - GetItemCount() → int (number of distinct items)
  - Every method that modifies the list calls OnCartChanged?.Invoke()

Story 6.2 — Cart Icon:
- Create Components/CartIcon.razor
- Injects CartService
- Shows Bootstrap nav-link with cart icon (use Unicode 🛒 or Bootstrap
  icon if available)
- Badge showing item count (Bootstrap badge bg-danger rounded-pill)
- Badge hidden when cart is empty (count == 0)
- Clicking navigates to /cart
- Subscribes to CartService.OnCartChanged to re-render
- Implements IDisposable to unsubscribe
- Add CartIcon to MainLayout.razor in the public navigation bar (right side)

Story 6.3 — Integration with Catalog and Product Detail:
- In ProductCard.razor: "Добави в количката" button now calls CartService.AddItem()
  with quantity 1 and the product's data. Show a brief success alert/toast
  after adding: "Продуктът е добавен в количката."
- In ProductDetail.razor: "Добави в количката" button calls CartService.AddItem()
  with the quantity from the input field. Show same success alert.
- Use a simple Bootstrap toast or alert that auto-dismisses after 2 seconds.
  Create a reusable Components/ToastNotification.razor if it doesn't exist:
  - Shows a Bootstrap toast at the top-right
  - Auto-dismisses after 2 seconds
  - Can be triggered from any page via a shared service or CascadingParameter

After adding from catalog card, the cart icon badge should update immediately.
After adding from detail page, same behavior.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Cart icon visible in public nav bar with no badge (cart empty)
- [ ] Go to /catalog → click "Добави в количката" on a product
- [ ] Toast appears: "Продуктът е добавен в количката."
- [ ] Cart icon badge shows "1"
- [ ] Add same product again → badge still "1" (quantity increased internally)
- [ ] Add a different product → badge shows "2"
- [ ] Go to product detail → set quantity to 3.5 → click "Добави в количката"
- [ ] Toast appears, cart badge updates
- [ ] Click cart icon → navigates to /cart (page may be empty — that's next)
- [ ] Cannot add out-of-stock products (button disabled)
- [ ] Toast auto-dismisses after 2 seconds

### Commit
```bash
git add .
git commit -m "Epic 06: Stories 6.1-6.3 — Cart service, cart icon, catalog integration"
```

---

## Session 3.4 — Cart Page (Epic 06, Story 6.4)

### Prompt 1
```
Read docs/conventions.md and planning/epics/06-cart-and-checkout.md.

Implement Story 6.4 — Cart Page.

Page at /cart:
- Public, no auth, uses MainLayout
- Page title: "Количка"
- Injects CartService

If cart is empty:
- Show centered message: "Количката ви е празна."
- "Разгледайте каталога" button → links to /catalog

If cart has items — display as a table (desktop) / cards (mobile):

Table columns:
- Снимка (small thumbnail, 60x60, placeholder if null)
- Продукт (product name)
- Ед. цена с ДДС ("XX.XX €")
- Количество: editable InputNumber<decimal> field (min 0.01, step 0.01)
  - On change: call CartService.UpdateQuantity() — updates instantly
- Мерна ед. ("кг" or "м²")
- Общо: row total = quantity × unitPriceWithVat, formatted as "XX.XX €"
- Действия: remove button (× icon, btn-outline-danger btn-sm)
  - On click: CartService.RemoveItem() — row disappears, totals recalculate

Below the table — Totals section (right-aligned, Bootstrap card):
- Сума без ДДС: {GetTotalWithoutVat()} €
- Общо ДДС: {GetTotalVat()} €
- **Обща сума: {GetTotalWithVat()} €** (bold, larger text)

Buttons below totals:
- "Продължи към поръчка" (btn-primary btn-lg) → navigates to /checkout
- "Продължи пазаруването" (btn-outline-secondary) → navigates to /catalog

All totals recalculate instantly when quantity changes or items are removed.
The page subscribes to CartService.OnCartChanged for reactivity.

Mobile responsive:
- On mobile, show cart items as Bootstrap cards instead of a table
- Each card shows: image, name, price, quantity input, total, remove button

All text in Bulgarian. Bootstrap 5 only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:

**Empty cart:**
- [ ] Navigate to /cart with empty cart → "Количката ви е празна." shown
- [ ] "Разгледайте каталога" link works

**With items (add some from catalog first):**
- [ ] Cart table shows all added items with correct data
- [ ] Product images shown (or placeholder)
- [ ] Prices formatted as "XX.XX €"
- [ ] Change quantity of an item → row total recalculates instantly
- [ ] Totals section updates: Сума без ДДС, Общо ДДС, Обща сума all correct
- [ ] Set quantity to 2.5 → row total = 2.5 × unit price
- [ ] Click remove (×) on an item → item disappears, totals recalculate
- [ ] Cart icon badge updates when items removed
- [ ] Remove all items → empty cart message appears
- [ ] "Продължи към поръчка" navigates to /checkout
- [ ] "Продължи пазаруването" navigates to /catalog
- [ ] Mobile: check at 375px width — cards layout instead of table

### Commit
```bash
git add .
git commit -m "Epic 06: Story 6.4 — Cart page with editable quantities and totals"
```

---

## Session 3.5 — Order Entities + API (Epic 06, Story 6.6)

> **Note**: I'm doing Story 6.6 (backend) BEFORE Story 6.5 (checkout UI) so the API
> is ready and tested before building the checkout page against it.

### Prompt 1 (Plan)
```
Read docs/conventions.md, docs/database-schema.md (Order, OrderCustomerInfo,
OrderItem sections, and all related enums), docs/api-endpoints.md
(POST /api/orders), and planning/epics/06-cart-and-checkout.md.

I want to implement Story 6.6 — Order entities, migration, and the
POST /api/orders endpoint for placing orders.

Before writing any code, tell me:
- What entities will you create and what fields will each have?
- What enums will you create?
- What DTOs will you need for the create order request and response?
- How will you generate the order number (NSI-YYYYMMDD-XXXX)?
- How will you snapshot product prices into OrderItem?
- What validation will you perform?

Don't write any code yet.
```

> **Wait for response. Carefully verify:**
> - Order entity matches database-schema.md exactly (including ConfirmedAt, CompletedAt, IsCancelled)
> - OrderCustomerInfo has ALL fields (nullable, validation in service layer)
> - OrderItem snapshots: ProductName, UnitPriceWithoutVat, VatAmount, UnitPriceWithVat, Unit
> - Enums: CustomerType (Individual=0, Company=1), OrderStatus (Pending=0, Confirmed=1, Completed=2), DeliveryMethod (Pickup=0, Delivery=1)
> - Order number format correct
> - Stock NOT decremented on placement
>
> **Correct if needed, then:**

### Prompt 2 (Execute)
```
Proceed with the implementation:

1. Create Enums in Models/Entities/:
- CustomerType.cs: Individual = 0, Company = 1
- OrderStatus.cs: Pending = 0, Confirmed = 1, Completed = 2
- DeliveryMethod.cs: Pickup = 0, Delivery = 1

2. Create Entities in Models/Entities/:
- Order.cs: Id, OrderNumber (max 20, unique), CustomerType, Status (default Pending),
  DeliveryMethod, DeliveryFee (nullable decimal 18,2), IsCancelled (default false),
  CreatedAt, ConfirmedAt (nullable), CompletedAt (nullable), UpdatedAt
  Navigation: OrderCustomerInfo CustomerInfo, ICollection<OrderItem> Items
- OrderCustomerInfo.cs: Id, OrderId (unique FK), FullName (max 200, nullable),
  Phone (max 20, nullable), Address (max 500, nullable), CompanyName (max 200, nullable),
  Eik (max 13, nullable), Mol (max 200, nullable), ContactPerson (max 200, nullable),
  ContactPhone (max 20, nullable)
- OrderItem.cs: Id, OrderId (FK), ProductId (FK), ProductName (max 200, required),
  Quantity (decimal 18,2), UnitPriceWithoutVat (decimal 18,2), VatAmount (decimal 18,2),
  UnitPriceWithVat (decimal 18,2), Unit (int, UnitType enum stored as snapshot)

3. AppDbContext:
- Add DbSet<Order>, DbSet<OrderCustomerInfo>, DbSet<OrderItem>
- Configure all relationships, constraints, indexes as per database-schema.md
- Order → OrderCustomerInfo: one-to-one, cascade delete
- Order → OrderItem: one-to-many, cascade delete
- OrderItem → Product: many-to-one, DeleteBehavior.Restrict

4. Migration named "AddOrderEntities", apply it.

5. Create DTOs in Models/DTOs/:
- CreateOrderRequest.cs:
  { CustomerType (int), DeliveryMethod (int),
    CustomerInfo: { FullName, Phone, Address, CompanyName, Eik, Mol,
    ContactPerson, ContactPhone },
    Items: List<OrderItemRequest> { ProductId (int), Quantity (decimal) } }
- CreateOrderResponse.cs: { OrderNumber, Message }

6. Create Services/IOrderService.cs and Services/OrderService.cs:
- CreateAsync(CreateOrderRequest) → CreateOrderResponse
- Logic:
  a. Validate customer type and required fields:
     - Individual: FullName, Phone required
     - Company: CompanyName, Eik, Mol, ContactPerson, ContactPhone required
     - Eik must be 9 or 13 digits
     - If delivery: Address required
  b. Validate all items: product exists, is active, quantity > 0
  c. Generate OrderNumber: NSI-YYYYMMDD-XXXX (sequential per day)
  d. Create Order with Status = Pending
  e. Create OrderCustomerInfo
  f. For each item: create OrderItem with SNAPSHOT of current product prices
     (copy ProductName, PriceWithoutVat, VatAmount, PriceWithVat, Unit from Product)
  g. Do NOT decrement stock
  h. Save all in a transaction
  i. Return { OrderNumber, Message: "Вашата поръчка е приета успешно." }

7. Create Controllers/OrdersController.cs with POST /api/orders (PUBLIC, no auth)
- Returns 201 with CreateOrderResponse
- Returns 400 with Bulgarian error messages for validation failures:
  - Missing required field: "Полето '{fieldName}' е задължително."
  - Invalid EIK: "ЕИК/Булстат трябва да е 9 или 13 цифри."
  - Product not found: "Продукт с ID {id} не е намерен."
  - Product inactive: "Продукт '{name}' не е наличен."

Register OrderService in DI.
```

### Verify
```bash
dotnet ef database update --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Api
```

Test via Swagger — Individual customer, pickup:
```json
{
  "customerType": 0,
  "deliveryMethod": 0,
  "customerInfo": {
    "fullName": "Иван Петров",
    "phone": "+359888123456"
  },
  "items": [
    { "productId": 1, "quantity": 5.00 },
    { "productId": 3, "quantity": 10.00 }
  ]
}
```
- [ ] Returns 201 with orderNumber like "NSI-20260219-0001"
- [ ] Order exists in database with Status = Pending
- [ ] OrderCustomerInfo has FullName and Phone
- [ ] OrderItems have snapshotted prices from products
- [ ] Product stock quantities UNCHANGED

Test — Company customer, delivery:
```json
{
  "customerType": 1,
  "deliveryMethod": 1,
  "customerInfo": {
    "companyName": "Строй ЕООД",
    "eik": "123456789",
    "mol": "Георги Димитров",
    "contactPerson": "Мария Иванова",
    "contactPhone": "+359899987654",
    "address": "бул. България 100, Пловдив"
  },
  "items": [
    { "productId": 1, "quantity": 20.00 }
  ]
}
```
- [ ] Returns 201 with next sequential order number
- [ ] OrderCustomerInfo has all company fields

Test validation:
- [ ] Missing fullName for individual → 400 with Bulgarian error
- [ ] Missing companyName for company → 400
- [ ] Invalid EIK "123" → 400 "ЕИК/Булстат трябва да е 9 или 13 цифри."
- [ ] Delivery without address → 400
- [ ] Non-existent productId → 400
- [ ] Empty items array → 400

### Commit
```bash
git add .
git commit -m "Epic 06: Story 6.6 — Order entities, migration, POST /api/orders endpoint"
```

---

## Session 3.6 — Checkout Page (Epic 06, Story 6.5)

### Prompt 1
```
Read docs/conventions.md and planning/epics/06-cart-and-checkout.md.

Implement Story 6.5 — Checkout Page.

Page at /checkout:
- Public, no auth, uses MainLayout
- Injects CartService and a client-side OrderService
- Redirects to /cart if cart is empty (check on OnInitializedAsync)

Create Services/IOrderService.cs and Services/OrderService.cs in the
Blazor CLIENT project:
- PlaceOrderAsync(CreateOrderRequest) → CreateOrderResponse
- Calls POST /api/orders

Page layout — use a Bootstrap card-based stepped form. All sections
visible on one page (not a multi-step wizard), scrollable:

**Section 1 — "Тип клиент" (Customer Type):**
- Two Bootstrap radio buttons styled as cards/buttons:
  - "Физическо лице" (Individual)
  - "Фирма" (Company)
- Default: none selected (must choose one)

**Section 2 — "Данни за клиента" (Customer Information):**
- This section appears after customer type is selected
- Form fields change based on selection:

  If Физическо лице:
  - Име и фамилия (text, required)
  - Телефонен номер (text/tel, required)

  If Фирма:
  - Име на фирмата (text, required)
  - ЕИК / Булстат (text, required, placeholder: "9 или 13 цифри")
  - МОЛ (text, required)
  - Лице за контакт (text, required)
  - Телефон за контакт (text/tel, required)

**Section 3 — "Метод на получаване" (Delivery Method):**
- Two radio buttons:
  - "Вземане от обекта" (Pickup)
  - "Доставка" (Delivery)
- If Delivery selected:
  - Show "Адрес за доставка" text input (required)
  - Show info alert (Bootstrap alert-info):
    "Цената за доставка ще бъде определена и добавена от нас след
    получаване на поръчката."

**Section 4 — "Обобщение на поръчката" (Order Summary):**
- Table of cart items: Продукт, Количество, Мерна ед., Ед. цена с ДДС, Общо
- Сума без ДДС: XX.XX €
- Общо ДДС: XX.XX €
- Обща сума с ДДС: XX.XX € (bold)
- If delivery selected: note "Цената за доставка ще бъде добавена
  допълнително."

**Section 5 — Customer info summary:**
- Read-only display of what the customer entered (name, phone, address, etc.)
- Shown in a Bootstrap card with muted header "Вашите данни"

**Submit:**
- "Потвърди поръчката" button (btn-success btn-lg, full width)
- Loading spinner on button while submitting
- On success: navigate to /order-confirmation/{orderNumber}
- On validation error from API: show Bootstrap alert-danger at the top
  of the page with the error message
- Client-side validation: validate all required fields before sending.
  Show per-field validation errors using Bootstrap is-invalid class
  and Bulgarian messages:
  - "Полето е задължително." for empty required fields
  - "ЕИК/Булстат трябва да е 9 или 13 цифри." for invalid EIK

All text in Bulgarian. Bootstrap 5 only. Mobile responsive.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:

**Empty cart redirect:**
- [ ] Go to /checkout with empty cart → redirected to /cart

**Add items from catalog, then go to /checkout:**

**Individual + Pickup:**
- [ ] Select "Физическо лице" → name and phone fields appear
- [ ] Select "Вземане от обекта" → no address field
- [ ] Try submit with empty fields → validation errors in Bulgarian
- [ ] Fill in name + phone → order summary visible
- [ ] Customer info summary shows entered data
- [ ] Click "Потвърди поръчката" → loading spinner → navigates to confirmation page

**Company + Delivery:**
- [ ] Add items to cart again
- [ ] Select "Фирма" → 5 company fields appear
- [ ] Select "Доставка" → address field appears + info message about delivery fee
- [ ] Enter invalid EIK "123" → validation error
- [ ] Enter valid EIK "123456789" → error clears
- [ ] Fill all fields → submit → success

**Validation:**
- [ ] Per-field validation errors shown with red borders and Bulgarian text
- [ ] API error (if any) shown as alert at top of page
- [ ] Loading spinner visible during API call

### Commit
```bash
git add .
git commit -m "Epic 06: Story 6.5 — Checkout page with customer type, delivery, validation"
```

---

## Session 3.7 — Order Confirmation Page (Epic 06, Story 6.7)

### Prompt 1
```
Read docs/conventions.md and planning/epics/06-cart-and-checkout.md.

Implement Story 6.7 — Order Confirmation Page.

Page at /order-confirmation/{orderNumber}:
- Public, no auth, uses MainLayout
- Route parameter: orderNumber (string)

Layout — centered Bootstrap card with:
- Success icon: large green checkmark (use Unicode ✅ or a Bootstrap
  text-success large icon: ✓)
- Heading: "Вашата поръчка е приета!" (h2, text-success)
- Order number: "Номер на поръчка: {orderNumber}" (h4 or prominent text,
  displayed in a Bootstrap alert-light or card with a copy-friendly format)
- Message: "Ще се свържем с вас за потвърждение на поръчката."
- If the URL doesn't have a valid order number format, still show the page
  but just display the number as-is (this page is just a confirmation —
  no API call needed)
- "Обратно към каталога" button (btn-primary) → links to /catalog

On page load:
- Call CartService.ClearCart() to ensure the cart is emptied
  (it should already be cleared from the checkout page, but this is a
  safety measure in case the user navigates directly)

Simple, clean, centered design. No need to fetch order data from the API.

All text in Bulgarian.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test the FULL customer flow end-to-end:
- [ ] Go to /catalog
- [ ] Add 2-3 products to cart (mix of кг and м² products)
- [ ] Go to /cart → verify items, quantities, totals correct
- [ ] Click "Продължи към поръчка"
- [ ] Fill in customer info (test both individual and company)
- [ ] Select delivery method
- [ ] Verify order summary shows correct items and totals
- [ ] Click "Потвърди поръчката"
- [ ] Arrives at /order-confirmation/NSI-XXXXXXXX-XXXX
- [ ] Success message displayed with order number
- [ ] Cart icon badge is now empty (0 items)
- [ ] "Обратно към каталога" button works
- [ ] Check database: order exists with correct data, prices snapshotted, stock unchanged

### Commit
```bash
git add .
git commit -m "Epic 06: Story 6.7 — Order confirmation page"
```

---

## Phase 3 Complete ✅

At this point you should have a fully functional customer storefront:
- ✅ Catalog with category filter, search, pagination
- ✅ Product detail with full pricing (with/without ДДС)
- ✅ Shopping cart with editable quantities and live totals
- ✅ Checkout with individual/company types and pickup/delivery options
- ✅ Order placement (saved to DB with price snapshots)
- ✅ Order confirmation page
- ✅ Cart icon with live badge count

**A real customer could now place an order through your website.**

Update planning/overview.md:
```markdown
| 05 | Public Catalog & Product Detail | ✅ Completed | Epic 04       |
| 06 | Cart & Checkout                 | ✅ Completed | Epic 05       |
```

```bash
git add planning/overview.md
git commit -m "Update planning status: Phase 3 complete"
```

**Next**: Phase 4 — Order Management (Admin). Start a fresh Claude Code session.

---

## Troubleshooting

### If cart items disappear on page navigation:
```
Cart items are being lost when navigating between pages. CartService
must be registered as a SINGLETON in Program.cs, not Scoped or Transient.
In Blazor WASM, a singleton lives for the lifetime of the browser tab.
Check the service registration in Program.cs.
```

### If cart icon doesn't update after adding items:
```
The cart icon badge is not updating when items are added. CartIcon.razor
must subscribe to CartService.OnCartChanged event in OnInitialized and
call StateHasChanged() in the event handler. It must also implement
IDisposable to unsubscribe. Show me the current CartIcon.razor code.
```

### If checkout fails with CORS error:
```
The POST /api/orders request is failing with a CORS error. This is a
public endpoint that doesn't require authentication. Make sure CORS is
configured to allow POST requests from the Blazor client origin and
that the Content-Type header (application/json) is allowed.
```

### If order number generation creates duplicates:
```
The order number generation might have a race condition. Make sure the
order number generation and order creation happen within a database
transaction. Use a retry mechanism if a unique constraint violation
occurs, or use a database sequence for the sequential number.
```

### If snapshotted prices in OrderItem are wrong:
```
The prices in OrderItem don't match the product prices. Make sure
the order creation code reads the CURRENT product prices from the
database (not from the request) and copies them into OrderItem fields:
ProductName, UnitPriceWithoutVat, VatAmount, UnitPriceWithVat, and Unit.
These must be copied from the Product entity, not passed by the client.
```

### If decimal quantities don't work in cart:
```
The quantity input in the cart is not accepting decimal values like 2.5.
In Blazor, make sure you're using InputNumber<decimal> (not int) and
the HTML input has step="0.01". The CartItem.Quantity field must be
decimal type.
```

### If checkout form validation shows English messages:
```
The form validation is showing English error messages instead of Bulgarian.
Make sure all DataAnnotations on the form model use the ErrorMessage
parameter with Bulgarian text. For example:
[Required(ErrorMessage = "Полето е задължително.")]
Do NOT use the default English messages.
```
