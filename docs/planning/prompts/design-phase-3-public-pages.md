# Design Phase 3: Public Pages Redesign

## Prerequisites

- Design Phases 1-2 completed (theme CSS, layouts redesigned)
- Application runs without errors
- Fresh Claude Code session

---

## Session D3.1 — Home Page

### Prompt 1
```
Read CLAUDE.md, docs/planning/design-plan.md (Phase 3 section), and
.claude/skills/frontend-design/SKILL.md.

Also read:
- src/NaturalStoneImpex.Client/Pages/Public/Home.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign the Home page (landing page). This is visual-only — keep ALL existing
data bindings, the category loading logic, and all @code unchanged.

Requirements (mobile-first):

**Hero Section:**
- Full-width (break out of container if needed), generous height
  (min-height: 60vh on mobile, 70vh on desktop)
- Background: dramatic dark gradient that evokes natural stone
  (e.g., linear-gradient with var(--nsi-dark) and var(--nsi-primary-dark))
  Consider adding a subtle CSS texture or noise pattern for depth
- "Natural Stone Impex" heading: display font, large (clamp for responsive),
  white, with staggered fade-in-up animation
- Subtitle "Качествени строителни материали": lighter weight, fade-in with delay
- "Натурален камък, цимент, плочки и още": muted white (rgba), fade-in with more delay
- CTA button "Разгледайте каталога": use .btn-nsi-accent, large, with hover
  scale effect. Fade-in last.
- Center everything vertically and horizontally

**Features Section ("Защо да изберете нас"):**
- Section heading with display font, subtle after-line decoration
  (e.g., short accent-colored line below the heading)
- Replace emojis (🏗️💰🚚) with Bootstrap Icons in accent-colored circles:
  - 🏗️ → bi-gem or bi-building (Качествени материали)
  - 💰 → bi-tag or bi-currency-euro (Конкурентни цени)
  - 🚚 → bi-truck (Бърза доставка)
- Cards: border-0, shadow-sm, rounded (var(--nsi-radius-md)), hover lift effect
- Grid: 1 col on mobile (stacked) → 3 col on md+
- Staggered fade-in-up animation on each card (use .animate-stagger-* classes)

**Categories Section ("Нашите категории"):**
- Same heading style as features
- Cards with a warm gradient background (since no category images exist):
  use different gradients per card from the brand palette
  (e.g., primary→primary-dark, accent→primary, etc.)
- Category name in white overlay text, centered, display font
- Product count below name in muted
- Hover: slight lift + shadow increase
- Grid: 1 col → 2 col sm → 3 col md+

**Contact Summary Section ("Свържете се с нас"):**
- Background: .section-warm or .section-dark (try warm first)
- Replace emojis (📞📍🕐) with Bootstrap Icons in styled circles
  (bi-telephone, bi-geo-alt, bi-clock)
- Clean card-like items or simple icon+text blocks
- Grid: stacked on mobile → 3 col on md+
- "Към контакти" button: .btn-nsi-outline, centered

**General:**
- Generous section spacing (section padding utilities from site.css)
- Each section should have visual rhythm (warm→white→warm or similar)
- All animations should be subtle and professional — not bouncy or flashy
- Keep all existing text exactly as-is (Bulgarian)
- Keep all existing data bindings (@foreach categories, etc.) unchanged
- Keep the entire @code block unchanged
```

### Verify
- [ ] Hero: dramatic gradient, animated text, accent CTA button, responsive
- [ ] Hero mobile (375px): text readable, button full-width or appropriately sized
- [ ] Features: Bootstrap Icons in circles (no emojis), card hover effects
- [ ] Categories: gradient backgrounds, hover effects, grid responsive
- [ ] Contact: icons instead of emojis, clean layout, responsive
- [ ] All sections have good vertical spacing
- [ ] Animations play on page load (staggered, smooth)
- [ ] All links still functional (catalog link, category links, contacts link)
- [ ] No horizontal overflow on any screen size

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Public/Home.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 3.1: Home page — hero gradient, icon features, animated sections"
```

---

## Session D3.2 — Catalog Page

### Prompt 1
```
Read docs/planning/design-plan.md (Phase 3.2 section).

Also read:
- src/NaturalStoneImpex.Client/Pages/Public/Catalog.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign the Catalog page. Visual-only — keep ALL existing data bindings,
search debounce logic, pagination, category filtering, and @code unchanged.

