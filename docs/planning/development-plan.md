# Development Plan — Natural Stone Impex

## Phase 0: Preparation (You Are Here ✅)

Everything done in this conversation — no coding yet.

| Task | Status | Output |
|------|--------|--------|
| Clarify business requirements | ✅ Done | Captured in conversation |
| Write Product Requirements (PRD) | ✅ Done | `docs/product-requirements.md` |
| Write Technical Specification | ✅ Done | `docs/technical-specification.md` |
| Write Database Schema | ✅ Done | `docs/database-schema.md` |
| Write API Endpoints | ✅ Done | `docs/api-endpoints.md` |
| Write Conventions / Strict Rules | ✅ Done | `docs/conventions.md` |
| Write CLAUDE.md | ✅ Done | `CLAUDE.md` |
| Write README.md | ✅ Done | `README.md` |
| Write Planning Overview | ✅ Done | `planning/overview.md` |
| Write all 10 Epics with Stories | ✅ Done | `planning/epics/01–10` |

**Action now**: Create the repo, copy all these files in, make your first git commit.

```bash
git init NaturalStoneImpex
cd NaturalStoneImpex
# Copy all docs, planning files, CLAUDE.md, README.md
git add .
git commit -m "Phase 0: Project documentation and planning"
```

---

## Phase 1: Foundation (Epic 01 + 02)
**Goal**: Both projects running, database connected, admin can log in.
**Estimated time**: 1 day

### Session 1.1 — Project Scaffolding (Epic 01, Stories 1.1–1.3)
```
Prompt: "Read docs/conventions.md and planning/epics/01-project-setup.md.
Implement Stories 1.1, 1.2, and 1.3 — create the solution, configure API
and Blazor client projects."
```
**Verify**:
- [ ] `dotnet build` succeeds
- [ ] API starts and returns `/api/health`
- [ ] Blazor client starts and shows navigation
- [ ] Both projects run simultaneously

```bash
git commit -m "Epic 01: Stories 1.1-1.3 — Project scaffolding"
```

### Session 1.2 — Database + E2E (Epic 01, Stories 1.4–1.5)
```
Prompt: "Read planning/epics/01-project-setup.md. Implement Stories 1.4
and 1.5 — initial migration and end-to-end verification."
```
**Verify**:
- [ ] Database created
- [ ] Blazor client can call API (no CORS errors)

```bash
git commit -m "Epic 01: Stories 1.4-1.5 — Database and E2E verification"
```

### Session 1.3 — Authentication (Epic 02, All Stories)
```
Prompt: "Read docs/conventions.md, docs/database-schema.md, and
planning/epics/02-authentication.md. Implement all stories for
Epic 02 — AdminUser entity, login endpoint, Blazor auth state,
login page, and route protection."
```
**Verify**:
- [ ] Admin user seeded in database
- [ ] Login page works at `/admin/login`
- [ ] JWT token returned on valid login
- [ ] Admin pages protected (redirect to login if not authenticated)
- [ ] Logout works

```bash
git commit -m "Epic 02: Authentication — login, JWT, route protection"
```

---

## Phase 2: Admin CRUD (Epic 03 + 04)
**Goal**: Admin can manage categories and products with images.
**Estimated time**: 1–2 days

### Session 2.1 — Categories (Epic 03, All Stories)
```
Prompt: "Read docs/database-schema.md, docs/api-endpoints.md, and
planning/epics/03-category-management.md. Implement all stories —
entity, API endpoints, and admin page."
```
**Verify**:
- [ ] Category entity and seed data in database
- [ ] All 4 API endpoints work (GET, POST, PUT, DELETE)
- [ ] Admin page shows categories, can add/edit/delete
- [ ] Cannot delete category with products (error shown)

```bash
git commit -m "Epic 03: Category management — full CRUD"
```

### Session 2.2 — Product Entity + API (Epic 04, Stories 4.1–4.2)
```
Prompt: "Read docs/database-schema.md, docs/api-endpoints.md, and
planning/epics/04-product-management.md. Implement Stories 4.1 and
4.2 — Product entity with migration, seed data, and all API endpoints
including image upload."
```
**Verify**:
- [ ] Product table created with correct columns
- [ ] Seed products exist
- [ ] All API endpoints work via Swagger
- [ ] Image upload saves file and returns path

```bash
git commit -m "Epic 04: Stories 4.1-4.2 — Product entity and API"
```

### Session 2.3 — Product Admin Pages (Epic 04, Stories 4.3–4.4)
```
Prompt: "Read planning/epics/04-product-management.md. Implement
Stories 4.3 and 4.4 — product list page and product form (add/edit)
with image upload and VAT auto-calculation."
```
**Verify**:
- [ ] Product list shows all products with filters and pagination
- [ ] Add product form works with VAT auto-calculation
- [ ] Edit product form pre-fills correctly
- [ ] Image upload with preview works
- [ ] Soft delete works

```bash
git commit -m "Epic 04: Stories 4.3-4.4 — Product admin pages"
```

