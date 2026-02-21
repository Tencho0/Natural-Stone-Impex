# Phase 6: Polish — Landing, Contacts, Dashboard, Receipt — Exact Claude Code Prompts

## Prerequisites

- Phases 1–5 completed and committed
- All core features working (catalog, cart, checkout, orders, invoices)
- Some orders in the database (mix of pending, confirmed, completed, cancelled)
- Some invoices in the database
- Some products with low stock (set 1-2 products to stock ≤ 10 via admin to test alerts)
- Fresh Claude Code session

---

## Session 6.1 — Landing Page (Epic 09, Story 9.1)

### Prompt 1
```
Read docs/conventions.md and planning/epics/09-landing-and-contacts.md.

Implement Story 9.1 — Landing / Home Page.

Page at / (root route):
- Public, no auth, uses MainLayout
- This is the first thing customers see — it should look professional

Layout sections:

**1. Hero Section:**
- Full-width section with a solid background color (Bootstrap bg-dark
  text-white or bg-primary — something professional for a building
  materials shop)
- Shop name: "Natural Stone Impex" (h1, large, bold)
- Tagline: "Качествени строителни материали" (h4, lighter weight)
- Subtitle: "Натурален камък, цимент, плочки и още" (p, text-muted or lighter)
- Call-to-action button: "Разгледайте каталога" (btn-warning btn-lg or
  btn-light btn-lg) → links to /catalog
- Generous padding: py-5 or larger

**2. Features Section (3 cards in a row):**
- Section title: "Защо да изберете нас" (h3, centered, mb-4)
- 3 Bootstrap cards in a row (col-md-4), centered icons/emoji:
  - Card 1: Icon ✓ or 🏗️, Title: "Качествени материали",
    Text: "Предлагаме само проверени и висококачествени строителни материали."
  - Card 2: Icon 💰, Title: "Конкурентни цени",
    Text: "Най-добрите цени на пазара с възможност за доставка."
  - Card 3: Icon 🚚, Title: "Бърза доставка",
    Text: "Доставяме до вашия обект в най-кратък срок."
- Cards with text-center, padding, subtle border or shadow

**3. Categories Section (dynamic):**
- Section title: "Нашите категории" (h3, centered, mb-4)
- Fetch categories from GET /api/categories
- Display as clickable Bootstrap cards in a responsive row
- Each card: category name (h5), product count ("12 продукта"),
  links to /catalog?categoryId={id}
- If no categories loaded, skip this section

**4. Contact Summary Section:**
- Light background (bg-light)
- Section title: "Свържете се с нас" (h3, centered)
- Row with: Phone placeholder, Address placeholder, Working hours placeholder
  (use Unicode icons: 📞, 📍, 🕐)
- "Към контакти" button (btn-outline-primary) → links to /contacts

**5. Footer (reusable component):**
- Create Components/Layout/Footer.razor
- Include in MainLayout.razor at the bottom
- Content: "© 2026 Natural Stone Impex. Всички права запазени."
- Phone number placeholder
- Dark background (bg-dark text-light), padding, centered text

Remove the health check test component from Home.razor if it's still
there from Phase 1.

All text in Bulgarian. Bootstrap 5 only. No custom CSS.
Mobile responsive — all sections stack vertically on small screens.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Navigate to / → landing page loads
- [ ] Hero section: shop name, tagline, CTA button visible
- [ ] "Разгледайте каталога" button → navigates to /catalog
- [ ] Features section: 3 cards visible with Bulgarian text
- [ ] Categories section: shows categories fetched from API with product counts
- [ ] Clicking a category card → navigates to /catalog?categoryId={id}
- [ ] Contact summary section visible with placeholder info
- [ ] "Към контакти" button → navigates to /contacts
- [ ] Footer visible at bottom with © text
- [ ] Footer appears on ALL public pages (catalog, product detail, cart, checkout, contacts)
- [ ] Mobile responsive: check at 375px — everything stacks, readable
- [ ] No health check component leftover from Phase 1
- [ ] Page looks professional — not just raw unstyled HTML

### Commit
```bash
git add .
git commit -m "Epic 09: Story 9.1 — Landing page with hero, features, categories, footer"
```

---

## Session 6.2 — Contacts Page (Epic 09, Story 9.2)

### Prompt 1
```
Read docs/conventions.md and planning/epics/09-landing-and-contacts.md.