Requirements (mobile-first):

**Page header:**
- "Каталог" heading with display font
- Subtle breadcrumb or page description below

**Category filter — Mobile-first:**
- Mobile: currently a dropdown — restyle as horizontal scrollable chips/pills
  (overflow-x: auto, flex-nowrap) with accent color on active chip.
  If this is too complex, keep the dropdown but style it with brand colors
  and rounded corners.
- Desktop (lg+): sidebar list with styled items — active category gets
  accent left border + background highlight + bold text. Count badges on
  each category.
- "Всички категории" item styled distinctly (slightly different from others)

**Search input:**
- Modern rounded input (border-radius 50px or var(--nsi-radius-md))
- Search icon (bi-search) INSIDE the input on the left (use input-group
  with the icon as a visual addon, or CSS position)
- Focus ring with accent color
- Full-width on mobile, appropriate width on desktop
- Placeholder "Търсене на продукт..." stays

**Product grid:**
- 1 col → 2 col sm → 3 col md → 4 col xl
- Consistent card heights via flex
- Gap between cards: g-4
- ProductCard component handles card styling (Phase D4) — just ensure the
  grid container is correct

**Empty state:**
- "Няма намерени продукти." — larger text, muted, centered
- Add a large Bootstrap Icon above (bi-search or bi-inbox) for visual weight
- Keep the existing text

**Loading state:**
- Existing spinner — style it with brand primary color if not already done

**Pagination:**
- Pagination component handles its own styling (Phase D4) — just ensure
  it's centered below the grid with appropriate margin

Keep all @code logic unchanged. Keep all existing conditional rendering
(@if _isLoading, @if products empty, etc.) unchanged.
```

### Verify
- [ ] Page header with display font
- [ ] Mobile (375px): category chips/dropdown, search full-width, 1-col grid
- [ ] Tablet (768px): 2-3 col grid
- [ ] Desktop (1200px): sidebar visible, 3-4 col grid, search appropriately sized
- [ ] Search has icon, rounded, accent focus ring
- [ ] Category selection still works (API calls, products filter)
- [ ] Search still works with debounce
- [ ] Empty state has icon + text
- [ ] Pagination renders correctly
- [ ] Loading spinner visible during data fetch

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Public/Catalog.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 3.2: Catalog page — search redesign, category sidebar, responsive grid"
```

---

## Session D3.3 — Product Detail Page

### Prompt 1
```
Read docs/planning/design-plan.md (Phase 3.3 section).

Also read:
- src/NaturalStoneImpex.Client/Pages/Public/ProductDetail.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign the Product Detail page. Visual-only — keep ALL data bindings,
the AddToCart logic, the toast notification, and @code unchanged.

Requirements (mobile-first):

**Breadcrumb:**
- Subtle, smaller text, lighter color (not prominent)
- Use var(--nsi-gray) for separators

**Image area:**
- Mobile: full-width, rounded corners (var(--nsi-radius-md)), aspect-ratio: 1
  for consistent height, shadow-sm
- Desktop: left column (col-md-6), same styling
- Use .img-zoom-wrapper for hover zoom effect
- "Няма снимка" placeholder: styled with warm background, icon (bi-image),
  centered text

**Product info area:**
- Mobile: below image, full-width
- Desktop: right column (col-md-6)
- Product name: large display font (h2)
- Category badge: pill-shaped, accent-colored (.badge-nsi-accent)
- Description: regular body text, good line-height
- Price section — visually prominent:
  - Main price (с ДДС): very large, accent color, display font
  - Without ДДС and ДДС amount: smaller, muted, below main price
  - Separated from other content with subtle border or background
- Stock status: pill badge (.badge-nsi-success or .badge-nsi-danger)
- Unit display: clean text, not a badge
- Quantity input: styled with brand focus ring, appropriate width
  (not full-width, just enough for the number)
- "Добави в количката" button: .btn-nsi-accent, large (btn-lg), with
  cart icon prefix (bi-cart-plus), full-width on mobile, auto-width on desktop
  Disabled state should be visually muted

**Mobile sticky CTA:**
- On mobile (< md), add a sticky bar at the bottom of the viewport with
  the price and "Добави в количката" button. This requires:
  - A fixed-bottom bar (position: fixed, bottom: 0, z-index: 1000)
  - Show only on mobile (d-md-none)
  - Contains: price + add-to-cart button side by side
  - Background: white with shadow-lg on top
  - Hide the regular button in the product info area on mobile to avoid
    duplication (d-none d-md-block on the original button)
  NOTE: This sticky bar duplicates the button — both should call the same
  existing AddToCart method. Don't modify @code, just add a second button
  in the markup that calls the same method.

**"Обратно към каталога" link:**
- Subtle, with arrow icon (bi-arrow-left), above the breadcrumb or below content

Keep all @code logic unchanged. Keep all existing data bindings unchanged.
```

