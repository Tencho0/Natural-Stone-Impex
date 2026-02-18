# Product Requirements Document — Natural Stone Impex

## 1. Executive Summary

Natural Stone Impex is a building materials shop in Bulgaria that needs a web-based system to manage its inventory and accept customer orders online. The system will replace manual processes (phone orders, paper-based stock tracking) with a simple digital workflow.

The product has two sides:
- A **public storefront** where customers can browse the catalog and place orders without creating an account.
- An **admin panel** where the shop owner manages products, stock, incoming deliveries, and customer orders.

This is version 1.0 — focused on core functionality. No online payments, no customer accounts, no notifications.

---

## 2. Problem Statement

Today, the shop handles orders and inventory manually:
- Customers call or visit to ask about available products and prices.
- The owner tracks stock in spreadsheets or on paper.
- There is no centralized view of what's in stock, what's been ordered, or what's been delivered.
- Invoices from suppliers are filed physically, making it hard to track delivery history.

This leads to:
- Lost or forgotten orders.
- Inaccurate stock counts (overselling or not knowing when to reorder).
- Time wasted answering repetitive phone calls about product availability and prices.
- No order history or paper trail for deliveries.

---

## 3. Goals & Success Criteria

### Business Goals
1. **Reduce phone inquiries** — Customers can see product availability and prices online, 24/7.
2. **Accurate stock tracking** — Stock is updated automatically when deliveries arrive (invoices) and when orders are confirmed.
3. **Order management** — All orders are centralized, trackable, and have a clear lifecycle (pending → confirmed → completed).
4. **Delivery records** — Every incoming delivery is logged with supplier info, invoice number, and date.
5. **Simple receipts** — The owner can print a стокова разписка (non-official receipt) for every confirmed order.

### Success Criteria (V1)
- The owner can add/edit/remove products and categories without technical help.
- A customer can place an order in under 3 minutes.
- Stock numbers are always accurate (auto-updated on invoice entry and order confirmation).
- The owner can view all orders and their statuses from a single screen.
- The owner can print a receipt for any confirmed order.

---

## 4. Target Users

### Customer (Клиент)
- **Who**: Construction companies, contractors, and individual buyers in Bulgaria.
- **Tech level**: Basic — can browse a website, fill out a form, use a phone.
- **Needs**: See what's available, see prices, place an order quickly without creating an account.
- **Two sub-types**:
  - **Физическо лице** (Individual) — provides name, phone, and optionally a delivery address.
  - **Фирма** (Company) — provides company name, ЕИК/Булстат, МОЛ, contact person and phone, and optionally a delivery address.

### Shop Owner / Admin (Администратор)
- **Who**: The owner of Natural Stone Impex (single person).
- **Tech level**: Comfortable using a computer but not a developer.
- **Needs**: Manage products and prices, track stock, process orders, log deliveries, print receipts.

---

## 5. User Stories

### 5.1 Customer Stories

**Browsing & Shopping**
- As a customer, I want to see all available products with prices so I can decide what to buy without calling the shop.
- As a customer, I want to filter products by category so I can quickly find what I need (e.g., only cement products).
- As a customer, I want to search for a product by name so I can find it quickly.
- As a customer, I want to see both the price with and without ДДС on the product detail page so I know the full cost breakdown.
- As a customer, I want to see if a product is in stock before adding it to my cart.

**Ordering**
- As a customer, I want to add products to a cart and adjust quantities before placing an order.
- As a customer, I want to choose whether I'm ordering as an individual or as a company, so the correct information is collected.
- As a customer, I want to choose between picking up my order or having it delivered.
- As a customer, I want to see a summary of my order before confirming it.
- As a customer, I want to receive an order number after placing my order so I can reference it when contacting the shop.

### 5.2 Admin Stories

**Authentication**
- As the admin, I want to log in with a username and password so that only I can access the management panel.

**Category Management**
- As the admin, I want to create, edit, and delete product categories so I can organize my catalog.
- As the admin, I should not be able to delete a category that still has products in it, to prevent accidental data loss.

**Product Management**
- As the admin, I want to add new products with a name, description, category, prices (without ДДС, ДДС amount, with ДДС), unit of measurement, stock quantity, and a photo.
- As the admin, I want to edit existing products to update prices, descriptions, or stock.
- As the admin, I want to see a list of all products with their current stock levels so I can identify what needs restocking.
- As the admin, I want to see low stock alerts on my dashboard so I don't run out of popular items.

**Order Management**
- As the admin, I want to see all incoming orders with their status so I can process them in order.
- As the admin, I want to view the full details of an order including customer info, items, and totals.
- As the admin, I want to add a delivery fee to an order before confirming it (if the customer chose delivery).
- As the admin, I want to confirm an order, which should automatically reduce stock for each ordered item.
- As the admin, I should see an error if I try to confirm an order but there isn't enough stock for one or more items.
- As the admin, I want to mark a confirmed order as completed when the customer has received their goods.
- As the admin, I want to cancel a pending order if the customer changes their mind (without affecting stock).
- As the admin, I want to print a стокова разписка (receipt) for any confirmed or completed order.

