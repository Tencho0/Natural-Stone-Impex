# Phase 7: Testing, Bug Fixes & Final Polish — Exact Claude Code Prompts

## Prerequisites

- Phases 1–6 completed and committed
- All features implemented
- Fresh database recommended (drop and recreate with seed data for clean testing):
  ```bash
  dotnet ef database drop --project src/NaturalStoneImpex.Api --force
  dotnet ef database update --project src/NaturalStoneImpex.Api
  ```
- Both projects running
- Fresh Claude Code session

---

## Session 7.1 — Full End-to-End Test (Manual — No Claude Code)

> **This session is entirely manual. YOU test everything. Write down every bug.**
> Open a text file called `bugs.md` and log every issue you find.

### Test 1: Customer Flow — Individual + Pickup

```
1. Open the app in browser (incognito/private window for clean state)
2. Verify landing page loads correctly
   - [ ] Hero section visible with shop name and tagline
   - [ ] "Разгледайте каталога" button works
   - [ ] Categories section shows categories with product counts
   - [ ] Footer visible at bottom

3. Navigate to catalog
   - [ ] Products displayed as cards with images/placeholders
   - [ ] Prices show "XX.XX €" format with "вкл. X.XX € ДДС"
   - [ ] Category sidebar works (click each category)
   - [ ] "Всички категории" resets filter
   - [ ] Search: type "гранит" → filters correctly
   - [ ] Search: clear → all products return
   - [ ] Out-of-stock product shows "Изчерпан" (set one to 0 via admin first)
   - [ ] Pagination works (if enough products)

4. View product detail
   - [ ] Click a product → detail page loads
   - [ ] Breadcrumb: Каталог > {Category} > {Product}
   - [ ] Breadcrumb links work
   - [ ] All 3 prices shown: with ДДС, without ДДС, ДДС amount
   - [ ] Stock status badge correct
   - [ ] Unit displayed correctly (кг or м²)

5. Add to cart and checkout
   - [ ] Set quantity to 3.5 → click "Добави в количката"
   - [ ] Toast appears, cart icon badge shows "1"
   - [ ] Go to catalog, add 2 more different products (click button on card)
   - [ ] Cart icon shows "3"
   - [ ] Click cart icon → cart page
   - [ ] All 3 items visible with correct prices
   - [ ] Change quantity on one item → totals recalculate instantly
   - [ ] Remove one item → item gone, totals update, badge shows "2"
   - [ ] Totals correct: Сума без ДДС + Общо ДДС = Обща сума
   - [ ] Click "Продължи към поръчка"

6. Checkout — Individual + Pickup
   - [ ] Select "Физическо лице"
   - [ ] Name and phone fields appear
   - [ ] Select "Вземане от обекта"
   - [ ] No address field shown
   - [ ] Try submit with empty fields → Bulgarian validation errors
   - [ ] Fill: Име = "Тест Клиент", Телефон = "+359888111222"
   - [ ] Order summary shows correct items and totals
   - [ ] Customer info summary shows entered data
   - [ ] Click "Потвърди поръчката"
   - [ ] Confirmation page: order number displayed (NSI-XXXXXXXX-XXXX)
   - [ ] Cart icon badge empty (0)
   - [ ] "Обратно към каталога" works

WRITE DOWN the order number: _______________
```

### Test 2: Customer Flow — Company + Delivery

```
1. Add products to cart again (2-3 items)
2. Go to checkout
   - [ ] Select "Фирма"
   - [ ] 5 company fields appear
   - [ ] Select "Доставка"
   - [ ] Address field appears
   - [ ] Info message about delivery fee shown
   - [ ] Try invalid ЕИК "123" → error
   - [ ] Fill all fields:
     Фирма: "Тест ЕООД"
     ЕИК: "123456789"
     МОЛ: "Иван Иванов"
     Лице за контакт: "Петър Петров"
     Телефон: "+359899333444"
     Адрес: "ул. Тестова 1, София"
   - [ ] Submit → confirmation page with order number

WRITE DOWN the order number: _______________
```