### Verify
- [ ] Breadcrumb subtle and functional
- [ ] Image: rounded, zoom on hover, responsive
- [ ] Product name in display font
- [ ] Main price large and accent-colored
- [ ] Stock badge pill-shaped with brand colors
- [ ] Add-to-cart button styled with icon
- [ ] Mobile (375px): image on top, info below, sticky CTA bar at bottom
- [ ] Desktop (1200px): side-by-side layout, no sticky bar
- [ ] Add-to-cart still works (both regular button and mobile sticky bar)
- [ ] Toast notification still appears
- [ ] Loading and error states still render

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Public/ProductDetail.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 3.3: Product detail — image zoom, accent pricing, mobile sticky CTA"
```

---

## Session D3.4 — Cart, Checkout, Order Confirmation, Contacts

### Prompt 1
```
Read docs/planning/design-plan.md (Phases 3.4–3.7).

Read these files:
- src/NaturalStoneImpex.Client/Pages/Public/Cart.razor
- src/NaturalStoneImpex.Client/Pages/Public/Checkout.razor
- src/NaturalStoneImpex.Client/Pages/Public/OrderConfirmation.razor
- src/NaturalStoneImpex.Client/Pages/Public/Contacts.razor

Redesign ALL FOUR pages in one pass. Visual-only — keep ALL data bindings,
form logic, validation, CartService calls, OrderService calls, and @code
blocks unchanged in every file.

**Cart.razor:**
- Mobile-first: card layout is primary (already exists), restyle it:
  - Product thumbnail with rounded corners
  - Clean quantity display with styled +/- stepper buttons around the input
    (add - and + buttons wrapping the existing InputNumber, purely visual
    enhancement — they call the same quantity update logic)
    NOTE: If adding +/- buttons requires C# code changes, skip them and
    just style the existing InputNumber instead.
  - Row totals in accent color
  - Remove button: subtle, icon-only (bi-x-lg or bi-trash)
- Desktop (md+): table with .table-container wrapper (rounded, shadow)
  - Header with brand primary bg (from table override in site.css)
- Summary card: border-left or border-top with accent color, shadow
- "Обща сума" in large display font, accent color
- Buttons: "Продължи към поръчка" as .btn-nsi-accent, "Продължи пазаруването"
  as .btn-nsi-outline
- Empty cart: large icon (bi-cart-x), bigger text, warm styling

**Checkout.razor:**
- Numbered sections already exist in cards — restyle the cards:
  - Section numbers: accent-colored circles (or badges) instead of plain "1."
  - Card headers: subtle warm background, display font
  - Card bodies: generous padding
- Radio buttons for customer type and delivery: style as selectable cards
  (full card clickable, selected state with accent border). If too complex
  with existing Blazor bindings, just improve the radio button visual styling
  (larger, custom styled radio with accent color)
- Form inputs: already restyled by site.css global overrides
- Order summary table: .table-container wrapper
- Totals: accent color, large
- Submit button: .btn-nsi-accent, large, full-width on mobile
- Mobile: all sections stack naturally, full-width
- Desktop (lg+): consider order summary as sticky sidebar (right column)
  while form sections on the left — but only if this doesn't require
  @code changes. If layout restructuring is too complex, keep single
  column with better styling.

**OrderConfirmation.razor:**
- Success icon: replace the plain ✓ with an animated CSS checkmark
  (circle with animated draw-in of checkmark using stroke-dasharray/offset)
  or use a large Bootstrap Icon (bi-check-circle-fill) in var(--nsi-success)
  with fade-in animation
- Card: larger shadow, more padding, rounded
- Order number: displayed in a distinct styled box (bg var(--nsi-light),
  rounded, monospace-ish text)
- "Обратно към каталога" button: .btn-nsi-primary
- Centered with generous vertical spacing