**Invoice / Delivery Management**
- As the admin, I want to record a new delivery by entering the supplier name, invoice number, invoice date, and a list of products with quantities and purchase prices.
- As the admin, I want stock to be automatically updated when I save a new delivery so I don't have to manually adjust each product.
- As the admin, I want to see a history of all deliveries with dates and supplier info so I have a record of incoming stock.

---

## 6. Feature Breakdown by Priority

### Must Have (V1 — Launch)
| Feature                           | User   |
|-----------------------------------|--------|
| Product catalog with categories   | Customer |
| Product detail page with prices   | Customer |
| Cart and checkout flow            | Customer |
| Individual and company order types| Customer |
| Pickup vs delivery selection      | Customer |
| Contact page                      | Customer |
| Admin login                       | Admin  |
| Product CRUD with image upload    | Admin  |
| Category CRUD                     | Admin  |
| Order list with status filter     | Admin  |
| Order confirmation (decrements stock) | Admin |
| Mark order as completed           | Admin  |
| Add delivery fee to orders        | Admin  |
| Receipt printing                  | Admin  |
| Invoice entry (auto-updates stock)| Admin  |
| Invoice/delivery history          | Admin  |
| Admin dashboard with stats        | Admin  |

### Nice to Have (V1.1 — Post-Launch)
| Feature                           | User   |
|-----------------------------------|--------|
| Order cancellation                | Admin  |
| Change admin password             | Admin  |
| Low stock threshold configuration | Admin  |
| Product search on catalog         | Customer |
| Cart persistence (localStorage)   | Customer |

### Future (V2+)
| Feature                               |
|----------------------------------------|
| Email/SMS order notifications          |
| Multiple admin users with roles        |
| Online payment (card, bank transfer)   |
| Customer accounts and order history    |
| Export to Excel/PDF                    |
| Advanced reporting and analytics       |
| Multilingual support                   |
| Automatic VAT calculation              |

---

## 7. Business Rules

### Pricing
- Every product has three price fields: **price without ДДС**, **ДДС amount**, and **price with ДДС** — all in Euro (€).
- All three are entered by the admin manually. The system assists by auto-calculating, but the admin can override.
- Validation: Price with ДДС must equal Price without ДДС + ДДС amount.

### Stock Management
- Stock is tracked per product as a decimal number (supports fractional units like 2.5 кг).
- **Stock increases** when a new invoice/delivery is entered.
- **Stock decreases** when an admin confirms an order (not when the customer places it).
- If stock is insufficient to confirm an order, the system blocks confirmation and shows an error.
- Out-of-stock products are shown in the catalog with an "Изчерпан" label and no add-to-cart option.

### Orders
- Orders go through three statuses: **Чакаща** (Pending) → **Потвърдена** (Confirmed) → **Завършена** (Completed).
- A pending order can be cancelled (separate flag, no stock impact).
- Delivery fee is added manually by the admin after the order is placed.
- Order items store a snapshot of product prices at the time of order — price changes do not affect existing orders.
- Order numbers follow the format: `NSI-YYYYMMDD-XXXX` (auto-generated).

### Invoices
- Invoices represent incoming stock deliveries from suppliers.
- Invoices are immutable after creation (cannot be edited or deleted) to maintain audit integrity.
- If a correction is needed, the admin adjusts stock manually via product edit or creates a corrective invoice.

### Units of Measurement
- Two units available: **кг** (kilograms) and **м²** (square meters).
- The unit is set per product by the admin and cannot be changed after orders exist for that product (data integrity).

---

## 8. Constraints & Assumptions

### Constraints
- Bulgarian language only — no multilingual support in V1.
- Single admin user — no multi-user or role-based access.
- No online payment — orders are placed online but paid offline (cash, bank transfer, etc.).
- No email/SMS — the admin checks the system manually for new orders.
- Receipt is a стокова разписка (informal) — not an official фактура (tax invoice).

### Assumptions
- The shop owner has reliable internet access.
- Customers have access to a modern web browser (Chrome, Firefox, Safari, Edge — last 2 versions).
- Product images will be provided by the admin (photos or supplier images).
- The shop has a relatively small catalog (under 500 products) — no need for advanced search infrastructure.
- Traffic will be low to moderate — no need for CDN, caching layers, or horizontal scaling in V1.

---

## 9. Open Questions

These items need clarification or a decision before or during development:

1. **Shop contact details** — What is the actual address, phone number, email, and working hours for the contacts page?
2. **Low stock threshold** — What default value should trigger a low stock alert? (Suggested: 10 units)
3. **Maximum image size** — 5MB suggested. Is this acceptable?
4. **Order number format** — `NSI-YYYYMMDD-XXXX` proposed. Any preference?
5. **Hosting environment** — Undecided. Needs to be resolved before deployment docs are written.
