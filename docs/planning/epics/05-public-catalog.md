# Epic 05: Public Catalog & Product Detail

## Description
Customer-facing catalog page where users can browse products by category, search by name, and view detailed product information including prices and stock availability.

## Dependencies
- Epic 04 (Product Management) must be completed — products and categories must exist in the database.

## Stories

---

### Story 5.1: Catalog Page

**As** a customer, **I want** to browse all available products **so that** I can find what I need to buy.

**Acceptance Criteria:**
- [ ] Page at `/catalog` (or `/каталог`) with title "Каталог"
- [ ] Products displayed as cards in a responsive grid (3 columns desktop, 2 tablet, 1 mobile)
- [ ] Each product card shows:
  - Product image (placeholder if no image)
  - Product name
  - Price with ДДС (e.g., "24.00 €")
  - ДДС amount (e.g., "вкл. 4.00 € ДДС")
  - Unit (кг or м²)
  - "Добави в количката" button
- [ ] Out-of-stock products show "Изчерпан" badge instead of add-to-cart button
- [ ] Category filter: sidebar on desktop, dropdown on mobile — lists all categories + "Всички" option
- [ ] Search bar above the product grid — filters by product name
- [ ] Pagination at the bottom (12 products per page)
- [ ] Clicking a product card (or product name) navigates to product detail page
- [ ] Loading spinner shown while products load
- [ ] If no products match filter/search: show "Няма намерени продукти." message

**Tasks:**
- Create `Pages/Public/Catalog.razor`
- Create `Components/ProductCard.razor` (reusable card component)
- Implement category filter (call `GET /api/categories`, filter via query param)
- Implement search input with debounce (300ms)
- Implement pagination component (reusable `Components/Pagination.razor`)
- Handle empty states and loading states
- Responsive layout with Bootstrap grid

---

### Story 5.2: Product Detail Page

**As** a customer, **I want** to see full details of a product **so that** I can decide whether to buy it and at what quantity.

**Acceptance Criteria:**
- [ ] Page at `/products/{id}` with product name as page title
- [ ] Breadcrumb: Каталог > {Category Name} > {Product Name}
- [ ] Layout: image on the left (or top on mobile), details on the right
- [ ] Displays:
  - Product image (large, placeholder if none)
  - Product name
  - Category name (as a link back to catalog filtered by that category)
  - Description (if available)
  - **Цена с ДДС**: prominently displayed (e.g., "24.00 €")
  - **Цена без ДДС**: displayed below (e.g., "20.00 € без ДДС")
  - **ДДС**: displayed (e.g., "4.00 € ДДС")
  - Unit of measurement (кг or м²)
  - Stock status: "В наличност" (green) or "Изчерпан" (red)
- [ ] Quantity input (number, min 0.01, step depends on unit — 0.01 for both кг and м²)
- [ ] "Добави в количката" button (disabled if out of stock)
- [ ] After adding to cart: show success toast/alert "Продуктът е добавен в количката."
- [ ] "Обратно към каталога" link
- [ ] If product not found (invalid ID or inactive): show "Продуктът не е намерен." with link back to catalog

**Tasks:**
- Create `Pages/Public/ProductDetail.razor`
- Call `GET /api/products/{id}` on page load
- Implement breadcrumb component
- Display all price fields with proper formatting
- Implement quantity input with validation
- Connect "Добави в количката" to cart state (Epic 06 dependency — for now, just log to console or prepare the interface)
- Handle 404 / not found state
- Responsive layout
