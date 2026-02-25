# Design Phase 1: Foundation — Theme & Global Styles

## Prerequisites

- All 7 logic phases completed and working
- Application runs without errors
- Fresh Claude Code session

---

## Session D1.1 — Google Fonts + Master Theme CSS

### Prompt 1 (Plan)
```
Read CLAUDE.md, docs/planning/design-plan.md (full document), and
.claude/skills/frontend-design/SKILL.md.

I want to implement Design Phase 1 — create the CSS foundation for a
complete visual redesign of the application.

The current app uses plain Bootstrap 5 with almost no custom CSS. We need
to transform it into a distinctive, premium natural stone brand identity.

Before writing any code, tell me:
- What Google Fonts pairing will you choose? (Must NOT be Inter, Roboto,
  Arial, Playfair Display, or Space Grotesk — these are banned as generic.
  The display font should feel "stone/earth/craft" — solid, grounded.
  The body font should be clean and readable.)
- Show me the full CSS custom properties you plan to define
- What global overrides will you apply to Bootstrap components?
- What custom utility classes will you create?
- What animations/keyframes will you define?
- How will you ensure mobile-first CSS (min-width media queries only)?

Don't write any code yet.
```

> **Wait for response. Verify:**
> - Font choices are distinctive, not generic — Google them to check they look good
> - Color variables match the palette in design-plan.md
> - CSS is structured mobile-first (base styles for mobile, `@media (min-width: ...)` for desktop)
> - No `max-width` media queries (that's desktop-first, not mobile-first)
> - Button overrides include min-height 44px for touch targets
> - Card overrides include border-radius 12px and hover effects
> - Animations are CSS-only (no JS libraries)
>
> **If font choices seem wrong, suggest alternatives:**
> ```
> I don't like [font]. Consider these alternatives for display:
> DM Serif Display, Cormorant Garamond, Libre Baskerville, Lora, Merriweather.
> For body: DM Sans, Source Sans 3, Outfit, Nunito Sans, Libre Franklin.
> Pick one that feels premium and earthy, not generic.
> ```
>
> **Then proceed:**

### Prompt 2 (Execute)
```
The plan looks good. Proceed with the implementation:

1. Update wwwroot/index.html:
   - Add Google Fonts <link> tags (with display=swap) for both chosen fonts
   - Add <link href="css/site.css" rel="stylesheet" /> AFTER app.css
   - Keep all existing links (Bootstrap CSS, Bootstrap Icons, app.css) unchanged

2. Create wwwroot/css/site.css — the master theme file. Structure it as:

   a) CSS Custom Properties (:root block):
      - All colors from design-plan.md Color Palette Reference
      - Font family variables (--nsi-font-display, --nsi-font-body)
      - Spacing variables (--nsi-radius-sm: 8px, --nsi-radius-md: 12px, --nsi-radius-lg: 16px)
      - Shadow variables (--nsi-shadow-sm, --nsi-shadow-md, --nsi-shadow-lg)
      - Transition variable (--nsi-transition: all 0.3s ease)

   b) Global base styles (mobile-first):
      - body: font-family var(--nsi-font-body), background var(--nsi-light),
        color var(--nsi-dark), smooth scroll, -webkit-font-smoothing
      - h1-h6: font-family var(--nsi-font-display), letter-spacing -0.02em
      - a: color var(--nsi-primary), transition

   c) Button overrides:
      - .btn-nsi-primary: bg var(--nsi-primary), white text, min-height 44px,
        border-radius var(--nsi-radius-sm), hover with darker bg + translateY(-1px) + shadow
      - .btn-nsi-accent: bg var(--nsi-accent), dark text, same hover pattern
      - .btn-nsi-outline: transparent bg, primary border, hover fill
      - Override .btn: add transition var(--nsi-transition)

   d) Card overrides:
      - .card: border-radius var(--nsi-radius-md), border-color var(--nsi-gray-light),
        transition var(--nsi-transition)
      - .card:hover (for clickable cards): translateY(-2px) + shadow-md
      - .card-img-top: border-radius top only matching card

   e) Form overrides:
      - .form-control, .form-select: border-radius var(--nsi-radius-sm),
        border-color var(--nsi-gray-light), padding 0.625rem 1rem (touch-friendly)
      - .form-control:focus: border-color var(--nsi-accent), box-shadow with accent color ring

   f) Table overrides:
      - .table-container: new class, bg white, border-radius var(--nsi-radius-md),
        overflow hidden, shadow-sm
      - .table thead: bg var(--nsi-primary) with white text (instead of table-dark)

   g) Badge overrides:
      - .badge: border-radius 50px (pill), slightly larger padding
      - .badge-nsi-accent: bg var(--nsi-accent), dark text
      - .badge-nsi-success: bg var(--nsi-success), white text
      - .badge-nsi-danger: bg var(--nsi-danger), white text

   h) Section utilities:
      - .section-warm: bg var(--nsi-light), padding 3rem 0 (mobile), 5rem 0 (desktop)
      - .section-dark: bg var(--nsi-dark), color white, same padding
      - .section-white: bg white, same padding

   i) Image utilities:
      - .img-zoom-wrapper: overflow hidden, border-radius var(--nsi-radius-md)
      - .img-zoom-wrapper img: transition transform 0.4s ease
      - .img-zoom-wrapper:hover img: transform scale(1.05)

   j) Animation keyframes and utilities:
      - @keyframes fadeInUp: from { opacity: 0; translateY(20px) } to { opacity: 1; translateY(0) }
      - @keyframes fadeIn: from { opacity: 0 } to { opacity: 1 }
      - .animate-fade-in-up: animation fadeInUp 0.6s ease forwards
      - .animate-stagger-1 through .animate-stagger-5: increasing animation-delay (0.1s increments)
      - .animate-fade-in: animation fadeIn 0.4s ease forwards

   k) Responsive enhancements:
      - @media (min-width: 768px): increase section padding, heading sizes
      - @media (min-width: 992px): further increases for desktop

   l) Navbar utility (for later use):
      - .navbar-nsi: bg var(--nsi-dark), box-shadow, sticky
      - .navbar-nsi .nav-link: color rgba(255,255,255,0.8), hover white
      - .navbar-nsi .nav-link.active: color white, border-bottom accent

   m) Sidebar utility (for later use):
      - .sidebar-nsi: bg var(--nsi-darker), min-height 100vh
      - .sidebar-nsi .nav-link: color rgba(255,255,255,0.7), hover with bg change
      - .sidebar-nsi .nav-link.active: left border accent, bg slightly lighter

3. Update wwwroot/css/app.css:
   - Change validation .valid outline color to var(--nsi-success)
   - Change validation .invalid outline color to var(--nsi-danger)
   - Change loading spinner stroke to var(--nsi-primary)

Do NOT modify any .razor files yet — this phase is CSS-only.
Make sure all CSS is mobile-first (base = mobile, min-width queries for desktop).
```

### Verify
```bash
dotnet build
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Check in browser:
- [ ] App loads without CSS errors (check console)
- [ ] Background color changed from white to warm off-white (#F8F6F3)
- [ ] Font changed from default Bootstrap to the chosen Google Fonts
- [ ] Headings use the display font, body text uses the body font
- [ ] Existing cards have rounded corners (12px)
- [ ] Form inputs have rounded corners and accent-colored focus rings
- [ ] Buttons have hover transitions (translateY + shadow)
- [ ] No layout breakage — all existing pages still render correctly
- [ ] Check mobile (375px) — no horizontal overflow, all text readable
- [ ] Check desktop (1920px) — all sections appropriately spaced
- [ ] Inspect site.css — confirm no max-width media queries (mobile-first only)

### Commit
```bash
git add src/NaturalStoneImpex.Client/wwwroot/css/site.css src/NaturalStoneImpex.Client/wwwroot/css/app.css src/NaturalStoneImpex.Client/wwwroot/index.html
git commit -m "Design Phase 1: CSS foundation — theme variables, fonts, component overrides, animations"
```

---

## Design Phase 1 Complete ✅

At this point you should have:
- ✅ Google Fonts loaded (distinctive display + body fonts)
- ✅ site.css with full CSS custom properties, component overrides, animations
- ✅ Warm off-white page background
- ✅ Brand colors applied globally via variables
- ✅ Cards, buttons, forms, tables, badges all restyled
- ✅ Animation utilities ready for use in subsequent phases
- ✅ Navbar and sidebar utility classes ready
- ✅ All existing pages still functional (no layout breakage)
- ✅ Mobile-first CSS structure

**Next**: Design Phase 2 — Layouts (Navbar, Sidebar, Footer). Start a fresh Claude Code session.

---

## Troubleshooting

### If Google Fonts don't load:
```
The custom fonts are not loading. Check that the Google Fonts <link> tags
in index.html have the correct family names and weights. Open the font URL
directly in a browser to verify it returns CSS. Make sure display=swap is set.
```

### If CSS variables aren't applying:
```
The CSS custom properties from site.css are not being applied. Check that:
1. site.css is linked AFTER bootstrap.min.css in index.html
2. The :root block has the correct variable names
3. The CSS specificity is high enough to override Bootstrap defaults
If needed, use more specific selectors or check for typos in variable names.
```

### If cards or buttons don't show new styles:
```
The Bootstrap component overrides in site.css are not taking effect.
Bootstrap has high specificity on some classes. Make sure our overrides
use equal or higher specificity. For example, use .card instead of just
overriding properties. If needed, check that site.css loads after
bootstrap.min.css in index.html.
```

### If mobile layout breaks:
```
There's horizontal overflow on mobile. Check that no element has a fixed
width that exceeds the viewport. Use Chrome DevTools device toolbar at
375px and look for elements extending beyond the viewport boundary.
Common culprits: fixed-width tables, images without max-width: 100%,
containers with horizontal padding that adds to width.
```

### If animations are janky:
```
The CSS animations are not smooth. Make sure animations only transform
opacity and transform properties (these are GPU-accelerated). Avoid
animating width, height, margin, padding, or top/left. Use translateY
instead of top, and scale instead of width/height changes.
```
