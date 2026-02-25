# Design Phase 5: Admin Pages Redesign

## Prerequisites

- Design Phases 1-4 completed (theme, layouts, public pages, shared components)
- Application runs without errors
- Admin sidebar already redesigned (Phase D2.2)
- Fresh Claude Code session

---

## Session D5.1 — Login Page + Dashboard

### Prompt 1
```
Read CLAUDE.md, docs/planning/design-plan.md (Phase 5 section), and
.claude/skills/frontend-design/SKILL.md.

Read these files:
- src/NaturalStoneImpex.Client/Pages/Admin/Login.razor
- src/NaturalStoneImpex.Client/Pages/Admin/Dashboard.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign BOTH pages. Visual-only — keep ALL data bindings, form models,
validation, EditForm, event handlers, and @code blocks unchanged.

**Login.razor:**
- Card: remove the inline style, use CSS classes instead:
  - Max-width ~420px on desktop, full-width with padding on mobile
  - Shadow-lg, border-radius var(--nsi-radius-lg), no visible border
  - Generous padding (p-4 mobile, p-5 desktop)
- Brand header: "Natural Stone Impex" in display font, larger, with subtle
  accent decoration (small line, accent color, or decorative element)
- "Вход в администрацията" subtitle in muted, smaller
- Form inputs: already restyled by site.css overrides, but ensure they
  have proper spacing (mb-4 instead of mb-3 for breathing room)
- Login button: .btn-nsi-accent instead of .btn-primary, full-width,
  min-height 48px
- Error alert: softer styling (rounded, accent-danger border instead
  of hard Bootstrap danger)
- Spinner: use brand primary color
- Keep all EditForm, DataAnnotationsValidator, InputText bindings unchanged
- Keep all @code unchanged

**Dashboard.razor:**
- Page heading "Табло": display font, larger
- Stat cards (4 cards: products, pending, confirmed, completed):
  - Mobile: 2 col (col-6) instead of stacking to 1 — stats should be
    visible at a glance. 4 col on lg+.
  - Each card: rounded (var(--nsi-radius-md)), shadow-sm, white bg
  - Replace the border-start Bootstrap color classes with brand variables:
    - Products → var(--nsi-primary) left border
    - Pending → var(--nsi-warning) left border
    - Confirmed → var(--nsi-accent) left border
    - Completed → var(--nsi-success) left border
  - Number: large (h2 or bigger), using the corresponding color
  - Label: muted, small caps or subtle styling
  - Add a Bootstrap Icon to each card for visual weight:
    - Products: bi-box-seam
    - Pending: bi-hourglass-split
    - Confirmed: bi-check2-circle
    - Completed: bi-check2-all
  - Subtle hover effect (lift)

- Recent orders table:
  - Wrap in .table-container (rounded, shadow-sm)
  - Header: brand primary bg (from site.css table override)
  - Status badges: pill-shaped with distinct colors:
    - Pending → warning bg
    - Confirmed → accent/info bg
    - Completed → success bg
    - Cancelled → danger bg
  - Clickable rows keep existing navigation
  - Mobile: consider making the table horizontally scrollable with
    overflow-x: auto on the container, OR convert to card-based layout.
    If card conversion is too complex without @code changes, just make
    the table container scrollable.

- Low stock alerts:
  - Card with warning accent (border-start var(--nsi-warning))
  - Alert icon in header (bi-exclamation-triangle)
  - Same table styling as recent orders
  - Mobile: scrollable table container

Keep all @code unchanged. Keep all data bindings and conditional rendering.
```

### Verify

**Login:**
- [ ] Card: no inline style, CSS-class based max-width, shadow, rounded
- [ ] Display font brand, accent subtitle
- [ ] Button is accent-colored, 48px height
- [ ] Form still works: test admin/Admin123! login
- [ ] Error message styled softly on wrong password
- [ ] Mobile: card full-width with padding
- [ ] Desktop: card centered, ~420px

**Dashboard:**
- [ ] Stat cards: 2-col on mobile, 4-col on desktop
- [ ] Cards have brand-colored left borders and icons
- [ ] Numbers are large and colored
- [ ] Hover effect on stat cards
- [ ] Recent orders table: rounded container, brand header
- [ ] Status badges pill-shaped with distinct colors
- [ ] Low stock card: warning accent, alert icon
- [ ] Mobile: tables scrollable or card-based
- [ ] All data loads correctly (stats, orders, low stock)
- [ ] Clicking order row still navigates to detail

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Admin/Login.razor src/NaturalStoneImpex.Client/Pages/Admin/Dashboard.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 5.1: Login page + Dashboard — branded cards, stat icons, styled tables"
```

---

## Session D5.2 — Products List + Product Form

### Prompt 1
```
Read docs/planning/design-plan.md (Phase 5.3 section).