Implement Story 9.2 — Contacts Page.

Page at /contacts:
- Public, no auth, uses MainLayout
- Page title: "Контакти" (h2)

Layout — two-column on desktop, stacked on mobile:

**Left column (col-md-6) — Contact Information:**
- Bootstrap card with body:
  - Heading: "Информация за контакт" (h5)
  - Each line with icon + text:
    - 🏢 **Фирма:** Natural Stone Impex
    - 📍 **Адрес:** [Placeholder — ул. Примерна 1, София]
    - 📞 **Телефон:** [Placeholder — +359 888 123 456]
      - Phone is a clickable link: <a href="tel:+359888123456">
    - 📧 **Имейл:** [Placeholder — info@naturalstonimpex.bg]
      - Email is a clickable link: <a href="mailto:info@...">
    - 🕐 **Работно време:**
      - Понеделник – Петък: 08:00 – 17:00
      - Събота: 09:00 – 13:00
      - Неделя: Почивен ден

  Use Bootstrap list-unstyled or dl/dt/dd for clean formatting.
  Bold the labels, regular weight for values.

**Right column (col-md-6) — Map:**
- Bootstrap card with body:
  - Heading: "Къде се намираме" (h5)
  - Google Maps iframe embed:
    ```html
    <iframe
      src="https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d2932.5!2d23.3219!3d42.6977!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x0%3A0x0!2zNDLCsDQxJzUyLjAiTiAyM8KwMTknMTguOCJF!5e0!3m2!1sen!2sbg!4v1234567890"
      width="100%" height="350" style="border:0;" allowfullscreen=""
      loading="lazy" referrerpolicy="no-referrer-when-downgrade">
    </iframe>
    ```
  - Note: This is a placeholder centered on Sofia. The shop owner will
    replace the embed URL with their actual location.

**Below both columns — Call to Action:**
- Centered text: "Имате въпроси? Не се колебайте да се свържете с нас!"
- "Обратно към каталога" button (btn-primary) → /catalog

All text in Bulgarian. Bootstrap 5 only. Mobile: columns stack.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Navigate to /contacts → page loads
- [ ] Contact info card: shop name, address, phone, email, working hours visible
- [ ] Phone number is clickable (tel: link)
- [ ] Email is clickable (mailto: link)
- [ ] Working hours formatted correctly (Mon-Fri, Sat, Sun)
- [ ] Google Maps iframe visible (shows Sofia area)
- [ ] "Обратно към каталога" button works
- [ ] Mobile responsive: cards stack vertically
- [ ] Can reach this page from public nav "Контакти" link
- [ ] Can reach from landing page "Към контакти" button

### Commit
```bash
git add .
git commit -m "Epic 09: Story 9.2 — Contacts page with info and map"
```

---

## Session 6.3 — Admin Dashboard (Epic 09, Story 9.3)