### Test 3: Admin Flow — Login + Dashboard

```
1. Navigate to /admin
   - [ ] Redirected to /admin/login
   - [ ] Enter wrong password → Bulgarian error message
   - [ ] Enter admin / Admin123! → redirected to dashboard

2. Dashboard
   - [ ] 4 stat cards visible with numbers
   - [ ] Pending orders count matches (should be 2 from tests above)
   - [ ] Recent orders show the 2 orders just placed
   - [ ] Low stock section correct (alert or "sufficient" message)
```

### Test 4: Admin Flow — Category Management

```
1. Navigate to /admin/categories
   - [ ] Categories listed with product counts
   - [ ] Add "Тестова категория" → appears in list
   - [ ] Edit to "Тестова категория 2" → name updates
   - [ ] Delete "Тестова категория 2" → removed
   - [ ] Try deleting a category with products → error in Bulgarian
```

### Test 5: Admin Flow — Product Management

```
1. Navigate to /admin/products
   - [ ] Products listed with correct data
   - [ ] Category filter works
   - [ ] Search works
   - [ ] Stock color coding correct (red ≤10, orange ≤50, green >50)

2. Add new product
   - [ ] Click "Добави продукт" → form page
   - [ ] Category dropdown populated
   - [ ] Enter: Име = "Тестов продукт", Category = any,
     Цена без ДДС = 10, ДДС = 2 → Цена с ДДС auto-fills to 12
   - [ ] Set Цена с ДДС to 15 → validation error (doesn't match)
   - [ ] Fix to 12 → error clears
   - [ ] Unit = кг, Наличност = 50
   - [ ] Upload a JPG image
   - [ ] Save → redirected to list, product appears with thumbnail

3. Edit product
   - [ ] Click edit → form pre-filled
   - [ ] Change price → save → updated
   - [ ] Upload new image → save → image changed

4. Delete product
   - [ ] Click delete → confirmation dialog
   - [ ] Confirm → product gone from list (soft deleted)
```

### Test 6: Admin Flow — Order Processing

```
1. Navigate to /admin/orders
   - [ ] Both test orders visible
   - [ ] Status badges: both "Чакаща" (yellow)
   - [ ] Status filter tabs work
   - [ ] Status summary counts correct

2. Process individual+pickup order (from Test 1):
   - [ ] Click "Виж" → order detail
   - [ ] Customer info correct (name, phone, no address)
   - [ ] Items correct with prices
   - [ ] Totals correct
   - [ ] No delivery fee section (pickup order)
   - NOTE stock quantities of ordered products: ___
   - [ ] Click "Потвърди поръчката" → confirm modal
   - [ ] Confirm → success, status = "Потвърдена"
   - CHECK stock → decreased by ordered amounts: ___  ✓
   - [ ] "Принтирай разписка" button now visible
   - [ ] Click "Маркирай като завършена" → confirm → status = "Завършена"
   - [ ] Only print button remains

3. Process company+delivery order (from Test 2):
   - [ ] Open order detail
   - [ ] Company info shown: Фирма, ЕИК, МОЛ, etc.
   - [ ] Delivery fee section visible (input field)
   - [ ] Enter delivery fee 30.00 → click "Задай"
   - [ ] Grand total increases by 30
   - [ ] Confirm order → stock decremented
   - [ ] Complete order

4. Cancel test:
   - Place a new quick order from the storefront
   - [ ] Open in admin → click "Откажи"
   - [ ] Confirm → status shows "Отказана" (red)
   - [ ] Verify stock NOT changed
   - [ ] No action buttons available

5. Insufficient stock test:
   - Place an order with quantity larger than available stock
   - [ ] Try to confirm → error with product details table
   - [ ] Stock NOT changed
```

### Test 7: Admin Flow — Invoice Management

