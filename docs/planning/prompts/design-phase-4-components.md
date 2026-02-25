# Design Phase 4: Shared Components Redesign

## Prerequisites

- Design Phases 1-3 completed (theme CSS, layouts, all public pages)
- Application runs without errors
- Fresh Claude Code session

---

## Session D4.1 — ProductCard, Pagination, CartIcon, ToastNotification

### Prompt 1
```
Read CLAUDE.md, docs/planning/design-plan.md (Phase 4 section), and
.claude/skills/frontend-design/SKILL.md.

Read ALL component files:
- src/NaturalStoneImpex.Client/Components/ProductCard.razor
- src/NaturalStoneImpex.Client/Components/Pagination.razor
- src/NaturalStoneImpex.Client/Components/CartIcon.razor
- src/NaturalStoneImpex.Client/Components/ToastNotification.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign ALL FOUR shared components. Visual-only — keep ALL data bindings,
event handlers, parameters, CartService calls, and @code blocks unchanged.

**ProductCard.razor:**
- Card: border-0, shadow-sm, border-radius var(--nsi-radius-md), overflow hidden
  (for image zoom), hover: translateY(-4px) + shadow-md transition
- Image area: wrapped in .img-zoom-wrapper so hover zooms the image.
  Fixed height (200px mobile, 220px desktop via CSS), object-fit: cover.
  "Няма снимка" placeholder: warm background (var(--nsi-light)), centered
  Bootstrap Icon (bi-image, large, muted), rounded top matching card
- Card body: d-flex flex-column for consistent heights
- Product name link: dark text, no underline, hover shows primary color
  Truncate to 2 lines with CSS line-clamp if name is long
- Price: large, bold, accent color for the main price (PriceWithVat)
  VAT info: smaller, muted, below main price
- Unit badge: pill-shaped (.badge-nsi-accent or light accent bg)
- "Добави в количката" button: .btn-nsi-accent, full-width, with
  bi-cart-plus icon prefix, min-height 44px for touch. Hover scale effect.
- Out-of-stock treatment: the "Изчерпан" badge — make it full-width pill,
  muted red background. Optionally add a subtle overlay or reduced opacity
  on the entire card when out of stock.
- All text and data bindings stay exactly the same

**Pagination.razor:**
- Page buttons: rounded (border-radius 8px or pill), min-size 40×40px
  for touch targets
- Active page: bg var(--nsi-accent), dark text, slight shadow
- Inactive pages: light bg, hover with primary color border
- Previous/Next arrows: same sizing as page buttons
- Disabled state: muted, no pointer cursor
- Centered with appropriate margin-top
- Keep all @code logic (page calculation, ellipsis) unchanged

**CartIcon.razor:**
- Cart icon: use bi-cart3 or bi-bag (Bootstrap Icon) instead of emoji if
  currently using emoji. If already using bi-cart, just restyle.
- Badge: .badge-nsi-accent (gold background, dark text), positioned with
  translate for overlap effect (position: absolute, top: -8px, right: -8px)
- Subtle pulse animation on badge when count changes (CSS @keyframes pulse)
- Icon color: white (inherits from navbar), hover: var(--nsi-accent) transition
- Keep all CartService subscriptions and @code unchanged

**ToastNotification.razor:**
- Toast wrapper: positioned top-right (or top-center on mobile)
- Toast card: rounded (var(--nsi-radius-md)), bg white, shadow-lg,
  accent-colored left border (4px solid var(--nsi-success))
- Icon: bi-check-circle-fill in success green
- Text: clean, readable
- Slide-in animation from right (or fade-in from top)
- Auto-dismiss still works as before (2 seconds)
- Mobile: full-width at top of screen with padding
- Keep all @code logic (Show method, timer, visibility) unchanged
```

### Verify

**ProductCard:**
- [ ] Card has rounded corners, hover lift + shadow
- [ ] Image zooms on hover (scale effect)
- [ ] "Няма снимка" placeholder styled with icon
- [ ] Price display: large accent price, muted VAT info
- [ ] "Добави в количката" button: accent, icon, full-width, 44px height
- [ ] Out-of-stock products visually muted with red badge
- [ ] Cards in catalog grid all have consistent heights
- [ ] Mobile (375px): cards look good full-width
- [ ] Click product name → navigates to detail page

**Pagination:**
- [ ] Buttons rounded, touch-friendly (40×40px)
- [ ] Active page has accent background
- [ ] Hover effects on inactive pages
- [ ] Previous/Next work correctly
- [ ] Centered below grid

**CartIcon:**
- [ ] Icon displays correctly in navbar
- [ ] Badge shows item count in accent gold
- [ ] Badge positioned with overlap effect
- [ ] Badge hidden when cart empty (0 items)
- [ ] Add item from catalog → badge updates immediately
- [ ] Click icon → navigates to /cart

**ToastNotification:**
- [ ] Toast appears top-right (or top-center mobile) on "Add to cart"
- [ ] Has slide-in animation
- [ ] Accent left border + success icon
- [ ] Auto-dismisses after 2 seconds
- [ ] Does not block interaction with page

### Commit
```bash
git add src/NaturalStoneImpex.Client/Components/ProductCard.razor src/NaturalStoneImpex.Client/Components/Pagination.razor src/NaturalStoneImpex.Client/Components/CartIcon.razor src/NaturalStoneImpex.Client/Components/ToastNotification.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 4: Shared components — ProductCard, Pagination, CartIcon, Toast redesign"
```

---

## Design Phase 4 Complete ✅

At this point all shared components should have the new design:
- ✅ ProductCard: image zoom, accent pricing, branded button, out-of-stock styling
- ✅ Pagination: rounded, touch-friendly, accent active state
- ✅ CartIcon: styled icon, accent badge with overlap, pulse animation
- ✅ ToastNotification: slide-in, success border, auto-dismiss

**Next**: Design Phase 5 — Admin Pages. Start a fresh Claude Code session.

---

## Troubleshooting

### If image zoom doesn't work:
```
The image hover zoom effect is not working. Make sure the image is wrapped
in a .img-zoom-wrapper div that has overflow: hidden. The img inside must
have transition: transform 0.4s ease. The hover rule should be on the
wrapper: .img-zoom-wrapper:hover img { transform: scale(1.05); }
Check that the wrapper has the correct border-radius matching the card.
```

### If card heights are inconsistent:
```
Product cards in the grid have different heights. Make sure the card uses
h-100 class and the card-body has d-flex flex-column. The "Add to cart"
button area should have mt-auto to push it to the bottom. All images
should have the same fixed height with object-fit: cover.
```

### If pagination buttons are too small on mobile:
```
The pagination buttons are too small for touch on mobile. Set min-width
and min-height to 40px on the page-link class. Add padding of at least
0.5rem 0.75rem. Test on actual touch device or device toolbar.
```

### If CartIcon badge animation causes layout shift:
```
The cart badge animation is causing layout jump. Use position: absolute
on the badge relative to the cart icon wrapper (position: relative).
This takes the badge out of normal flow and prevents layout shifts.
The animation should only transform opacity and scale, not dimensions.
```

### If toast overlaps navbar on mobile:
```
The toast notification is appearing behind or overlapping the sticky navbar.
Set the toast container z-index higher than the navbar (navbar is typically
z-index: 1020-1030, so use 1050+ for the toast). On mobile, consider
positioning the toast below the navbar with top: 70px instead of top: 0.
```
