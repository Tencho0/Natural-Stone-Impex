# Design Modernization Plan — Natural Stone Impex

## Context

The application is fully functional but uses plain Bootstrap 5 with virtually no custom CSS (78 lines of boilerplate). Every page looks like a generic Bootstrap template — dark navbar, default cards, default buttons, no brand identity. The goal is to transform the UI into a modern, professional storefront with a cohesive visual identity that fits a building materials / natural stone business, while keeping Bootstrap 5 as the foundation and not introducing new frameworks.

## Design Philosophy — Avoiding Generic "AI Slop"

Inspired by the [frontend-design plugin](https://github.com/anthropics/claude-code/tree/main/plugins/frontend-design) principles, adapted for a **professional business context**:

- **No generic defaults**: Avoid overused fonts (Inter, Roboto, Arial, system fonts), cliched color schemes (purple gradients on white), and predictable Bootstrap layouts
- **Intentional aesthetic**: Every design choice must serve the "natural stone / building materials" identity — earthy, solid, trustworthy, premium but approachable
- **Distinctive typography**: Pair a characterful display font with a refined body font — not the first Google Fonts result
- **Dominant color with sharp accents**: Warm earth tones dominate, gold accents punctuate — no timid, evenly-distributed palettes
- **Motion with purpose**: One well-orchestrated page load with staggered reveals beats scattered micro-interactions. Hover states should surprise subtly.
- **Atmosphere over flat color**: Use gradient meshes, subtle textures, layered shadows — not plain solid backgrounds everywhere

## Mobile-First Design Strategy

**All CSS and markup must be written mobile-first.** This is the core principle:

- **CSS writes for mobile by default**, then uses `min-width` media queries (or Bootstrap's `md:`, `lg:` breakpoints) to enhance for larger screens
- **Touch targets**: All buttons and interactive elements minimum 44×44px
- **Single-column layouts by default**, expanding to multi-column on `md`+ breakpoints
- **Navigation**: Mobile hamburger menu is the primary design; desktop horizontal nav is the enhancement
- **Product grid**: 1 column on mobile → 2 on `sm` → 3 on `md` → 4 on `lg`
- **Admin sidebar**: Hidden by default on mobile, slide-in overlay or offcanvas triggered by hamburger; visible permanently only on `lg`+
- **Tables**: Card-based layout on mobile, table layout on `md`+ (already partially done in Cart)
- **Font sizes**: Base sizes for mobile, scale up with `clamp()` or media queries
- **Spacing**: Compact padding/margins on mobile (`p-3`), generous on desktop (`p-4`, `p-5`)
- **Images**: Full-width on mobile, constrained on desktop. Use `aspect-ratio` for consistent placeholders
- **Forms**: Full-width inputs stacked vertically on mobile, side-by-side where appropriate on desktop
- **Sticky elements**: Sticky "Add to Cart" bar on mobile product detail, sticky order summary on desktop checkout

### Breakpoint Reference (Bootstrap 5)
| Breakpoint | Class prefix | Min-width | Usage |
|------------|-------------|-----------|-------|
| (default)  | —           | 0px       | Mobile phones (portrait) — **design starts here** |
| `sm`       | `sm:`       | 576px     | Mobile phones (landscape), small tablets |
| `md`       | `md:`       | 768px     | Tablets |
| `lg`       | `lg:`       | 992px     | Small desktops, tablets landscape |
| `xl`       | `xl:`       | 1200px    | Standard desktops |
| `xxl`      | `xxl:`      | 1400px    | Large desktops |

## Data Structure Constraint — DO NOT MODIFY

**Critical rule**: The data layer is frozen. This redesign is purely visual/CSS/markup.

- **DO NOT** add, remove, rename, or reorder any fields, DTOs, entities, or API endpoints
- **DO NOT** change any C# logic, services, or data flow
- **DO NOT** add/remove form inputs — every field the user sees today must remain exactly as-is
- **DO NOT** change field labels text (they are in Bulgarian and correct)
- **DO** change the visual styling, layout, spacing, colors, fonts, animations, and HTML structure of how those same fields are presented
- If a page shows 5 fields today, it shows the same 5 fields after redesign — just prettier

## Approach: Custom CSS Theme Layer on Top of Bootstrap

Instead of replacing Bootstrap, we add a custom CSS theme file (`site.css`) with CSS custom properties (variables), refined component styles, Google Fonts, and subtle animations. Then we update each `.razor` file to use the new classes. This is the most efficient approach — no new dependencies, no build tool changes.

## Frontend Design Skill — Installed

The `frontend-design` skill (adapted from [Anthropic's official plugin](https://github.com/anthropics/claude-code/tree/main/plugins/frontend-design)) is installed at `.claude/skills/frontend-design/SKILL.md`. Claude automatically uses it during frontend work.

It enforces:
- No generic fonts (Inter, Roboto, Arial), no cliched color schemes, no cookie-cutter patterns
- Intentional organic/natural + luxury/refined aesthetic matching the stone brand
- Mobile-first implementation, frozen data structure
- Distinctive typography, dominant color with sharp accents, motion with purpose

**Design preview**: Run the app in browser and test with Chrome DevTools device toolbar. No Figma needed.

---

## Phase 1: Foundation — Theme & Global Styles

### 1.1 Add Google Fonts to `index.html`
- **Display font**: Something distinctive and premium — e.g., DM Serif Display, Cormorant Garamond, or Libre Baskerville (NOT Inter, NOT Playfair Display which is overused)
- **Body font**: Clean and readable — e.g., DM Sans, Source Sans 3, or Outfit
- The exact choice should feel "stone/earth/craft" — solid, grounded, slightly serif for headings
- Add `<link>` with `display=swap` in `wwwroot/index.html`

### 1.2 Create `wwwroot/css/site.css` — master theme file (mobile-first)
- **CSS Custom Properties** for brand colors (see Color Palette below)
- **Global overrides**: body font-family, `background-color: var(--nsi-light)`, smooth scroll, `-webkit-font-smoothing`
- **Typography**: heading font-family, tighter letter-spacing on headings, generous line-height on body
- **Mobile-first base styles**: compact spacing, full-width elements, stacked layouts
- **Button overrides**: `.btn-nsi-primary`, `.btn-nsi-accent` — min-height 44px (touch target), border-radius 8px, hover transitions with `transform` and `box-shadow`
- **Card overrides**: border-radius 12px, hover shadow + subtle `translateY(-2px)`, `overflow: hidden` for image zoom
- **Form overrides**: border-radius 8px on inputs, focus ring with `var(--nsi-accent)`, padding for touch comfort
- **Table overrides**: wrapped in `.table-container` with border-radius and overflow, softer header colors
- **Badge overrides**: pill-shaped (`border-radius: 50px`), slightly larger padding
- **Image utilities**: `.img-zoom-wrapper` with `overflow: hidden` + `img:hover { scale: 1.05 }`
- **Section utilities**: `.section-warm` (warm bg), `.section-dark` (dark bg with light text)
- **Animation utilities**: `@keyframes fadeInUp`, `.animate-on-scroll` with staggered delays
- **Responsive enhancements**: `@media (min-width: 768px)` and `(min-width: 992px)` for spacing/typography scaling

### 1.3 Update `wwwroot/css/app.css`
- Update validation styling to match new color scheme
- Update loading spinner to use brand colors

### Files:
- `src/NaturalStoneImpex.Client/wwwroot/index.html`
- `src/NaturalStoneImpex.Client/wwwroot/css/site.css` (NEW)
- `src/NaturalStoneImpex.Client/wwwroot/css/app.css`

---

## Phase 2: Layouts — Navbar, Sidebar, Footer

### 2.1 Public Navbar (`Layout/MainLayout.razor`)
- **Mobile-first**: Hamburger menu is default, horizontal nav appears at `lg`+
- Custom dark background with warm undertone (`var(--nsi-dark)`)
- Brand: larger text with display font, or text-logo treatment
- `sticky-top` with subtle shadow
- Active link: accent-colored bottom border indicator
- Cart icon: accent-colored badge with count
- Mobile menu: full-width dropdown with generous tap targets (48px nav items)

### 2.2 Admin Sidebar (`Layout/AdminLayout.razor`)
- **Mobile-first**: Offcanvas/slide-in sidebar on mobile, fixed sidebar at `lg`+
- Warm dark gradient background
- Active nav item: left border accent + subtle bg highlight
- Hover effects with smooth transitions
- Hamburger trigger button visible on mobile, hidden on `lg`+
- Top bar on mobile shows brand + hamburger

### 2.3 Footer (`Layout/Footer.razor`)
- **Mobile-first**: Single stacked column → 3-column at `md`+
- Company info | Quick links | Contact info
- Warm dark background with subtle top border (accent color)
- Better typography hierarchy with display font for company name

### Files:
- `src/NaturalStoneImpex.Client/Layout/MainLayout.razor`
- `src/NaturalStoneImpex.Client/Layout/AdminLayout.razor`
- `src/NaturalStoneImpex.Client/Layout/Footer.razor`
- `src/NaturalStoneImpex.Client/Layout/LoginLayout.razor`

---

## Phase 3: Public Pages

### 3.1 Home Page (`Pages/Public/Home.razor`)
- **Hero**: Full-width gradient (earthy dark tones), large display-font heading, staggered fade-in animation, accent CTA button with hover scale. Full viewport height on mobile, generous padding on desktop.
- **Features**: Bootstrap Icons in accent-colored circles instead of emojis, card hover lift. Single column on mobile → 3 columns on `md`+
- **Categories**: Cards with gradient overlays (placeholder if no image), overlay text. 1 col mobile → 2 col `sm` → 3 col `md`+
- **Contact summary**: Icon + text in warm-bg section, stacked on mobile → 3 col on `md`+
- Generous section spacing with subtle animated reveals

### 3.2 Catalog Page (`Pages/Public/Catalog.razor`)
- **Mobile-first**: Category filter as horizontal scrollable chips or dropdown on mobile → sidebar at `lg`+
- Search: rounded input with search icon inside, full-width on mobile
- Product grid: 1 col → 2 col `sm` → 3 col `md` → 4 col `xl`
- Better empty state with large icon

### 3.3 Product Detail (`Pages/Public/ProductDetail.razor`)
- **Mobile-first**: Image full-width on top → side-by-side at `md`+
- Image with rounded corners, subtle shadow
- Price: large, accent color, prominent
- Sticky "Add to Cart" bar at bottom on mobile (fixed position)
- Standard button on desktop
- Breadcrumb: subtle, smaller

### 3.4 Cart Page (`Pages/Public/Cart.razor`)
- **Mobile-first**: Card-based layout is primary → table layout at `md`+
- Quantity stepper: styled +/- buttons with touch targets
- Summary card: accent border, sticky on desktop sidebar
- Empty cart: large icon, warm message

### 3.5 Checkout Page (`Pages/Public/Checkout.razor`)
- **Mobile-first**: Stacked form sections → 2-column (form + order summary sidebar) at `lg`+
- Modern input styling, grouped in card sections
- Order summary: collapsible on mobile, always-visible sidebar on desktop

### 3.6 Order Confirmation (`Pages/Public/OrderConfirmation.razor`)
- Success icon/animation (CSS checkmark)
- Clean order summary card, centered layout

### 3.7 Contacts Page (`Pages/Public/Contacts.razor`)
- **Mobile-first**: Stacked info cards → multi-column at `md`+
- Map full-width on mobile
- Better visual separation between sections

### Files:
- All files in `src/NaturalStoneImpex.Client/Pages/Public/`

---

## Phase 4: Shared Components

### 4.1 ProductCard (`Components/ProductCard.razor`)
- Image wrapper with `overflow: hidden`, hover zoom (`scale(1.05)` on image)
- Rounded corners (12px)
- Better price typography: large bold price, smaller muted VAT line
- "Add to cart" button: accent color, icon prefix (`bi-cart-plus`), full-width, 44px min-height
- Out-of-stock: overlay dimming or muted card treatment
- Consistent height with flex layout

### 4.2 Pagination (`Components/Pagination.razor`)
- Rounded buttons, accent active state
- Touch-friendly sizing (min 40px tap targets)
- Hover transitions

### 4.3 CartIcon (`Components/CartIcon.razor`)
- Accent-colored badge
- Pulse animation on count change

### 4.4 ToastNotification (`Components/ToastNotification.razor`)
- Rounded toast, accent/success color
- Slide-in from top animation
- Icon + text layout

### Files:
- All files in `src/NaturalStoneImpex.Client/Components/`

---

## Phase 5: Admin Pages

### 5.1 Login Page (`Pages/Admin/Login.razor`)
- Centered card with shadow on warm background
- Brand header with display font
- Full-width form inputs, accent login button
- Works well on mobile (card adapts to screen width)

### 5.2 Dashboard (`Pages/Admin/Dashboard.razor`)
- Stat cards: 1 col mobile → 2 col `sm` → 4 col `lg`, with large numbers and subtle icon/color coding
- Tables: card-based on mobile → tables on `md`+
- Status pills with distinct colors

### 5.3 Products List & Form (`Pages/Admin/Products.razor`, `ProductForm.razor`)
- Table with rounded wrapper, card-based on mobile
- Action buttons: icon-only with tooltips on desktop, full buttons on mobile
- Form: card sections, modern inputs, full-width on mobile
- Image upload area styling

### 5.4 Categories (`Pages/Admin/Categories.razor`)
- Clean table/card view
- Responsive layout

### 5.5 Orders & Detail (`Pages/Admin/Orders.razor`, `OrderDetail.razor`)
- Status badge pills with distinct colors
- Info sections as cards, stacked on mobile
- Clean detail layout

### 5.6 Invoices (`Pages/Admin/Invoices.razor`, `InvoiceCreate.razor`, `InvoiceDetail.razor`)
- Matching table/form styling from Products
- Responsive

### 5.7 Receipt (`Pages/Admin/Receipt.razor`)
- Keep print-optimized, minimal visual changes (print CSS only)

### Files:
- All files in `src/NaturalStoneImpex.Client/Pages/Admin/`

---

## Execution Order for Claude Code

Work through the phases sequentially. Each phase should be a separate prompt/session:

1. **Phase 1** — Create `site.css` with full mobile-first theme, update `index.html` and `app.css`
2. **Phase 2** — Update all layout files (MainLayout, AdminLayout, Footer, LoginLayout)
3. **Phase 3** — Update all public pages (Home through Contacts)
4. **Phase 4** — Update all shared components (ProductCard, Pagination, CartIcon, Toast)
5. **Phase 5** — Update all admin pages (Login through Receipt)

Each phase ends with `dotnet build` to verify no compilation errors.

---

## Constraints

- **Mobile-first CSS** — write for mobile by default, enhance with `min-width` media queries
- **Keep Bootstrap 5** as the base — we layer on top, not replace
- **No new CSS frameworks** (no Tailwind, no MUI) — only custom CSS + Bootstrap
- **No npm/webpack** — plain CSS files loaded in `index.html`
- **All text stays in Bulgarian** — no text changes unless fixing something
- **No C# logic changes** — this is purely a visual/markup update
- **No data model changes** — same fields, same DTOs, same forms, same API calls
- **Keep all existing functionality** — just change appearance
- **Bootstrap Icons** stay as the icon library
- **Touch-friendly** — minimum 44×44px interactive elements
- **No generic AI aesthetics** — no Inter/Roboto, no purple gradients, no cookie-cutter patterns

---

## Color Palette Reference

| Token | Color | Usage |
|-------|-------|-------|
| `--nsi-primary` | `#8B7355` | Primary brand (headers, links, accents) |
| `--nsi-primary-dark` | `#6B5740` | Hover states, active states |
| `--nsi-accent` | `#D4A853` | CTAs, highlights, badges |
| `--nsi-accent-dark` | `#B8903F` | Accent hover |
| `--nsi-dark` | `#2C2C2C` | Navbar, footer, dark backgrounds |
| `--nsi-darker` | `#1A1A1A` | Admin sidebar |
| `--nsi-light` | `#F8F6F3` | Page backgrounds |
| `--nsi-white` | `#FFFFFF` | Cards, content areas |
| `--nsi-gray` | `#6C757D` | Muted text, borders |
| `--nsi-gray-light` | `#E9E5E0` | Subtle borders, dividers |
| `--nsi-success` | `#4A8C5C` | In stock, success states |
| `--nsi-danger` | `#C0392B` | Out of stock, errors |
| `--nsi-warning` | `#E67E22` | Low stock, pending states |

---

## Verification

After each phase:
1. Run `dotnet build` — must compile without errors
2. Run the app (`dotnet run --project src/NaturalStoneImpex.Api` + `dotnet run --project src/NaturalStoneImpex.Client`)
3. **Test mobile first**: Chrome DevTools → device toolbar → iPhone SE (375px), then Galaxy S8 (360px)
4. Test tablet: iPad (768px)
5. Test desktop: 1920px
6. Verify no broken layouts, missing text, or non-functional buttons
7. Test all touch targets are at least 44×44px on mobile
8. Test admin login and navigation still works
9. Verify print receipt still renders correctly
10. Use Chrome DevTools responsive mode to test intermediate sizes (e.g., 480px, 600px, 900px)
