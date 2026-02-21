# Epic 06: Cart & Checkout

## Description
Customers can add products to a shopping cart, review their cart, and complete the checkout process by providing their information and placing an order. The cart supports both individual and company customers, with optional delivery.

## Dependencies
- Epic 05 (Public Catalog) must be completed.

## Stories

---

### Story 6.1: Cart State Management

**As** a developer, **I want** a cart state management system **so that** customers can add, update, and remove items across pages.

**Acceptance Criteria:**
- [ ] `CartService` created as a scoped/singleton service managing cart state in memory
- [ ] Cart item model: `ProductId`, `ProductName`, `UnitPriceWithVat`, `VatAmount`, `UnitPriceWithoutVat`, `Unit`, `Quantity`, `ImagePath`
- [ ] Methods: `AddItem(item)`, `UpdateQuantity(productId, quantity)`, `RemoveItem(productId)`, `ClearCart()`, `GetItems()`, `GetTotalWithVat()`, `GetTotalVat()`, `GetItemCount()`
- [ ] Adding an item that already exists in the cart increases its quantity
- [ ] Cart state persists during the session (Blazor in-memory — lost on page refresh)
- [ ] Optional: persist to localStorage via JS interop for survival across refreshes
- [ ] Cart item count accessible globally (for cart icon badge)
- [ ] `CartService` raises an event/notification when cart changes (for UI updates)

**Tasks:**
- Create `Services/CartService.cs` with in-memory state
- Create `Models/CartItem.cs`
- Register as singleton in `Program.cs` (singleton in WASM = per-tab session)
- Implement all cart operations
- Add `OnCartChanged` event for UI reactivity
- Optional: add JS interop for localStorage persistence

---

### Story 6.2: Cart Icon in Navigation

**As** a customer, **I want** to see how many items are in my cart at all times **so that** I know when I've added something.

**Acceptance Criteria:**
- [ ] Cart icon (🛒 or Bootstrap icon) in the public navigation bar
- [ ] Badge showing the number of items in the cart (e.g., "3")
- [ ] Badge hidden when cart is empty
- [ ] Clicking the cart icon navigates to `/cart`
- [ ] Badge updates immediately when items are added/removed (reactive)

**Tasks:**
- Create `Components/CartIcon.razor`
- Subscribe to `CartService.OnCartChanged` event
- Add to `MainLayout.razor` navigation
- Style with Bootstrap badge

---

### Story 6.3: Integrate Cart with Catalog & Product Detail

**As** a customer, **I want** the "Добави в количката" buttons to actually add items to my cart.

**Acceptance Criteria:**
- [ ] "Добави в количката" on ProductCard (catalog) adds 1 unit of the product to the cart
- [ ] "Добави в количката" on ProductDetail page adds the specified quantity to the cart
- [ ] Success feedback shown: Bootstrap toast or alert "Продуктът е добавен в количката."
- [ ] Cart icon badge updates immediately
- [ ] Cannot add out-of-stock products (button disabled)

**Tasks:**
- Inject `CartService` into `Catalog.razor` and `ProductDetail.razor`
- Wire up add-to-cart buttons
- Add toast/alert notification component
- Verify cart icon updates

---

### Story 6.4: Cart Page

**As** a customer, **I want** to review my cart before checkout **so that** I can verify items and adjust quantities.

**Acceptance Criteria:**
- [ ] Page at `/cart` with title "Количка"
- [ ] Table/list of cart items:
  - Product image (small thumbnail)
  - Product name
  - Unit price with ДДС
  - Quantity (editable input — decimal, min 0.01)
  - Unit (кг / м²)
  - Row total (quantity × unit price with ДДС)
  - Remove button (✕ icon)
- [ ] Updating quantity recalculates the row total and cart totals instantly
- [ ] **Междинна сума** (Subtotal with ДДС): sum of all row totals
- [ ] **Общо ДДС** (Total VAT): sum of all VAT amounts × quantities
- [ ] "Продължи към поръчка" button → navigates to checkout
- [ ] "Продължи пазаруването" link → navigates to catalog
- [ ] If cart is empty: show message "Количката ви е празна." with link to catalog
- [ ] Responsive: card layout on mobile instead of table

**Tasks:**
- Create `Pages/Public/Cart.razor`
- Display cart items from `CartService`
- Implement quantity editing with instant recalculation
- Implement item removal
- Calculate and display subtotal and total VAT
- Handle empty cart state
- Responsive design

---

### Story 6.5: Checkout Page — Customer Info & Delivery

**As** a customer, **I want** to provide my information and choose a delivery method **so that** the shop can process my order.