### Prompt 1
```
Read docs/conventions.md, docs/api-endpoints.md (GET /api/orders/stats,
GET /api/orders/recent, GET /api/products/low-stock), and
planning/epics/09-landing-and-contacts.md.

Implement Story 9.3 — Admin Dashboard.

Page at /admin (replaces placeholder):
- Page title: "Табло"
- @attribute [Authorize]
- This is the admin landing page after login

Fetches data from 3 API endpoints on load:
1. GET /api/orders/stats → stats counts
2. GET /api/orders/recent?count=5 → recent orders
3. GET /api/products/low-stock?threshold=10 → low stock alerts

Layout:

**Row 1 — Stat Cards (4 cards in a row, col-md-3 each):**

Card 1:
- Label: "Общо продукти"
- Value: {TotalProducts} (large number, h3)
- Icon/accent: bg-primary text-white or border-start border-primary
- Subtle background or left border color

Card 2:
- Label: "Чакащи поръчки"
- Value: {PendingOrders}
- Accent: bg-warning or border-warning
- If PendingOrders > 0: value text-warning, bold (highlight urgency)

Card 3:
- Label: "Потвърдени поръчки"
- Value: {ConfirmedOrders}
- Accent: bg-info or border-info

Card 4:
- Label: "Завършени поръчки"
- Value: {CompletedOrders}
- Accent: bg-success or border-success

Each card is a Bootstrap card with padding, clean design. The number
should be prominently displayed.

**Row 2 — Two columns:**

**Left column (col-md-7) — "Последни поръчки" (Recent Orders):**
- Section title: "Последни поръчки" (h5)
- Table with last 5 orders:
  - Columns: Номер, Дата, Клиент, Статус, Сума
  - Dates formatted DD.MM.YYYY
  - Status with color badges (same as order list page)
  - Prices formatted "XX.XX €"
  - Each row clickable → navigates to /admin/orders/{id}
- "Виж всички поръчки" link below → /admin/orders
- If no orders: "Няма поръчки."

**Right column (col-md-5) — "Ниска наличност" (Low Stock Alerts):**
- Section title: "Ниска наличност" (h5)
- If products with stock ≤ 10 exist:
  - List/table of low stock products:
    - Columns: Продукт, Категория, Наличност, Мерна ед.
    - Stock quantity in RED text (text-danger)
    - Each product name is a link → /admin/products/{id}/edit
  - Bootstrap alert-warning icon/header for visibility
- If no low stock products:
  - Bootstrap alert-success:
    "✓ Всички продукти са с достатъчна наличност."

**Loading states:**
- Each section loads independently (don't block the whole page)
- Show individual spinner per section while loading
- If any API call fails, show error in that section only

All text in Bulgarian. Bootstrap 5 only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

**Setup**: Before testing, make sure you have:
- At least 1-2 products with stock ≤ 10 (edit via admin products page)
- Orders in various statuses (pending, confirmed, completed)

Test in browser:
- [ ] Login → arrives at /admin → dashboard loads
- [ ] 4 stat cards visible with correct counts
- [ ] Stat counts match actual data in database
- [ ] Pending orders count highlighted if > 0
- [ ] Recent orders table shows last 5 orders
- [ ] Order dates formatted DD.MM.YYYY
- [ ] Status badges correct colors
- [ ] Clicking an order row → navigates to order detail
- [ ] "Виж всички поръчки" → navigates to /admin/orders
- [ ] Low stock section shows products with stock ≤ 10
- [ ] Stock amounts in red text
- [ ] Product names are clickable links to edit page
- [ ] If all products have stock > 10: green success message shown instead
- [ ] Each section loads independently (spinners per section)
- [ ] Mobile responsive: cards stack, columns stack

### Commit
```bash
git add .
git commit -m "Epic 09: Story 9.3 — Admin dashboard with stats, recent orders, low stock alerts"
```

---

## Session 6.4 — Receipt Layout (Epic 10, Story 10.1)

### Prompt 1
```
Read docs/conventions.md, docs/technical-specification.md (Receipt section
under 5.5 Order Management), and planning/epics/10-receipt-printing.md.

Implement Story 10.1 — Receipt Component/Page.

Page at /admin/orders/{id:int}/receipt:
- @attribute [Authorize]
- Fetches order detail from GET /api/orders/{id} on load
- This page is designed ENTIRELY for printing — minimal screen chrome

Layout — designed for A4 portrait paper:

**Header:**
- Shop name centered: "NATURAL STONE IMPEX" (bold, larger font, uppercase)
- Address below: "[Placeholder address]" (centered, normal weight)
- Phone below: "[Placeholder phone]" (centered)
- Horizontal line (hr) divider

**Document Title:**
- "СТОКОВА РАЗПИСКА" (centered, bold, h4, uppercase)
- "№ {OrderNumber}" (centered, below title)
- "Дата: {DD.MM.YYYY}" (centered — use ConfirmedAt date if available,
  else CreatedAt)

**Spacer (1 blank line)**

**Customer Info:**
- If Individual:
  - "Клиент: {FullName}"
  - "Телефон: {Phone}"
  - "Адрес: {Address}" (only if delivery, only if address exists)
- If Company:
  - "Фирма: {CompanyName}"
  - "ЕИК: {Eik}"
  - "МОЛ: {Mol}"
  - "Лице за контакт: {ContactPerson}"
  - "Телефон: {ContactPhone}"
  - "Адрес: {Address}" (only if delivery)