Read these files:
- src/NaturalStoneImpex.Client/Pages/Admin/Products.razor
- src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign BOTH pages. Visual-only — keep ALL data bindings, form logic,
EditForm, validation, search, pagination, and @code unchanged.

**Products.razor (product list):**
- Page heading: display font, with "Нов продукт" button (btn-nsi-accent)
  aligned to the right (flex between)
- Search/filter bar: styled search input (rounded, with icon), category
  dropdown with brand styling. Horizontally aligned on desktop, stacked on mobile.
- Product table:
  - Wrapped in .table-container (rounded, shadow-sm, overflow hidden)
  - Header: brand primary bg
  - Image column: small rounded thumbnails (40x40, border-radius 8px)
  - Price column: formatted clearly
  - Stock column: color-coded (green if > 5, yellow if 1-5, red if 0)
    NOTE: only do this if the existing markup already has conditional
    styling. If not, just make it look clean.
  - Status column: active/inactive badge (pill, success/danger)
  - Action buttons: icon-only (bi-pencil, bi-eye, bi-toggle-off/on) with
    hover tooltips, grouped together
  - Mobile: horizontally scrollable table container
- Pagination: already styled by Phase D4

**ProductForm.razor (add/edit form):**
- Page heading: display font, "Нов продукт" or "Редактиране на продукт"
- Back link: subtle, with bi-arrow-left icon
- Form organized in card sections:
  - Section 1 — Basic Info: name, category, description
  - Section 2 — Pricing: price without VAT, VAT amount, price with VAT
    (keep them in a card together for visual grouping)
  - Section 3 — Stock & Unit: quantity, unit type
  - Section 4 — Image: image upload area with styled border (dashed, accent
    color on hover/dragover). Preview of current image if editing.
  - Each section: card with subtle heading, generous padding
- Form inputs: already styled by site.css, just ensure proper grouping
- Submit button: .btn-nsi-accent, with appropriate icon (bi-check-lg)
- Cancel button: .btn-nsi-outline
- Mobile: all sections stack, full-width inputs
- Desktop: consider 2-column layout for pricing fields (3 prices side by side)

Keep all @code unchanged. Keep all EditForm bindings, validation, and
image upload logic unchanged.
```

### Verify
- [ ] Products list: rounded table, brand header, thumbnails, styled actions
- [ ] Search: icon, rounded, brand focus ring
- [ ] "Нов продукт" button: accent, positioned correctly
- [ ] Mobile: table scrollable, search/filter stacked
- [ ] Product form: organized in card sections
- [ ] Pricing fields visually grouped
- [ ] Image upload area styled
- [ ] All form fields still work (create and edit product)
- [ ] Validation still shows Bulgarian error messages
- [ ] Pagination works on product list

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Admin/Products.razor src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 5.2: Products list + form — table container, card sections, styled upload"
```

---

## Session D5.3 — Categories, Orders, OrderDetail