**Acceptance Criteria:**
- [ ] Page at `/checkout` with title "Поръчка"
- [ ] Redirects to `/cart` if cart is empty
- [ ] **Step 1 — Customer type** (radio buttons):
  - Физическо лице (Individual)
  - Фирма (Company)
- [ ] **Step 2 — Customer info form** (changes based on selection):

  **Физическо лице fields:**
  - Име и фамилия (required, min 2 chars)
  - Телефонен номер (required, valid phone format)

  **Фирма fields:**
  - Име на фирмата (required, min 2 chars)
  - ЕИК / Булстат (required, 9 or 13 digits)
  - МОЛ (required, min 2 chars)
  - Лице за контакт (required, min 2 chars)
  - Телефон за контакт (required, valid phone format)

- [ ] **Step 3 — Delivery method** (radio buttons):
  - Вземане от обекта (Pickup)
  - Доставка (Delivery)
- [ ] If Delivery selected: show "Адрес за доставка" field (required, min 5 chars)
- [ ] If Delivery selected: info message "Цената за доставка ще бъде определена и добавена от нас след получаване на поръчката."

- [ ] **Step 4 — Order summary:**
  - Cart items table (name, qty, unit, unit price, row total)
  - Subtotal with ДДС
  - Total ДДС
  - Note about delivery fee if applicable
  - Customer info summary

- [ ] "Потвърди поръчката" button
- [ ] Form validation with Bulgarian error messages
- [ ] Loading state on submit

**Tasks:**
- Create `Pages/Public/Checkout.razor`
- Create customer type selection with conditional form rendering
- Implement form validation for both customer types
- Implement delivery method selection with conditional address field
- Display order summary section
- Prepare submit payload for API

---

### Story 6.6: Order Placement API

**As** a customer, **I want** my order saved in the system when I confirm it **so that** the shop owner can process it.

**Acceptance Criteria:**
- [ ] Order-related entities created:
  - `Order`: Id, OrderNumber, CustomerType (enum), Status (enum: Pending/Confirmed/Completed), DeliveryMethod (enum: Pickup/Delivery), DeliveryFee (decimal, nullable), IsCancelled (bool), CreatedAt, UpdatedAt
  - `OrderCustomerInfo`: Id, OrderId (FK), FullName, Phone, Address, CompanyName, Eik, Mol, ContactPerson, ContactPhone (all nullable — populated based on customer type)
  - `OrderItem`: Id, OrderId (FK), ProductId (FK), ProductName, Quantity, UnitPriceWithoutVat, VatAmount, UnitPriceWithVat, Unit (enum)
- [ ] `POST /api/orders` endpoint (public, no auth)
  - Accepts: customer type, customer info, delivery method, delivery address, list of items (productId, quantity)
  - Validates: all required fields based on customer type, all products exist and are active
  - Creates order with status `Pending`, generates `OrderNumber` (format: `NSI-YYYYMMDD-XXXX`)
  - Snapshots current product prices into OrderItem (not referenced — copied)
  - Does NOT decrement stock (that happens on admin confirmation)
  - Returns: `{ "orderNumber": "NSI-20260218-0001" }` with HTTP 201
- [ ] EF Core migration for Order, OrderCustomerInfo, OrderItem tables

**Tasks:**
- Create entities: `Order.cs`, `OrderCustomerInfo.cs`, `OrderItem.cs`
- Create enums: `CustomerType.cs`, `OrderStatus.cs`, `DeliveryMethod.cs`
- Create DTOs: `CreateOrderRequest.cs`, `OrderItemRequest.cs`, `CreateOrderResponse.cs`
- Create `Services/IOrderService.cs` and `Services/OrderService.cs`
- Create `Controllers/OrdersController.cs` (POST endpoint only for now)
- Implement order number generation
- Create and apply migration

---

### Story 6.7: Order Confirmation Page

**As** a customer, **I want** to see a confirmation after placing my order **so that** I know the order was received.

**Acceptance Criteria:**
- [ ] After successful order placement, navigate to `/order-confirmation/{orderNumber}`
- [ ] Page shows:
  - Success icon/message: "Вашата поръчка е приета!"
  - Order number prominently displayed: "Номер на поръчка: NSI-20260218-0001"
  - Message: "Ще се свържем с вас за потвърждение."
  - If delivery was selected: "Цената за доставка ще бъде определена допълнително."
  - "Обратно към каталога" button
- [ ] Cart is cleared after successful order
- [ ] Page works as a standalone URL (can be bookmarked — just shows the order number, no sensitive data)

**Tasks:**
- Create `Pages/Public/OrderConfirmation.razor`
- Pass order number via route parameter
- Clear cart on arrival (if not already cleared)
- Display confirmation message with order number
- Style with Bootstrap (centered, clean design with success icon)
