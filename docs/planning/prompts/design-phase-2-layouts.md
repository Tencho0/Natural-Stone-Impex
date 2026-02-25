# Design Phase 2: Layouts — Navbar, Sidebar, Footer

## Prerequisites

- Design Phase 1 completed (site.css with theme, Google Fonts loaded)
- Application runs without errors
- Fresh Claude Code session

---

## Session D2.1 — Public Navbar (MainLayout.razor)

### Prompt 1
```
Read CLAUDE.md, docs/planning/design-plan.md, and
.claude/skills/frontend-design/SKILL.md.

Also read these files to understand the current state:
- src/NaturalStoneImpex.Client/Layout/MainLayout.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign the public navigation bar in MainLayout.razor. This is a visual-only
change — keep ALL existing links, the CartIcon component, and the navigation
structure exactly the same. Do NOT change any C# code or navigation logic.

Requirements:
- Mobile-first: hamburger menu is the default layout, horizontal nav appears at lg+
- Use the .navbar-nsi class from site.css (or update it if needed)
- Background: var(--nsi-dark) with a subtle warm undertone — not pure black
- Brand: "Natural Stone Impex" using the display font (var(--nsi-font-display)),
  larger and more prominent, with a subtle gold accent on "Stone" or a decorative
  element (like a small diamond ◆ separator)
- Sticky navbar: sticky-top with a subtle box-shadow
- Nav links: clean, spaced, with hover effect (underline slide-in or color transition)
- Active link indicator: bottom border using var(--nsi-accent) instead of Bootstrap default
- Cart icon area: the CartIcon component stays, but style the badge with var(--nsi-accent)
- Mobile hamburger: styled toggle button, full-width dropdown with generous tap targets
  (min-height 48px per nav item)
- Smooth transition on the collapse animation
- Add appropriate spacing between nav items

Also update the <main> container:
- Remove the mt-4 class and instead use appropriate padding that accounts
  for the sticky navbar height
- Consider using container-lg or container-xl instead of container for wider layouts

Do NOT change the CartIcon component itself — that's Phase D4.
Do NOT change the Footer component — that's later in this session.
Keep all Bulgarian text exactly as-is.
```

### Verify
- [ ] Navbar has warm dark background, not pure black
- [ ] Brand text uses display font, is prominent
- [ ] Nav links have hover effects and active indicators
- [ ] Sticky — scrolls with the page, shadow visible
- [ ] Mobile (375px): hamburger works, dropdown items are 48px+ tall, easy to tap
- [ ] Desktop (1920px): horizontal nav, well-spaced
- [ ] CartIcon still visible and functional
- [ ] All navigation links still work correctly
- [ ] No layout shift or content hidden behind sticky nav

### Commit
```bash
git add src/NaturalStoneImpex.Client/Layout/MainLayout.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 2.1: Public navbar redesign — sticky, warm dark, display font brand"
```

---

## Session D2.2 — Admin Sidebar (AdminLayout.razor)

### Prompt 1
```
Read docs/planning/design-plan.md (Phase 2 section) and
.claude/skills/frontend-design/SKILL.md.

Also read:
- src/NaturalStoneImpex.Client/Layout/AdminLayout.razor
- src/NaturalStoneImpex.Client/wwwroot/css/site.css

Redesign the admin layout sidebar. Keep ALL existing nav items, the logout
button, the "Към сайта" link, and all C# code (HandleLogout) exactly as-is.
This is visual-only.

Requirements:

Mobile-first approach:
- On mobile (< lg): sidebar is HIDDEN by default. Show a top bar with:
  - Brand "NSI Admin" or "Natural Stone Impex" (compact)
  - Hamburger button on the right
  - Clicking hamburger opens the sidebar as a Bootstrap offcanvas (slide-in
    from left) or a togglable overlay
  - Clicking a nav item in the mobile sidebar closes it and navigates
- On desktop (lg+): sidebar is permanently visible on the left (250px wide),
  content fills the remaining space

Sidebar styling:
- Use .sidebar-nsi class from site.css (or update it)
- Background: var(--nsi-darker) or a subtle dark gradient
- Brand area at top: "Natural Stone Impex" with display font, "Администрация"
  subtitle in muted color — same text as current but better styled
- Nav items: icon + text, with smooth hover effect (background color transition,
  slight indent or left border animation)
- Active nav item: left border 3px solid var(--nsi-accent) + slightly lighter
  background + white text (instead of just text-light on everything)
- Divider lines: subtle, using var(--nsi-gray) at low opacity
- Logout button at bottom: styled with var(--nsi-accent) outline or a distinct
  style that makes it clearly different from nav items
- "Към сайта" link: subtle styling, external-link feel

Top content bar (the "Административен панел" bar):
- Lighter, cleaner styling
- On mobile: this becomes the main top bar with hamburger
- On desktop: subtle light bar with breadcrumb or page title area

Keep all Bootstrap Icons (bi bi-speedometer2, etc.) exactly as-is.
Keep all NavLink components and their href values unchanged.
Keep the HandleLogout code unchanged.
```