### Prompt 1
```
Read docs/planning/design-plan.md (Phase 5 section).

Read these files:
- src/NaturalStoneImpex.Client/Pages/Admin/Categories.razor
- src/NaturalStoneImpex.Client/Pages/Admin/Orders.razor
- src/NaturalStoneImpex.Client/Pages/Admin/OrderDetail.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign ALL THREE pages. Visual-only — keep ALL data bindings, form logic,
EditForm, event handlers, and @code unchanged.

**Categories.razor:**
- Same patterns as Products list:
  - Page heading with display font + "Нова категория" button (btn-nsi-accent)
  - Table or list wrapped in .table-container
  - Brand header bg
  - Action buttons: icon-only, styled
  - Add/edit form (if inline): styled inputs with brand focus rings
  - Mobile: scrollable or card-based

**Orders.razor:**
- Page heading: display font
- Filter/status tabs: if there's a status filter, style as pills or
  segmented buttons with brand colors. Active tab highlighted.
- Orders table:
  - .table-container wrapper
  - Brand header
  - Order number column: monospace-ish, slightly bold
  - Customer name column
  - Date column: formatted dd.MM.yyyy
  - Status badges: pill-shaped, color-coded:
    - Pending (Чакаща) → var(--nsi-warning) bg
    - Confirmed (Потвърдена) → var(--nsi-accent) bg or info
    - Completed (Завършена) → var(--nsi-success) bg
    - Cancelled (Отменена) → var(--nsi-danger) bg
  - Clickable rows
  - Mobile: scrollable table
- Pagination: already styled

**OrderDetail.razor:**
- Page heading: order number in display font, status badge next to it
- Back link: subtle, with bi-arrow-left
- Info organized in cards:
  - Card 1 — Order Info: order number, date, status, delivery method
  - Card 2 — Customer Info: all customer fields
  - Card 3 — Order Items: table with .table-container, item rows with
    product name, quantity, unit price, total
  - Card 4 — Totals: subtotal, VAT, grand total in accent color
- Action buttons (confirm, complete, cancel): styled with appropriate
  brand colors:
  - Confirm: .btn-nsi-accent or btn-nsi-primary
  - Complete: success-styled button
  - Cancel: danger-styled button (outline)
- Mobile: cards stack, tables scrollable
- Desktop: consider 2-column layout (order info + customer side by side,
  items table full-width below)

Keep all @code unchanged.
```

### Verify
- [ ] Categories: styled table/list, brand header, action buttons
- [ ] Categories: add/edit still works
- [ ] Orders: status badges pill-shaped and color-coded
- [ ] Orders: table in rounded container
- [ ] Orders: clicking row navigates to detail
- [ ] Order Detail: info organized in cards
- [ ] Order Detail: status badge prominent next to heading
- [ ] Order Detail: items table styled
- [ ] Order Detail: totals in accent color
- [ ] Order Detail: action buttons (confirm/complete/cancel) work correctly
- [ ] Mobile: all pages responsive (scrollable tables, stacked cards)

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Admin/Categories.razor src/NaturalStoneImpex.Client/Pages/Admin/Orders.razor src/NaturalStoneImpex.Client/Pages/Admin/OrderDetail.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 5.3: Categories, Orders, OrderDetail — branded tables, status badges, card layout"
```

---

## Session D5.4 — Invoices + Receipt

### Prompt 1
```
Read docs/planning/design-plan.md (Phases 5.6-5.7).

Read these files:
- src/NaturalStoneImpex.Client/Pages/Admin/Invoices.razor
- src/NaturalStoneImpex.Client/Pages/Admin/InvoiceCreate.razor
- src/NaturalStoneImpex.Client/Pages/Admin/InvoiceDetail.razor
- src/NaturalStoneImpex.Client/Pages/Admin/Receipt.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign ALL FOUR pages. Visual-only — keep ALL data bindings, form logic,
print functionality, and @code unchanged.

**Invoices.razor (list):**
- Same patterns as Orders list:
  - Page heading: display font
  - Table in .table-container, brand header
  - Clean date formatting
  - Action buttons: icon-only, styled
  - "Нова доставка" button: .btn-nsi-accent
  - Mobile: scrollable table
  - Pagination: already styled

**InvoiceCreate.razor (form):**
- Same patterns as ProductForm:
  - Card sections for form grouping
  - Select order dropdown: styled
  - Product/quantity items: clean list/table
  - Submit: .btn-nsi-accent
  - Mobile: stacked, full-width

**InvoiceDetail.razor:**
- Same patterns as OrderDetail:
  - Info in cards
  - Items table in .table-container
  - Totals in accent color
  - Print button: .btn-nsi-primary with bi-printer icon
  - Mobile: stacked cards, scrollable table

**Receipt.razor — MINIMAL CHANGES ONLY:**
- This page uses window.print() and must remain print-optimized
- Only change the screen-visible styling (not the print layout)
- Add @media screen only styles for:
  - Better font sizing on screen
  - Brand accent for the print button
  - Card wrapper with shadow for screen display
- Do NOT change any print-specific styles or the receipt content layout
- The receipt must still print correctly on paper