---

## Phase 3: Customer Storefront (Epic 05 + 06)
**Goal**: Customers can browse, add to cart, and place orders.
**Estimated time**: 2–3 days

### Session 3.1 — Public Catalog (Epic 05, All Stories)
```
Prompt: "Read planning/epics/05-public-catalog.md and
docs/api-endpoints.md. Implement all stories — catalog page with
category filter, search, pagination, and product detail page."
```
**Verify**:
- [ ] Catalog shows products as cards with prices
- [ ] Category filter works
- [ ] Search works
- [ ] Pagination works
- [ ] Product detail shows all price fields
- [ ] Out-of-stock products show "Изчерпан"

```bash
git commit -m "Epic 05: Public catalog and product detail"
```

### Session 3.2 — Cart (Epic 06, Stories 6.1–6.4)
```
Prompt: "Read planning/epics/06-cart-and-checkout.md. Implement
Stories 6.1 through 6.4 — cart service, cart icon, catalog integration,
and cart page."
```
**Verify**:
- [ ] Adding products from catalog works
- [ ] Cart icon shows item count
- [ ] Cart page shows items with editable quantities
- [ ] Totals calculate correctly
- [ ] Remove item works

```bash
git commit -m "Epic 06: Stories 6.1-6.4 — Cart functionality"
```

### Session 3.3 — Checkout (Epic 06, Stories 6.5–6.7)
```
Prompt: "Read planning/epics/06-cart-and-checkout.md,
docs/database-schema.md, and docs/api-endpoints.md. Implement
Stories 6.5, 6.6, and 6.7 — checkout page, order API endpoint,
and confirmation page. This includes creating the Order,
OrderCustomerInfo, and OrderItem entities and migration."
```
**Verify**:
- [ ] Customer type selection works (individual vs company)
- [ ] Correct form fields shown per type
- [ ] Delivery method selection works
- [ ] Form validation works with Bulgarian messages
- [ ] Order saved in database with correct data
- [ ] Order number generated correctly (NSI-YYYYMMDD-XXXX)
- [ ] Confirmation page shows order number
- [ ] Cart cleared after order

```bash
git commit -m "Epic 06: Stories 6.5-6.7 — Checkout and order placement"
```

---

## Phase 4: Order Processing (Epic 07)
**Goal**: Admin can view, confirm, complete, and cancel orders.
**Estimated time**: 1–2 days

### Session 4.1 — Order API (Epic 07, Story 7.1)
```
Prompt: "Read docs/api-endpoints.md and planning/epics/07-order-management.md.
Implement Story 7.1 — all order management API endpoints (list, detail,
confirm with stock decrement, complete, cancel, set delivery fee)."
```
**Verify**:
- [ ] All endpoints work via Swagger
- [ ] Confirm decrements stock correctly
- [ ] Confirm fails with insufficient stock (error lists products)
- [ ] Cancel only works for pending orders
- [ ] Complete only works for confirmed orders

```bash
git commit -m "Epic 07: Story 7.1 — Order management API"
```

### Session 4.2 — Order Admin Pages (Epic 07, Stories 7.2–7.3)
```
Prompt: "Read planning/epics/07-order-management.md. Implement
Stories 7.2 and 7.3 — order list page with status filter and
order detail page with all action buttons."
```
**Verify**:
- [ ] Order list shows all orders with status badges
- [ ] Status filter works
- [ ] Order detail shows customer info, items, totals
- [ ] Delivery fee input and save works
- [ ] Confirm button works (stock decremented)
- [ ] Complete button works
- [ ] Cancel button works
- [ ] Error messages shown for insufficient stock

```bash
git commit -m "Epic 07: Stories 7.2-7.3 — Order admin pages"
```

---

## Phase 5: Invoices (Epic 08)
**Goal**: Admin can record deliveries and stock is auto-updated.
**Estimated time**: 1–2 days

### Session 5.1 — Invoice Entity + API (Epic 08, Stories 8.1–8.2)
```
Prompt: "Read docs/database-schema.md, docs/api-endpoints.md, and
planning/epics/08-invoice-management.md. Implement Stories 8.1 and
8.2 — Invoice entities, migration, and all API endpoints."
```
**Verify**:
- [ ] Invoice tables created
- [ ] POST creates invoice and increments stock
- [ ] GET list and detail return correct data
- [ ] Stock quantities updated in Products table

```bash
git commit -m "Epic 08: Stories 8.1-8.2 — Invoice entity and API"
```

### Session 5.2 — Invoice Admin Pages (Epic 08, Stories 8.3–8.5)
```
Prompt: "Read planning/epics/08-invoice-management.md. Implement
Stories 8.3, 8.4, and 8.5 — invoice list, new invoice form with
dynamic rows, and invoice detail view."
```
**Verify**:
- [ ] Invoice list shows all deliveries
- [ ] New invoice form: dynamic item rows work (add/remove)
- [ ] Product dropdown shows correct products
- [ ] Save creates invoice and updates stock
- [ ] Success message confirmed
- [ ] Invoice detail shows all data (read-only)