### Verify
- [ ] Desktop (992px+): sidebar visible permanently, 250px wide, content fills rest
- [ ] Active nav item has accent left border and highlighted background
- [ ] Hover on nav items shows smooth background transition
- [ ] Logout button distinct from nav items, positioned at bottom
- [ ] Mobile (375px): NO sidebar visible by default
- [ ] Mobile: top bar visible with brand + hamburger
- [ ] Mobile: hamburger opens sidebar as overlay/offcanvas
- [ ] Mobile: tapping nav item closes sidebar and navigates
- [ ] All admin pages still accessible and rendering correctly
- [ ] Logout still works
- [ ] "Към сайта" link still works

### Commit
```bash
git add src/NaturalStoneImpex.Client/Layout/AdminLayout.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 2.2: Admin sidebar — mobile offcanvas, active indicators, warm dark gradient"
```

---

## Session D2.3 — Footer + Login Layout

### Prompt 1
```
Read docs/planning/design-plan.md (Phase 2 section).

Read these files:
- src/NaturalStoneImpex.Client/Layout/Footer.razor
- src/NaturalStoneImpex.Client/Layout/LoginLayout.razor

Redesign BOTH components. Visual-only — keep all existing text and data.

**Footer.razor:**
- Mobile-first: single stacked column on mobile → 3 columns at md+
- Column 1 — Company: "Natural Stone Impex" in display font, short tagline
  "Качествени строителни материали" (already exists on home page, reuse),
  copyright line at bottom
- Column 2 — Quick links: Каталог, Контакти (use <a> tags linking to
  /catalog and /contacts)
- Column 3 — Contact: Phone number, address (reuse existing text from Footer)
- Background: var(--nsi-dark), matching the navbar
- Top border: 3px solid var(--nsi-accent) as accent divider
- Text: rgba(255,255,255,0.7) for body, white for headings
- Keep the existing phone number and copyright text exactly as-is
- Remove the emoji 📞 — use Bootstrap Icons instead (bi bi-telephone)
- Generous padding: py-4 on mobile, py-5 on desktop
- Remove the mt-5 from the current footer — the main content padding should
  handle spacing (or use a minimal mt-auto approach if needed)

**LoginLayout.razor:**
- Background: var(--nsi-light) warm off-white (instead of bg-light gray)
- Centered card layout (keep existing behavior)
- The card itself: shadow-lg, border-radius var(--nsi-radius-lg), no border
- Add the brand "Natural Stone Impex" above the card in display font
- Subtle accent line or decorative element
- Works on all screen sizes (already centered, just ensure card max-width
  is responsive — ~400px max on desktop, full-width with padding on mobile)

Do NOT modify the login form itself or any C# code — that's Phase D5.
```

### Verify

**Footer:**
- [ ] 3 columns on desktop (md+), stacked on mobile
- [ ] Display font for company name
- [ ] Quick links present and functional
- [ ] Contact info present with Bootstrap Icons (no emojis)
- [ ] Accent top border visible
- [ ] Warm dark background matching navbar
- [ ] Mobile (375px): stacked, readable, well-spaced
- [ ] No mt-5 gap above footer — smooth transition from content

**Login Layout:**
- [ ] Warm off-white background
- [ ] Card has large shadow, rounded corners
- [ ] Brand text above card
- [ ] Mobile (375px): card takes full width with padding
- [ ] Desktop: card centered, ~400px max width
- [ ] Login form still works (test admin/Admin123!)

### Commit
```bash
git add src/NaturalStoneImpex.Client/Layout/Footer.razor src/NaturalStoneImpex.Client/Layout/LoginLayout.razor src/NaturalStoneImpex.Client/wwwroot/css/site.css
git commit -m "Design Phase 2.3: Footer 3-column layout, login page warm redesign"
```

---

## Design Phase 2 Complete ✅

At this point you should have:
- ✅ Public navbar: sticky, warm dark, display font brand, active indicators, mobile hamburger
- ✅ Admin sidebar: mobile offcanvas, desktop permanent, active nav with accent border
- ✅ Footer: 3-column layout, accent top border, Bootstrap Icons, display font
- ✅ Login layout: warm background, branded card, shadow

**Next**: Design Phase 3 — Public Pages. Start a fresh Claude Code session.

---

## Troubleshooting

### If sticky navbar hides content behind it:
```
Content is hidden behind the sticky navbar. Add padding-top to the main
content area equal to the navbar height (typically 56-70px). Use
body { padding-top: var(--navbar-height); } or add pt-5 to the main container.
Check that the sticky-top class is on the nav element.
```

### If mobile offcanvas sidebar doesn't work:
```
The admin sidebar offcanvas is not opening/closing on mobile. In Blazor,
Bootstrap's JS-driven offcanvas may not work with NavigationManager.
Consider using a CSS-only approach with a checkbox toggle, or use Blazor's
@onclick to toggle a boolean that adds/removes a CSS class to show/hide
the sidebar with a CSS transition. Show me the current implementation.
```

### If footer is not sticking to bottom on short pages:
```
The footer is floating in the middle on pages with little content.
Use a flexbox approach on the page wrapper: min-height 100vh, display flex,
flex-direction column, and flex-grow 1 on the main content. This pushes
the footer to the bottom regardless of content height.
```

### If active nav link highlighting doesn't work:
```
The active nav link is not getting the accent border. Blazor's NavLink
component adds an "active" class when the route matches. Make sure the
CSS selector targets .nav-link.active (not just .active). Check that
NavLinkMatch.All is set correctly for the dashboard (exact match) vs
other pages (prefix match).
```