```
1. Navigate to /admin/invoices
   - [ ] Any previous invoices listed

2. Create new invoice
   - [ ] Click "Нова доставка" → form page
   - [ ] Date defaults to today
   - [ ] Product dropdown shows all active products with category and unit
   - NOTE stock of product you'll add: ___
   - [ ] Fill: Доставчик = "Тест Доставчик", Номер = "ТФ-001"
   - [ ] Add item: select product, quantity 100, price 10.00
   - [ ] Row total shows "1000.00 €"
   - [ ] Click "Добави артикул" → second row appears
   - [ ] Add another item → invoice total updates
   - [ ] Remove second row → total updates
   - [ ] Click "Запази" → confirmation modal with warning
   - [ ] Confirm → redirected to list, success message
   - CHECK stock → increased by 100: ___  ✓

3. View invoice
   - [ ] Click "Виж" on the new invoice
   - [ ] All header info correct
   - [ ] Items table correct
   - [ ] Invoice total correct
   - [ ] Read-only — no edit buttons
   - [ ] Info note about immutability visible
```

### Test 8: Receipt Printing

```
1. Navigate to a completed order → click "Принтирай разписка"
   - [ ] New tab opens with receipt
   - [ ] Header: shop name, address, phone
   - [ ] "СТОКОВА РАЗПИСКА" title with order number and date
   - [ ] Customer info correct
   - [ ] Items table correct with all price columns
   - [ ] Totals correct (ДДС breakdown, delivery fee if applicable)
   - [ ] Signature lines visible
   - [ ] Disclaimer footer visible
   - [ ] Click "Принтирай" → print dialog opens
   - [ ] Print preview: no sidebar, no nav, no buttons, clean receipt
   - [ ] Fits on A4 page
```

### Test 9: Contacts Page

```
1. Navigate to /contacts
   - [ ] Contact info visible (name, address, phone, email, hours)
   - [ ] Phone link clickable
   - [ ] Email link clickable
   - [ ] Google Maps iframe visible
```

### Test 10: Mobile Responsive

```
Open browser dev tools → toggle device toolbar → select iPhone SE (375px):

- [ ] Landing page: all sections stack, readable
- [ ] Catalog: products in single column, category as dropdown (not sidebar)
- [ ] Product detail: image above info
- [ ] Cart: items as cards (not table)
- [ ] Checkout: form usable on mobile
- [ ] Admin (tablet 768px): sidebar visible, tables scrollable
```

### Test 11: Edge Cases

```
- [ ] Visit /products/99999 → "Продуктът не е намерен."
- [ ] Visit /admin/orders/99999 → "Поръчката не е намерена."
- [ ] Visit /admin/invoices/99999 → "Доставката не е намерена."
- [ ] Empty cart → go to /checkout → redirected to /cart
- [ ] Empty cart → /cart shows "Количката ви е празна."
- [ ] Place order with 1 item → confirm → receipt shows 1 row
- [ ] All pages have Bulgarian text (search for any English leftovers)
- [ ] No console errors in browser dev tools during normal usage
```

### Compile Bug List

After all tests, your `bugs.md` should look something like:
```
## Bugs Found

### Critical
- [ ] Stock not decremented on order confirm (if found)
- [ ] Order totals incorrect (if found)

### Major
- [ ] Cart totals don't update when removing items
- [ ] Checkout validation allows empty phone number
- [ ] Receipt shows wrong date

### Minor
- [ ] Product card image stretching on mobile
- [ ] Category filter doesn't reset page to 1
- [ ] English text found on: [page name]
- [ ] Missing loading spinner on: [page name]

### Visual / Polish
- [ ] Dashboard cards not aligned on tablet
- [ ] Receipt table borders too thick
- [ ] Footer overlaps content on short pages
```

---

## Session 7.2 — Bug Fixes (Claude Code)

> **Start a new Claude Code session. Feed it the bug list.**

### Prompt 1 (if 5 or fewer bugs)
```
Read CLAUDE.md and docs/conventions.md.

I've completed testing and found the following bugs. Fix all of them:

[PASTE YOUR BUGS HERE FROM bugs.md]

For each fix:
1. Explain what caused the bug
2. Show me the fix
3. Make sure you don't break anything else

After fixing, list all files you changed.
```

