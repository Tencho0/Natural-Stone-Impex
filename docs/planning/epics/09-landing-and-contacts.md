# Epic 09: Landing Page & Contacts

## Description
The public-facing home page and contacts page. These are simple, mostly static pages that give the shop a professional online presence.

## Dependencies
- Epic 01 (Project Setup) must be completed.
- Can be built at any time after Epic 01, independently of other epics.

## Stories

---

### Story 9.1: Landing / Home Page

**As** a customer, **I want** to see a welcoming home page **so that** I understand what the shop offers and can navigate to the catalog.

**Acceptance Criteria:**
- [ ] Page at `/` (root route) with title "Natural Stone Impex"
- [ ] **Hero section:**
  - Shop name: "Natural Stone Impex"
  - Tagline/subtitle (e.g., "Качествени строителни материали" — "Quality building materials")
  - Call-to-action button: "Разгледайте каталога" → links to `/catalog`
  - Background image or solid color with professional look
- [ ] **About section** (brief):
  - Short paragraph about the shop (placeholder text, owner will replace)
  - Key selling points (e.g., quality materials, competitive prices, delivery available) — 3 icon cards
- [ ] **Featured categories section** (optional):
  - Display top categories (fetched from API) as clickable cards
  - Each card links to `/catalog?categoryId={id}`
  - Show category name and a generic icon or placeholder image
- [ ] **Contact summary:**
  - Phone number, address, working hours (placeholders)
  - "Свържете се с нас" button → links to `/contacts`
- [ ] Responsive design — looks good on mobile and desktop
- [ ] Footer visible on all public pages: shop name, © year, phone number

**Tasks:**
- Create `Pages/Public/Home.razor`
- Implement hero section with Bootstrap
- Implement about section with icon cards
- Optionally fetch categories for featured section
- Implement contact summary
- Create `Components/Layout/Footer.razor` and include in `MainLayout.razor`
- Add placeholder content (owner will replace later)

---

### Story 9.2: Contacts Page

**As** a customer, **I want** to see the shop's contact information **so that** I can reach them by phone, email, or visit in person.

**Acceptance Criteria:**
- [ ] Page at `/contacts` with title "Контакти"
- [ ] **Contact info displayed:**
  - Име: Natural Stone Impex
  - Адрес: (placeholder — owner will provide)
  - Телефон: (placeholder)
  - Имейл: (placeholder)
  - Работно време: (placeholder, e.g., Пон–Пет: 08:00–17:00, Съб: 09:00–13:00)
- [ ] Each piece of info has an icon (Bootstrap Icons or Unicode)
- [ ] Phone number is a clickable `tel:` link
- [ ] Email is a clickable `mailto:` link
- [ ] **Google Maps embed** (optional):
  - Placeholder iframe with Google Maps showing a generic Bulgarian location
  - Owner will replace with actual coordinates later
- [ ] Clean, simple layout — Bootstrap card or two-column layout (info + map)
- [ ] Responsive

**Tasks:**
- Create `Pages/Public/Contacts.razor`
- Layout contact info with Bootstrap grid and icons
- Add clickable tel: and mailto: links
- Add Google Maps iframe placeholder
- Style for mobile and desktop

---

### Story 9.3: Admin Dashboard

**As** the admin, **I want** a dashboard as my landing page after login **so that** I can see a quick overview of the shop status.

**Acceptance Criteria:**
- [ ] Page at `/admin` with title "Табло"
- [ ] **Stat cards** (top row, 4 cards):
  - Общо продукти: {count} (total active products)
  - Чакащи поръчки: {count} (pending orders — highlighted if > 0)
  - Потвърдени поръчки: {count}
  - Завършени поръчки: {count}
- [ ] **Low stock alerts** (section below stats):
  - Title: "Ниска наличност" (Low Stock)
  - List of products with StockQuantity ≤ 10 (threshold)
  - Each row: product name, category, current stock, unit
  - Link to edit the product
  - If no low stock items: "Всички продукти са с достатъчна наличност." message
- [ ] **Recent orders** (section below):
  - Title: "Последни поръчки"
  - Last 5 orders: order number, date, customer, status badge, total
  - "Виж всички поръчки" link → `/admin/orders`
- [ ] All data fetched via API on page load
- [ ] Loading states for each section

**Tasks:**
- Create API endpoint: `GET /api/admin/dashboard` (admin only) returning stats, low stock products, recent orders — OR use existing endpoints (products with low stock filter, orders with limit)
- Create `Pages/Admin/Dashboard.razor`
- Implement stat cards with Bootstrap
- Implement low stock alert list
- Implement recent orders table
- Add loading spinners per section