**Contacts.razor:**
- Replace ALL emojis (🏢📍📞📧🕐) with Bootstrap Icons:
  - 🏢 → bi-building
  - 📍 → bi-geo-alt-fill
  - 📞 → bi-telephone-fill
  - 📧 → bi-envelope-fill
  - 🕐 → bi-clock-fill
- Style icons in accent-colored circles (consistent with Home page features)
- Cards: rounded, warm shadow
- Map iframe: rounded corners, shadow
- Mobile: stacked cards, full-width map
- Desktop: side-by-side as current
- CTA section at bottom: warm background, clean styling
- Keep all contact info text and links exactly as-is

Keep all @code blocks unchanged in ALL four files.
```

### Verify

**Cart:**
- [ ] Mobile (375px): card layout, thumbnails, styled quantity, branded totals
- [ ] Desktop: table with rounded container, brand header
- [ ] Summary card has accent border and large total
- [ ] Buttons styled with brand classes
- [ ] Empty cart has large icon
- [ ] All cart operations still work (update qty, remove, navigate)

**Checkout:**
- [ ] Section cards restyled with accent numbers, warm headers
- [ ] Radio buttons improved visually
- [ ] Form inputs have brand focus rings
- [ ] Submit button accent and large
- [ ] All validation still works (test submit with empty fields)
- [ ] Order placement still works end-to-end

**Order Confirmation:**
- [ ] Success icon animated or styled prominently
- [ ] Order number in distinct box
- [ ] Card well-styled with shadow
- [ ] "Обратно към каталога" works

**Contacts:**
- [ ] Bootstrap Icons instead of all emojis
- [ ] Icons in accent circles
- [ ] Cards rounded with shadow
- [ ] Map iframe rounded
- [ ] All contact links (tel:, mailto:) still work
- [ ] Responsive: stacked on mobile, side-by-side on desktop

### Commit
```bash
git add src/NaturalStoneImpex.Client/Pages/Public/Cart.razor src/NaturalStoneImpex.Client/Pages/Public/Checkout.razor src/NaturalStoneImpex.Client/Pages/Public/OrderConfirmation.razor src/NaturalStoneImpex.Client/Pages/Public/Contacts.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 3.4: Cart, Checkout, OrderConfirmation, Contacts — full redesign"
```

---

## Design Phase 3 Complete ✅

At this point ALL public pages should have the new design:
- ✅ Home: animated hero, icon features, gradient categories, styled contacts
- ✅ Catalog: search with icon, category sidebar/chips, responsive grid
- ✅ Product Detail: image zoom, accent pricing, mobile sticky CTA
- ✅ Cart: branded table/cards, accent totals, styled buttons
- ✅ Checkout: restyled sections, branded form, accent submit
- ✅ Order Confirmation: animated success, styled card
- ✅ Contacts: Bootstrap Icons, accent circles, rounded cards

**Next**: Design Phase 4 — Shared Components. Start a fresh Claude Code session.

---

## Troubleshooting

### If hero gradient looks flat or dull:
```
The hero gradient looks too flat. Add more color stops or use a radial
gradient overlay for depth. Consider:
background: linear-gradient(135deg, var(--nsi-darker) 0%, var(--nsi-primary-dark) 50%, var(--nsi-dark) 100%);
Or add a subtle texture using a CSS pattern or a repeating-linear-gradient
for a stone-like feel.
```

### If animations don't trigger on page load:
```
The fade-in animations are not playing. In Blazor, content renders after
the component initializes. Make sure animation classes are applied in the
initial render (not added dynamically). The CSS animations should use
animation-fill-mode: forwards and start from opacity: 0 in the keyframe.
```

### If mobile sticky CTA overlaps footer or content:
```
The mobile sticky CTA bar is overlapping the footer or cutting off content.
Add padding-bottom to the main content area on mobile equal to the height
of the sticky bar (typically 60-80px). Use a media query:
@media (max-width: 767.98px) { .product-detail-content { padding-bottom: 80px; } }
```

### If checkout radio buttons lose binding:
```
The radio button styling change broke the Blazor binding. Make sure the
input elements still have their @onchange handlers and checked attributes
exactly as they were. Only change the surrounding HTML structure and CSS
classes, not the input elements themselves.
```

### If category chips overflow on mobile:
```
The horizontal category chips are overflowing or not scrollable. Make sure
the container has: display: flex; overflow-x: auto; white-space: nowrap;
-webkit-overflow-scrolling: touch; and each chip has flex-shrink: 0.
Add scroll-snap-type: x mandatory for smooth snapping.
```