### Prompt 1 (if more than 5 bugs)
```
Read CLAUDE.md and docs/conventions.md.

I've completed testing and found bugs. Let's fix them in order of priority.

CRITICAL BUGS (fix these first):
[paste critical bugs]

Fix these critical bugs now. I'll give you the remaining bugs after.
```

### Prompt 2 (remaining bugs after critical fixes)
```
Critical bugs are fixed. Now fix these remaining issues:

MAJOR BUGS:
[paste major bugs]

MINOR BUGS:
[paste minor bugs]
```

### Prompt 3 (visual/polish bugs)
```
Now fix these visual and polish issues:

[paste visual bugs]
```

### After each fix batch:
```bash
# Test the specific fixes
dotnet build
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
# Verify each bug is fixed
# Make sure nothing else broke
```

### Commit after all bugs fixed:
```bash
git add .
git commit -m "Phase 7: Bug fixes from E2E testing"
```

---

## Session 7.3 — UI Consistency Review (Claude Code)

### Prompt 1
```
Read CLAUDE.md and docs/conventions.md.

Do a UI consistency review across all Blazor pages. Check and fix
the following issues WITHOUT changing any business logic:

1. BULGARIAN TEXT AUDIT:
- Scan every .razor file for any English text in labels, buttons,
  placeholders, error messages, tooltips, or headings
- Replace any English found with correct Bulgarian
- List every English string you find and its replacement

2. LOADING STATES:
- Every page that fetches data must show a loading spinner while loading
- Check all pages in Pages/Public/ and Pages/Admin/
- Use a consistent spinner: Bootstrap spinner-border with text
  "Зареждане..." below it
- List any pages missing loading states

3. ERROR STATES:
- Every page that fetches by ID must handle "not found" (404)
- Check: ProductDetail, OrderDetail, InvoiceDetail, Receipt
- Show consistent message: "{Entity} не е намерен/а." with a link back
- List any pages missing error handling

4. EMPTY STATES:
- Check all list/table pages for empty state messages when no data
- Catalog: "Няма намерени продукти."
- Orders: "Няма поръчки с този статус."
- Invoices: "Няма записани доставки."
- Categories: "Няма категории."
- Cart: "Количката ви е празна."
- Low stock (dashboard): "Всички продукти са с достатъчна наличност."
- List any pages missing empty states

5. CURRENCY FORMAT:
- Search all .razor files for price displays
- ALL must use format "XX.XX €" (space before €, exactly 2 decimal places)
- No naked numbers without € sign
- List any inconsistencies

6. DATE FORMAT:
- Search all .razor files for date displays
- ALL must use DD.MM.YYYY format
- No ISO dates or other formats visible to users
- List any inconsistencies

7. BUTTON CONSISTENCY:
- Primary actions: btn-primary or btn-success
- Cancel/back actions: btn-outline-secondary
- Danger actions (delete, cancel order): btn-outline-danger or btn-danger
- View/detail actions: btn-outline-primary
- All action buttons should have consistent sizing within the same context
- List any inconsistencies

8. FORM VALIDATION:
- Every form must have validation with Bulgarian error messages
- Check: Login, ProductForm, CategoryModal, Checkout, InvoiceForm
- Error messages use Bootstrap is-invalid class
- List any forms missing validation

Report everything you find, fix it, and list all files changed.
```

### Verify
```bash
dotnet build
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Quick spot-check:
- [ ] Random public page → no English text
- [ ] Random admin page → no English text
- [ ] Loading spinner visible when navigating to any page
- [ ] Non-existent product/order/invoice → proper error message
- [ ] Empty category filter → "Няма намерени продукти."
- [ ] Prices all "XX.XX €" format
- [ ] Dates all DD.MM.YYYY format

### Commit
```bash
git add .
git commit -m "Phase 7: UI consistency review — Bulgarian text, loading states, formatting"
```

---

## Session 7.4 — Mobile Responsive Polish (Claude Code)

### Prompt 1
```
Read CLAUDE.md and docs/conventions.md.

Review and fix mobile responsiveness across all public-facing pages.
Test target: 375px width (iPhone SE).