Keep all @code unchanged in ALL files.
Keep all print JS interop unchanged.
```

### Verify

**Invoices list:**
- [ ] Table in rounded container with brand header
- [ ] "Нова доставка" button accent
- [ ] Mobile: scrollable table
- [ ] Pagination works

**Invoice Create:**
- [ ] Form in card sections
- [ ] All fields functional
- [ ] Submit creates invoice successfully

**Invoice Detail:**
- [ ] Info in styled cards
- [ ] Items table branded
- [ ] Totals accent
- [ ] Print button styled

**Receipt:**
- [ ] Screen display has card wrapper, branded button
- [ ] Click print → browser print dialog opens
- [ ] Print preview shows clean receipt (no screen-only styles leaking)
- [ ] Receipt prints correctly on paper (if possible, test with PDF printer)

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Admin/Invoices.razor src/NaturalStoneImpex.Client/Pages/Admin/InvoiceCreate.razor src/NaturalStoneImpex.Client/Pages/Admin/InvoiceDetail.razor src/NaturalStoneImpex.Client/Pages/Admin/Receipt.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 5.4: Invoices, InvoiceCreate, InvoiceDetail, Receipt — final admin redesign"
```

---

## Design Phase 5 Complete ✅

At this point ALL admin pages should have the new design:
- ✅ Login: branded card, display font, accent button
- ✅ Dashboard: stat cards with icons and brand colors, styled tables
- ✅ Products: rounded table, card-based form, styled upload
- ✅ Categories: branded table, styled actions
- ✅ Orders: status badge pills, card-based detail
- ✅ Invoices: matching table/form styling
- ✅ Receipt: screen styling improved, print layout preserved

---

## Final Verification — Full Application Design Review

After ALL five design phases are complete, do a final pass:

```bash
dotnet build
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test every page at three widths (375px, 768px, 1920px):

**Public pages:**
- [ ] Home: hero animation, icon features, gradient categories
- [ ] Catalog: search icon, category filter, responsive grid
- [ ] Product Detail: image zoom, accent pricing, mobile sticky CTA
- [ ] Cart: branded totals, styled table/cards
- [ ] Checkout: styled sections, accent submit
- [ ] Order Confirmation: animated success icon
- [ ] Contacts: Bootstrap Icons, rounded map

**Admin pages:**
- [ ] Login: branded card, working auth
- [ ] Dashboard: stat cards, styled tables
- [ ] Products list + form: rounded tables, card sections
- [ ] Categories: styled management
- [ ] Orders list + detail: status badges, card layout
- [ ] Invoices list + create + detail: matching styling
- [ ] Receipt: prints correctly

**Cross-cutting:**
- [ ] Navbar: sticky, display font brand, active indicators
- [ ] Admin sidebar: offcanvas on mobile, permanent on desktop
- [ ] Footer: 3 columns, accent top border
- [ ] ProductCard: image zoom, accent button, consistent heights
- [ ] Pagination: rounded, touch-friendly
- [ ] CartIcon: accent badge, updates correctly
- [ ] Toast: slide-in, auto-dismiss
- [ ] No horizontal overflow on ANY page at ANY width
- [ ] All touch targets ≥ 44×44px on mobile
- [ ] All animations smooth (60fps, no jank)
- [ ] Brand identity consistent across all pages
- [ ] No generic Bootstrap look remaining

### Final Commit
```bash
git add -A
git commit -m "Design complete: Full visual redesign — Natural Stone Impex brand identity"
```

---

## Troubleshooting

### If admin tables break on mobile:
```
Admin tables are overflowing on mobile. Wrap every table in a
.table-container div that has overflow-x: auto. This allows horizontal
scrolling while keeping the table readable. Do NOT try to make wide
tables responsive with stacking — that breaks data alignment.
```

### If print receipt shows screen styles:
```
The receipt is showing screen-only styles (shadows, brand colors) when
printing. Make sure all screen-only styles are wrapped in @media screen
blocks. The print layout should be plain, with no shadows, no colored
backgrounds, and no decorative elements. Check with Ctrl+P preview.
```

### If dashboard stat cards don't align:
```
The stat cards on the dashboard have inconsistent heights or don't align.
Make sure each card has h-100 class and the grid uses equal column sizes.
Use align-items-stretch on the row (default in Bootstrap flex).
```

### If form card sections are too cramped:
```
The card sections in the product/invoice forms feel cramped. Add more
padding to .card-body (p-4 minimum, p-5 on desktop). Add mb-4 between
form groups inside the card. Use gap utilities on the form.
```

### If login card has both inline and CSS styles:
```
The login card still has the old inline style attribute. Remove
style="width: 100%; max-width: 400px;" from the card div and use CSS
classes instead. Add a custom class like .login-card with max-width and
width defined in site.css, with responsive adjustments.
```
