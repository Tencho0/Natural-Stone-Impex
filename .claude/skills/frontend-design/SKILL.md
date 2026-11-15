---
name: frontend-design
description: Design rules for Natural Stone Impex (Direction A 'Образец'). Use for any UI work in this repo.
---

# Natural Stone Impex design rules (Direction A "Образец")

Full spec: `docs/superpowers/specs/2026-11-07-storefront-redesign-ssr-design.md` (sections 5.2, 6, 7.10). If this file and the spec disagree, the spec wins.

## Design read and dials

The site reads as a materials library: each stone is a labelled sample on a cool mineral ground. Photography carries the warmth. The interface stays quiet and precise.

- Variance 6, motion 2.
- Density 4 on the storefront, 7 in the admin.

## Tokens

```
--paper        #E8E9E6   page ground
--surface      #F4F5F3   inputs, raised panels, admin content
--ink          #1C2023   text
--stone        #5A6165   secondary text
--rule         #C7CBCA   hairlines, input borders
--accent       #2F4A5C   primary action, active state, cart count
--accent-hover #253C4B
--on-accent    #F4F5F3
--ok #3D6B4A   --warn #8A6418   --danger #A1352B   (text, borders, small markers; never large fills)
```

- One accent. No gradients.
- Bootstrap is themed through its `--bs-*` variables, so its utilities keep working.
- Radius 2px everywhere. No pills.
- Groups are separated by 1px `--rule` hairlines. No card shadows. One shadow token exists, for floating menus only.
- Spacing scale (px): 4, 8, 12, 16, 20, 24, 32, 48, 56, 72.
- Container max width 1200px. Gutter 20px on phones, 32px on desktop.
- Interactive targets are at least 44px.
- Type scale (px): 13, 14, 15 (body), 16, 18, 22, 26, 34, 48 (hero; 34 on phones). Body line-height 1.55. Text measure under 70 characters.

## Fonts

- Headlines: Geologica 300, 400, 500. Tracking -0.02 to -0.03 em, `text-wrap: balance`.
- Everything else: Wix Madefor Text 400, 500, 600.
- Both faces have Bulgarian letterforms through `locl` (`BGR`). They only activate when the page declares `<html lang="bg">`, so every page must.
- Self-hosted `woff2`, Latin + Cyrillic subset with `locl` kept, `font-display: swap`, metric-matched fallback `@font-face` rules. Nothing loads from Google or jsDelivr.
- No monospace face. Prices, quantities, order numbers and table numbers use `font-variant-numeric: tabular-nums`.
- Sentence case. No all-caps labels.

## Motion

- Easing `cubic-bezier(0.2, 0.7, 0.2, 1)`. Durations: 120ms press, 160ms hover and colour, 240ms menus.
- Motion only answers an action: button press (1px down), cart count change, menu open, toast.
- No entrance or scroll animations. `prefers-reduced-motion` turns transitions off.

## Components

- Photography: square crops in grids, 4:3 on the product page, `object-fit: cover`. Captions sit under the photo, never over it.
- Header: wordmark "Natural Stone Impex" in Geologica 500. Links: Каталог, Визуализатор, Контакти, Количка with a count. The active link gets a 2px accent underline. Phones get a "Меню" toggle.
- Product card: square photo; meta line (category left, unit right); name; price "20.45 €" with "за м², с ДДС"; a 40px square "+" button with `aria-label="Добави в количката"` that posts quantity 1. When priced 0, show "Цена при запитване" and an "Запитване" link to Contacts, with no add button. Stock does not change the card.
- Buttons: primary has the accent fill, secondary a 1px ink outline, tertiary is an underlined link. Minimum height 44px.
- Inputs: label above, helper text below, and an error replaces the helper in the same place. Minimum height 44px. Visible 2px accent focus ring.
- Segmented switch: for "По размери / Знам м²", "Физическо лице / Фирма" and the delivery method.
- Toast: one line ("Добавено в количката" plus a "Към количката" link), 4s, `role="status"`.
- Skeletons: only where content loads after first paint (admin, visualizer). Show after 300ms, never for faster responses.
- Icons: Bootstrap Icons, self-hosted, outline only, used sparingly.
- Admin: same tokens at higher density, 14px body, `--surface` content on `--paper` chrome. A top bar replaces the dark sidebar: wordmark "NSI Администрация", links Табло, Продукти, Категории, Поръчки, Доставки, and Изход on the right. It scrolls horizontally on phones.

## Never

- Gradient hero or gradient category tiles.
- Icon circles.
- Feature cards such as "Защо да изберете нас".
- `heading-accent` bars.
- Entrance animations, stagger classes, or hover lift on cards.
- Pill badges for units.
- A full-width coloured add-to-cart button on cards.
- Inline `style="color: var(--nsi-...)"` in admin pages.
- Web font or icon links to Google or jsDelivr.

## Copy rules

- Sentence case, active voice. No exclamation marks, no filler.
- Errors say what went wrong and what to do.
- One label per intent across the site ("Добави в количката" everywhere).
- Bulgarian copy may be edited where these rules require it.
- Dates `DD.MM.YYYY`. Money `12.50 €` through `PriceFormat`.

## Render modes

- Storefront (`/`, `/catalog`, `/products/{id}`, `/cart`, `/checkout`, `/order-confirmation/{OrderNumber}`, `/contacts`, `/not-found`): static SSR, in `Api/Components`, with `[ExcludeFromInteractiveRouting]`. No `dotnet.*.wasm` and no .NET assemblies are loaded on these pages.
- Admin (`/admin/**`, including the receipt): Client pages with interactive WebAssembly routing, prerender off. `/admin/login` is a static SSR form.
- Visualizer (`/visualizer`): a static SSR page with one island, `VisualizerApp`, using `InteractiveWebAssemblyRenderMode(prerender: false)`.
- Links between storefront and admin use `data-enhance-nav="false"` or `forceLoad: true`.