Check and fix these pages:

1. LANDING PAGE (/):
- Hero section text not overflowing
- Feature cards stack vertically (col-12 on mobile)
- Category cards stack vertically
- All text readable, no horizontal scroll

2. CATALOG (/catalog):
- Category filter: must be a dropdown (not sidebar) on mobile
  Use Bootstrap d-none d-md-block on sidebar, d-md-none on dropdown
- Product cards: single column (col-12) on mobile
- Search bar full width
- Pagination compact (fewer page numbers shown)

3. PRODUCT DETAIL (/products/{id}):
- Image stacks ABOVE info (not side by side)
- Prices readable
- Quantity input and button full width

4. CART (/cart):
- Items displayed as CARDS on mobile (not table)
  Use Bootstrap d-none d-md-block on table, d-md-none on cards
- Each card: image, name, price, quantity input, total, remove button
- Totals section full width

5. CHECKOUT (/checkout):
- Form fields full width
- Radio buttons readable
- Order summary table scrollable if needed (table-responsive)

6. CONTACTS (/contacts):
- Two columns stack on mobile
- Map iframe responsive (width: 100%)

7. ADMIN PAGES (768px tablet target):
- Admin sidebar: collapsible on small screens (Bootstrap offcanvas
  or collapse) with a hamburger toggle button
- Tables: wrap in div.table-responsive for horizontal scroll
- Dashboard cards: 2 per row on tablet (col-sm-6), 1 on mobile (col-12)

Only fix responsive issues. Don't change any business logic or
functionality. List all files changed.
```

### Verify
Open browser dev tools → responsive mode:

**375px (iPhone SE):**
- [ ] Landing: no horizontal scroll, everything stacks
- [ ] Catalog: dropdown filter (not sidebar), single column cards
- [ ] Product detail: image above info
- [ ] Cart: cards layout (not table)
- [ ] Checkout: form usable, fields full width
- [ ] Contacts: stacked, map full width

**768px (iPad):**
- [ ] Admin: sidebar accessible (hamburger or collapsed)
- [ ] Admin tables: scrollable horizontally
- [ ] Dashboard: 2 cards per row

**1024px+ (Desktop):**
- [ ] Everything looks normal — no regressions from mobile fixes

### Commit
```bash
git add .
git commit -m "Phase 7: Mobile responsive polish"
```

---

## Session 7.5 — Final Security + Performance Check (Claude Code)

### Prompt 1
```
Read CLAUDE.md and docs/conventions.md.

Do a final security and performance review. Check and fix:

1. API AUTHORIZATION CHECK:
- List every controller action across ALL controllers
- Verify each one has the correct auth:
  - Public (no [Authorize]): GET /api/health, GET /api/categories,
    GET /api/products, GET /api/products/{id}, POST /api/orders
  - Admin ([Authorize]): everything else
- Fix any endpoints with wrong auth level
- List your findings

2. INPUT VALIDATION:
- Check all POST/PUT endpoints have proper validation on request DTOs
- Check all DataAnnotations are present (Required, MaxLength, etc.)
- Check service layer validates business rules before database operations
- Check for SQL injection risk (should be none with EF Core parameterized
  queries, but verify no raw SQL)
- List any missing validation

3. ERROR HANDLING:
- Verify the global exception handling middleware catches unhandled exceptions
- Verify it returns { "error": "Възникна неочаквана грешка." } with HTTP 500
- Verify it does NOT leak stack traces or internal details to the client
- Verify all service methods have try-catch where appropriate

4. CORS CHECK:
- Verify CORS only allows the Blazor client origin
- Verify it's not set to AllowAnyOrigin in production configuration

5. IMAGE UPLOAD SECURITY:
- Verify file type validation (only JPG/PNG)
- Verify file size limit (5MB)
- Verify uploaded files are saved with safe filenames (no path traversal)
- Verify the uploads directory exists and is served correctly

6. PERFORMANCE QUICK WINS:
- Check for N+1 query problems: any place where we loop and make
  individual DB calls instead of using Include() or batch queries
