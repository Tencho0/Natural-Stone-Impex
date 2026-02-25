---
name: frontend-design
description: Create distinctive, production-grade frontend interfaces with high design quality. Use this skill when the user asks to build or restyle web components, pages, or layouts. Generates creative, polished code that avoids generic AI aesthetics. Tailored for Blazor WebAssembly + Bootstrap 5.
---

This skill guides creation of distinctive, production-grade frontend interfaces that avoid generic "AI slop" aesthetics. Implement real working code with exceptional attention to aesthetic details and creative choices.

The user provides frontend requirements: a component, page, application, or interface to build or restyle. They may include context about the purpose, audience, or technical constraints.

## Project Context — Natural Stone Impex

This is a **Bulgarian building materials shop** — a real business storefront selling natural stone, cement, tiles. The audience is Bulgarian builders, contractors, and homeowners.

- **Tech stack**: Blazor WebAssembly (Standalone) + Bootstrap 5 + custom CSS
- **Aesthetic direction**: **Organic/natural + luxury/refined** — earthy, solid, trustworthy, premium but approachable. Think high-end stone supplier, not generic e-commerce.
- **Color palette**: Warm stone/earth tones — bronze primary (#8B7355), gold accent (#D4A853), charcoal dark (#2C2C2C), warm off-white (#F8F6F3)
- **All UI text is in Bulgarian** — do not change any text content
- **Mobile-first design** — write CSS for mobile by default, use `min-width` media queries to enhance for larger screens
- **Data structure is frozen** — do not add, remove, or rename any fields, forms, DTOs, or API calls

## Design Thinking

Before coding, understand the context and commit to a clear aesthetic direction:
- **Purpose**: What problem does this interface solve? Who uses it?
- **Tone**: For this project, the tone is **organic/natural + luxury/refined** — the warmth of natural stone, the trust of a professional supplier, the clarity of a well-organized catalog. Not experimental or wild — intentional and polished.
- **Constraints**: Blazor WebAssembly, Bootstrap 5 base, no npm/webpack, CSS-only animations, mobile-first.
- **Differentiation**: What makes this feel like a **stone supplier** and not a generic Bootstrap template?

**CRITICAL**: Execute the organic/natural luxury direction with precision. The key is intentionality — every color, font, shadow, and spacing choice should feel like it belongs to a premium building materials brand.

Then implement working code (Blazor .razor + CSS) that is:
- Production-grade and functional
- Visually striking and memorable
- Cohesive with the natural stone brand identity
- Meticulously refined in every detail
- **Mobile-first** — works beautifully on phones first, then enhances for desktop

## Frontend Aesthetics Guidelines

Focus on:

- **Typography**: Choose fonts that are beautiful, unique, and interesting. Avoid generic fonts like Arial, Inter, and Roboto; opt instead for distinctive choices that fit a premium natural materials brand. Pair a characterful serif/display font (for headings) with a refined sans-serif body font. Think: stone-carved solidity for headings, clean readability for body.

- **Color & Theme**: Commit to the warm earth/stone palette. Use CSS variables for consistency. The bronze primary with gold accent dominates — not timid, evenly-distributed pastels. Dark charcoal backgrounds create atmosphere. Warm off-white (#F8F6F3) replaces pure white for a softer feel.

- **Motion**: Use CSS-only animations (no JS animation libraries in Blazor context). Prioritize high-impact moments: one well-orchestrated page load with staggered `fadeInUp` reveals (using `animation-delay`) creates more delight than scattered micro-interactions. Hover states on cards should lift and shadow subtly. Buttons should have smooth `transform` + `box-shadow` transitions.

- **Spatial Composition**: Generous negative space on desktop. Controlled density on mobile. Cards with rounded corners (12px). Sections with distinct backgrounds that create rhythm (warm → white → warm → dark). Use vertical spacing generously between sections.

- **Backgrounds & Visual Details**: Create atmosphere and depth. Use subtle gradients instead of flat solid colors. The hero section should have a dramatic gradient that evokes dark stone. Cards float with layered shadows. Use the `--nsi-light` off-white as page background for warmth. Add subtle border treatments and accent-colored dividers.

- **Touch & Mobile**: All interactive elements minimum 44×44px. Single-column layouts by default. Full-width buttons on mobile. Sticky action bars where appropriate (e.g., Add to Cart on product detail). Swipeable/scrollable where natural.

## What NOT to Do

**NEVER** use:
- Overused font families (Inter, Roboto, Arial, system-ui, sans-serif stack)
- Cliched color schemes (purple gradients, blue-on-white generic SaaS look)
- Predictable Bootstrap layouts with no customization (default card, default navbar, default table)
- Cookie-cutter design that could belong to any store
- Pure white (#FFFFFF) backgrounds everywhere — use the warm off-white
- Flat, shadowless, textureless designs — add depth
- Generic emoji for icons — use Bootstrap Icons consistently

**NEVER** modify:
- Data models, DTOs, entities, or API endpoints
- Form fields — same fields, same labels, same validation
- C# business logic or services
- Bulgarian text content

## Implementation Notes for Blazor + Bootstrap 5

- Custom CSS goes in `wwwroot/css/site.css`, loaded after Bootstrap in `index.html`
- Use Bootstrap's grid system (`row`, `col-*`) but override colors/spacing with custom CSS
- Use CSS custom properties (`var(--nsi-primary)`) for all brand colors
- Blazor components use `class="..."` attributes — add custom CSS classes alongside Bootstrap ones
- For responsive behavior: Bootstrap breakpoint classes (`col-md-4`, `d-lg-none`) + custom `@media (min-width: ...)` in CSS
- Keep `@code {}` blocks unchanged — only modify the HTML markup and CSS classes
- Use `<style>` scoped blocks in `.razor` files sparingly — prefer shared CSS in `site.css`

**IMPORTANT**: Match implementation complexity to the aesthetic vision. This is a refined, professional design — it needs restraint, precision, and careful attention to spacing, typography, and subtle details. Elegance comes from executing the vision well, not from adding more effects.