- "Метод: Вземане от обекта" or "Метод: Доставка"

**Spacer**

**Items Table:**
- Simple HTML table with thin borders (border: 1px solid #000):
  | № | Продукт | Мерна ед. | Количество | Ед. цена без ДДС | ДДС | Ед. цена с ДДС | Общо с ДДС |
  - Row for each order item
  - All prices formatted "XX.XX €"
  - Right-align all number columns
  - Header row bold, centered

**Totals Section (right-aligned below table):**
- "Сума без ДДС: {SubtotalWithoutVat} €"
- "Общо ДДС: {TotalVat} €"
- If delivery fee > 0: "Цена за доставка: {DeliveryFee} €"
- Separator line
- "**Обща сума: {GrandTotal} €**" (bold, slightly larger)

**Spacer (2 blank lines)**

**Signature Lines:**
- Two columns:
  - Left: "Предал: ___________________"
  - Right: "Приел: ___________________"

**Footer:**
- Blank line
- Centered, italic, smaller font:
  "Стокова разписка — не е официален данъчен документ."

**CRITICAL — Styling:**
- ALL styling must be in a <style> block inside the component (not external CSS)
- Use inline or scoped styles that work for BOTH screen and print
- Font: serif font family for professional document look
  (e.g., "Georgia, 'Times New Roman', serif")
- Colors: black text on white background ONLY
- Table: thin black borders, no background colors
- Max width: 800px, centered on screen
- Padding/margins suitable for A4 printing

**Screen-only elements (hidden when printing):**
- "Принтирай" button at top
- "Обратно към поръчката" link at top
- These will be hidden via print CSS in the next session

**Error states:**
- If order not found: "Поръчката не е намерена."
- If order is still Pending (not confirmed): "Разписка може да се
  генерира само за потвърдени или завършени поръчки."
- Loading spinner while data loads

All text in Bulgarian.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Navigate to /admin/orders/{id}/receipt for a CONFIRMED order
- [ ] Header: shop name, address, phone centered
- [ ] Document title: "СТОКОВА РАЗПИСКА" with order number and date
- [ ] Customer info displayed correctly based on type
- [ ] Items table: all columns present, prices formatted, numbers right-aligned
- [ ] Totals section: SubtotalWithoutVat, TotalVat, DeliveryFee (if applicable), GrandTotal
- [ ] Signature lines visible
- [ ] Footer disclaimer visible
- [ ] Professional document look (serif font, clean layout)
- [ ] "Принтирай" button visible at top
- [ ] "Обратно към поръчката" link visible
- [ ] Test with a PENDING order → shows error message
- [ ] Test with non-existent order ID → "Поръчката не е намерена."

**Test both customer types:**
- [ ] Individual customer order receipt → shows Клиент, Телефон
- [ ] Company customer order receipt → shows Фирма, ЕИК, МОЛ, Лице за контакт

### Commit
```bash
git add .
git commit -m "Epic 10: Story 10.1 — Receipt component with full layout"
```

---

## Session 6.5 — Print Functionality + Integration (Epic 10, Stories 10.2 + 10.3)

### Prompt 1
```
Read docs/conventions.md and planning/epics/10-receipt-printing.md.

Implement Stories 10.2 and 10.3 — Print functionality and print button
on order detail page.

Story 10.2 — Print via JS Interop:

1. Create a JavaScript function for printing:
- Add to wwwroot/js/print.js (or inline in index.html):
  ```javascript
  window.printPage = function() {
    window.print();
  }
  ```
- Reference the script in wwwroot/index.html before closing </body>

2. Add print CSS to the Receipt page (<style> block). Add @media print rules:

@media print {
  /* Hide everything except receipt content */
  .no-print {
    display: none !important;
  }

  /* Hide Blazor nav, admin sidebar, any layout chrome */
  .sidebar, .navbar, nav, header, footer {
    display: none !important;
  }

  /* Page settings for A4 */
  @page {
    size: A4 portrait;
    margin: 15mm;
  }

  /* Ensure receipt takes full width when printing */
  .receipt-container {
    max-width: 100% !important;
    margin: 0 !important;
    padding: 0 !important;
  }

  /* Prevent table from breaking across pages */
  table {
    page-break-inside: avoid;
  }

  /* No background colors in print */
  * {
    background: white !important;
    color: black !important;
  }

  body {
    font-size: 12pt;
  }
}

3. On the Receipt page:
- Wrap "Принтирай" button and "Обратно към поръчката" link in a
  <div class="no-print"> so they're hidden when printing
- "Принтирай" button: on click, call JS interop:
  await JSRuntime.InvokeVoidAsync("printPage")
- Inject IJSRuntime in the component
- Button styled: btn-primary, with printer icon (🖨️ or Unicode)
- "Обратно към поръчката" → navigates to /admin/orders/{id}

4. Wrap the receipt content in <div class="receipt-container">

Story 10.3 — Print Button on Order Detail Page:

5. In Pages/Admin/OrderDetail.razor:
- Add "Принтирай разписка" button
- Only visible when order Status is Confirmed (1) or Completed (2)
  AND NOT cancelled
- Button: btn-outline-primary with 🖨️ icon
- On click: open /admin/orders/{id}/receipt in a NEW TAB
  (use NavigationManager with forceLoad or anchor tag with target="_blank")
- Place button in the Actions section alongside other action buttons
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

**Test print from receipt page:**
- [ ] Navigate to /admin/orders/{id}/receipt for a confirmed order
- [ ] Click "Принтирай" → browser print dialog opens
- [ ] In print preview:
  - [ ] "Принтирай" button NOT visible
  - [ ] "Обратно към поръчката" link NOT visible
  - [ ] Admin sidebar/navigation NOT visible
  - [ ] Receipt content fills the page cleanly
  - [ ] Table has borders, text is readable
  - [ ] No background colors (black and white only)
  - [ ] Fits on one A4 page (for typical 3-5 item orders)
  - [ ] Signature lines visible at bottom
  - [ ] Footer disclaimer visible
- [ ] Cancel print → back to receipt page, everything still works

**Test print button on order detail:**
- [ ] Navigate to /admin/orders/{id} for a CONFIRMED order
- [ ] "Принтирай разписка" button visible in actions section
- [ ] Click → new tab opens with receipt page
- [ ] Navigate to a PENDING order → "Принтирай разписка" NOT visible
- [ ] Navigate to a COMPLETED order → button IS visible
- [ ] Navigate to a CANCELLED order → button NOT visible

**Test with different order types:**
- [ ] Print receipt for individual + pickup order → no address line
- [ ] Print receipt for company + delivery order → all company fields + address
- [ ] Print receipt for order with delivery fee → fee line shown in totals
- [ ] Print receipt for order without delivery fee → no fee line

**Test long order (add many items):**
- [ ] If more than ~10 items, verify table doesn't break awkwardly across pages
  (page-break-inside: avoid should handle this)

### Commit
```bash
git add .
git commit -m "Epic 10: Stories 10.2+10.3 — Print functionality and order detail integration"
```

---

## Phase 6 Complete ✅

At this point ALL features are implemented:
- ✅ Landing page with hero, features, categories
- ✅ Contacts page with info and map
- ✅ Admin dashboard with stats, recent orders, low stock alerts
- ✅ Receipt layout for A4 printing
- ✅ Print via browser dialog (JS interop)
- ✅ Print button on order detail page
- ✅ Footer on all public pages

Update planning/overview.md:
```markdown
| 09 | Landing Page & Contacts     | ✅ Completed  | Epic 01           |
| 10 | Receipt Printing            | ✅ Completed  | Epic 07           |
```

All epics should now be ✅ Completed:
```markdown
| #  | Epic                          | Status         | Dependencies    |
|----|-------------------------------|----------------|-----------------|
| 01 | Project Setup & Scaffolding   | ✅ Completed   | None            |
| 02 | Authentication                | ✅ Completed   | Epic 01         |
| 03 | Category Management           | ✅ Completed   | Epic 02         |
| 04 | Product Management            | ✅ Completed   | Epic 03         |
| 05 | Public Catalog & Product Detail | ✅ Completed | Epic 04         |
| 06 | Cart & Checkout               | ✅ Completed   | Epic 05         |
| 07 | Order Management (Admin)      | ✅ Completed   | Epic 06         |
| 08 | Invoice & Delivery Management | ✅ Completed   | Epic 04         |
| 09 | Landing Page & Contacts       | ✅ Completed   | Epic 01         |
| 10 | Receipt Printing              | ✅ Completed   | Epic 07         |
```

```bash
git add planning/overview.md
git commit -m "Update planning status: Phase 6 complete — all epics done"
```

**Next**: Phase 7 — Full end-to-end testing and polish. This is the final phase before deployment.

---

## Troubleshooting

### If print shows admin sidebar/navigation:
```
The admin sidebar and navigation are visible in the print preview. The
@media print CSS rules must hide these elements. Check that:
1. The sidebar has a CSS class that can be targeted (e.g., .sidebar)
2. The @media print block includes: .sidebar, .navbar, nav { display: none !important; }
3. The CSS is inside the Receipt.razor <style> block, not in a separate file
   that might not load
If the layout uses specific Blazor component names, inspect the rendered
HTML to find the correct selectors to hide.
```

### If receipt doesn't fit on one A4 page:
```
The receipt is overflowing to a second page. Check:
1. @page { margin: 15mm; } is set in the print CSS
2. Font size in print is reasonable: body { font-size: 12pt; } in @media print
3. Table cells don't have excessive padding
4. The receipt container doesn't have max-width that's too wide
5. If the order has many items, ensure page-break-inside: avoid is on
   the table (not the container — the table itself)
For very long orders (15+ items), it's acceptable to flow to a second page.
```

### If JS interop fails for print:
```
The window.printPage() function is not being called. Check:
1. The script file is referenced in index.html:
   <script src="js/print.js"></script>
   It must be BEFORE the Blazor script tag
2. Or if using inline script in index.html, it's inside a <script> tag
3. The component injects IJSRuntime:
   @inject IJSRuntime JSRuntime
4. The call is: await JSRuntime.InvokeVoidAsync("printPage")
5. The function exists on the window object: window.printPage = function() { ... }
```

### If dashboard stats are wrong:
```
The dashboard stat cards show incorrect numbers. Verify:
1. GET /api/orders/stats returns correct counts (test in Swagger)
2. TotalProducts counts only active products (IsActive = true)
3. PendingOrders counts only Status == Pending AND IsCancelled == false
4. ConfirmedOrders counts Status == Confirmed AND IsCancelled == false
5. CompletedOrders counts Status == Completed
If the API returns correct data but the dashboard shows wrong numbers,
check the DTO mapping in the Blazor client.
```

### If low stock alerts don't appear:
```
The low stock section shows the green "all sufficient" message even
though some products have stock ≤ 10. Check:
1. GET /api/products/low-stock?threshold=10 returns the correct products
   (test in Swagger)
2. The endpoint filters by StockQuantity <= threshold AND IsActive = true
3. The Blazor dashboard is calling the endpoint with the correct threshold
4. The conditional rendering checks the list count correctly:
   @if (lowStockProducts.Count > 0) { show table } else { show green message }
```

### If footer doesn't appear on all pages:
```
The footer should appear on every public page (home, catalog, product
detail, cart, checkout, contacts, order confirmation). Make sure:
1. Footer.razor component exists in Components/Layout/
2. It's included in MainLayout.razor AFTER the @Body section
3. It's NOT inside any conditional block that might hide it
Show me the current MainLayout.razor.
```

### If categories don't load on landing page:
```
The categories section on the landing page is empty. The landing page
calls GET /api/categories (public, no auth). Check:
1. The API endpoint is working (test in Swagger without auth)
2. The home page calls CategoryService.GetAllAsync() in OnInitializedAsync
3. The categories are being rendered after the data loads
4. Handle the loading state — show a spinner or skip the section while loading
```

### If receipt page accessible for pending orders:
```
The receipt page should show an error message for pending (unconfirmed)
orders. Add a check after loading the order:
@if (order.Status == 0) {
  <div class="alert alert-warning">
    Разписка може да се генерира само за потвърдени или завършени поръчки.
  </div>
}
Only render the receipt content when Status is Confirmed (1) or Completed (2).
```