- Check that product list endpoint uses .AsNoTracking() for read-only queries
- Check that pagination is done at the database level (Skip/Take), not
  in memory
- List any N+1 or performance issues found and fix them

Report everything. Fix all issues. List all files changed.
```

### Verify
```bash
dotnet build
dotnet run --project src/NaturalStoneImpex.Api
```

Quick security tests via Swagger:
- [ ] Call admin endpoint without token → 401
- [ ] POST /api/products with missing required field → 400 (not 500)
- [ ] POST /api/products/{id}/image with .exe file → 400 rejection
- [ ] GET /api/products works without token
- [ ] POST /api/orders works without token

### Commit
```bash
git add .
git commit -m "Phase 7: Security and performance review"
```

---

## Session 7.6 — Final Regression Test (Manual — No Claude Code)

> **One last manual pass to verify nothing broke during Sessions 7.2–7.5.**
> This should be quick — 15-20 minutes.

```
Quick regression checklist:

CUSTOMER FLOW:
- [ ] Landing page loads correctly
- [ ] Catalog: add product to cart from card
- [ ] Product detail: add to cart with custom quantity
- [ ] Cart: items correct, change quantity, remove item
- [ ] Checkout as individual + pickup → order confirmed
- [ ] Checkout as company + delivery → order confirmed
- [ ] Confirmation page shows order number
- [ ] Contacts page loads

ADMIN FLOW:
- [ ] Login works
- [ ] Dashboard loads with stats
- [ ] Add a category, edit it, delete it
- [ ] Add a product with image, edit it, delete it
- [ ] View order list, filter by status
- [ ] Set delivery fee on a delivery order
- [ ] Confirm an order → stock decreases
- [ ] Complete an order
- [ ] Print receipt → clean A4 output in print preview
- [ ] Create an invoice → stock increases
- [ ] View invoice detail
- [ ] Logout works

EDGE CASES:
- [ ] Invalid URLs show proper error messages
- [ ] Empty states show proper messages
- [ ] No console errors in browser dev tools
- [ ] No English text anywhere in the UI
```

### If bugs found:
```bash
# Quick fix session in Claude Code:
"Fix this specific issue: [describe bug]. Don't change anything else."
```

### Final Commit
```bash
git add .
git commit -m "Phase 7 complete: All tests passed, ready for deployment"
git tag v1.0.0 -m "Version 1.0.0 — Feature complete"
```

---

## Phase 7 Complete ✅

Your application is now:
- ✅ Fully tested end-to-end
- ✅ All bugs fixed
- ✅ UI consistent (Bulgarian text, formatting, loading states)
- ✅ Mobile responsive
- ✅ Security reviewed
- ✅ Performance checked
- ✅ Tagged as v1.0.0

Update planning/overview.md one final time — all statuses ✅.

```bash
git add planning/overview.md
git commit -m "Final planning status update: v1.0.0 complete"
```

---

## What's Next

### Deployment (Phase 8)
When you decide on hosting, start a Claude Code session:
```
"Read CLAUDE.md. I want to deploy this application to [hosting choice].
The API and Blazor client are separate projects. Create docs/deployment.md
with step-by-step instructions, and configure the projects for production
deployment (production appsettings, environment variables for connection
string and JWT key, etc.)"
```

### After Launch — Ongoing Maintenance
When you want to add features or fix bugs after deployment:
1. Always start from the latest committed code
2. Create a branch: `git checkout -b feature/feature-name`
3. Use Claude Code with the same workflow (read CLAUDE.md + relevant docs)
4. Test, commit, merge to main
5. Deploy

### Quick Reference — Your Complete Docs
```
CLAUDE.md                              → AI coding conventions
docs/product-requirements.md           → Business requirements
docs/technical-specification.md        → Technical spec
docs/database-schema.md                → Database design
docs/api-endpoints.md                  → API contract
docs/conventions.md                    → Strict rules
planning/overview.md                   → Epic status tracker
planning/epics/01-10                   → All stories and acceptance criteria
```

Congratulations — you built a complete inventory and order management system. 🎉