```bash
git commit -m "Epic 08: Stories 8.3-8.5 — Invoice admin pages"
```

---

## Phase 6: Polish (Epic 09 + 10)
**Goal**: Landing page, contacts, dashboard, and receipt printing.
**Estimated time**: 1–2 days

### Session 6.1 — Landing + Contacts + Dashboard (Epic 09, All Stories)
```
Prompt: "Read planning/epics/09-landing-and-contacts.md and
docs/api-endpoints.md. Implement all stories — home page, contacts
page, and admin dashboard with stats and low stock alerts."
```
**Verify**:
- [ ] Home page looks professional with hero section
- [ ] Contacts page shows shop info
- [ ] Dashboard shows correct counts
- [ ] Low stock alerts appear for products ≤ 10 quantity
- [ ] Recent orders shown

```bash
git commit -m "Epic 09: Landing page, contacts, admin dashboard"
```

### Session 6.2 — Receipt Printing (Epic 10, All Stories)
```
Prompt: "Read planning/epics/10-receipt-printing.md. Implement all
stories — receipt layout, print via JS interop, and print button
on order detail page."
```
**Verify**:
- [ ] Receipt page renders correctly with all order data
- [ ] Print button triggers browser print dialog
- [ ] Printed output is clean (no navigation, no colors)
- [ ] Fits on A4 paper
- [ ] Print button appears only for confirmed/completed orders

```bash
git commit -m "Epic 10: Receipt printing"
```

---

## Phase 7: Testing & Final Polish
**Goal**: Everything works end-to-end, no bugs, ready for deployment.
**Estimated time**: 1–2 days

### Session 7.1 — Full End-to-End Test
Manually test the entire flow:

**Customer flow:**
- [ ] Visit home page → navigate to catalog
- [ ] Filter by category, search by name
- [ ] View product detail (prices correct)
- [ ] Add multiple products to cart
- [ ] View cart, adjust quantities, remove an item
- [ ] Checkout as Физическо лице with delivery
- [ ] Checkout as Фирма with pickup
- [ ] See confirmation page with order number

**Admin flow:**
- [ ] Login
- [ ] Dashboard shows correct stats
- [ ] Manage categories (add, edit, delete)
- [ ] Manage products (add with image, edit, soft-delete)
- [ ] View incoming orders
- [ ] Set delivery fee on an order
- [ ] Confirm order → verify stock decremented
- [ ] Try confirming with insufficient stock → verify error
- [ ] Complete order
- [ ] Print receipt → verify A4 layout
- [ ] Enter new invoice → verify stock incremented
- [ ] View invoice detail
- [ ] Cancel a pending order

### Session 7.2 — Bug Fixes
```
Prompt: "Here are the issues I found during testing: [list bugs].
Fix all of them."
```

### Session 7.3 — UI Polish
```
Prompt: "Review all pages for visual consistency. Ensure:
- All Bulgarian text is correct (no typos, no English)
- Loading spinners on all async operations
- Error messages shown properly
- Mobile responsive on catalog and cart pages
- Consistent button styles across admin pages
- Footer on all public pages"
```

```bash
git commit -m "Phase 7: Bug fixes and UI polish"
```

---

## Phase 8: Deployment (When Ready)
**Goal**: App live on the internet.

This phase depends on your hosting choice. We'll write `docs/deployment.md` when you decide.

Options to consider:
- **Azure App Service** — native .NET support, easy SQL Server
- **Railway** — simple deploy, supports .NET + PostgreSQL (would need DB switch)
- **VPS (DigitalOcean, Hetzner)** — full control, cheapest long-term
- **IIS on Windows Server** — traditional, if you already have a server

---

## Timeline Summary

| Phase | Epic(s) | What | Estimated Time |
|-------|---------|------|----------------|
| 0 | — | Documentation & planning | ✅ Done |
| 1 | 01 + 02 | Foundation + Auth | 1 day |
| 2 | 03 + 04 | Category + Product CRUD | 1–2 days |
| 3 | 05 + 06 | Catalog + Cart + Checkout | 2–3 days |
| 4 | 07 | Order management | 1–2 days |
| 5 | 08 | Invoice management | 1–2 days |
| 6 | 09 + 10 | Polish + Receipt | 1–2 days |
| 7 | — | Testing + Bug fixes | 1–2 days |
| 8 | — | Deployment | 0.5–1 day |
| **Total** | | | **~9–15 days** |

## Claude Code Session Rules

1. **One session = one epic (or less)**. Never mix epics in one session.
2. **Start every session** with: "Read docs/conventions.md and planning/epics/XX.md"
3. **Ask Claude Code to plan before coding** for complex stories.
4. **Test after every session** before committing.
5. **Commit after every session** with a descriptive message.
6. **Start a new session** if Claude Code starts producing inconsistent code.
7. **Update planning/overview.md** status after each epic.
