# Epic 10: Receipt Printing

## Description
Admin can print a стокова разписка (non-official receipt) for any confirmed or completed order. The receipt is rendered as a print-friendly HTML page and printed via the browser's native print dialog.

## Dependencies
- Epic 07 (Order Management) must be completed — order detail data must be available.

## Stories

---

### Story 10.1: Receipt Component

**As** the admin, **I want** a print-friendly receipt layout **so that** I can print a стокова разписка for customers.

**Acceptance Criteria:**
- [ ] Receipt component/page at `/admin/orders/{id}/receipt`
- [ ] **Receipt layout** (designed for A4 paper, portrait):

  **Header:**
  - Shop name: "Natural Stone Impex" (bold, centered)
  - Shop address (placeholder)
  - Shop phone (placeholder)
  - Divider line

  **Document title:**
  - "СТОКОВА РАЗПИСКА" (centered, bold, larger font)
  - "№ {OrderNumber}" below
  - "Дата: {DD.MM.YYYY}" (order confirmation date or print date)

  **Customer info:**
  - If Физическо лице: Клиент: {FullName}, Телефон: {Phone}, Адрес: {Address if delivery}
  - If Фирма: Фирма: {CompanyName}, ЕИК: {Eik}, МОЛ: {Mol}, Лице за контакт: {ContactPerson}, Телефон: {ContactPhone}, Адрес: {Address if delivery}

  **Items table:**
  | № | Продукт | Мерна ед. | Количество | Ед. цена без ДДС | ДДС | Ед. цена с ДДС | Общо с ДДС |
  |---|---------|-----------|------------|-------------------|-----|-----------------|------------|
  - Row for each order item
  - All prices in EUR with 2 decimal places

  **Totals:**
  - Сума без ДДС: {subtotal without VAT} €
  - Общо ДДС: {total VAT} €
  - Цена за доставка: {delivery fee} € (only shown if delivery fee > 0)
  - **Обща сума: {grand total} €** (bold, larger)

  **Footer:**
  - Blank line
  - "Предал: _______________" (Handed by)
  - "Приел: _______________" (Received by)
  - Blank line
  - "Стокова разписка — не е официален данъчен документ." (italic, smaller font)

- [ ] Receipt uses clean, simple CSS: black text on white, no colors, thin borders on table
- [ ] Font: serif or system font, readable when printed
- [ ] Margins appropriate for A4 printing

**Tasks:**
- Create `Pages/Admin/Receipt.razor`
- Fetch order detail from API on load
- Implement receipt HTML layout
- Create print-specific CSS (can be in a `<style>` block or separate file)
- Handle loading and error states (order not found, not yet confirmed)

---

### Story 10.2: Print Functionality via JS Interop

**As** the admin, **I want** to click a button and print the receipt **so that** I can give a paper copy to the customer.

**Acceptance Criteria:**
- [ ] "Принтирай" (Print) button displayed at the top of the receipt page (visible on screen only, hidden when printing)
- [ ] Clicking the button triggers `window.print()` via JS interop
- [ ] Print CSS hides:
  - The print button itself
  - The admin sidebar/navigation
  - Any browser chrome (handled by `@media print` rules)
- [ ] Print CSS ensures:
  - Receipt fits on a single A4 page (for typical orders)
  - Page margins are set via `@page { margin: 15mm; }`
  - Table doesn't break across pages for long orders (use `page-break-inside: avoid`)
  - No background colors or images printed (clean B&W)
- [ ] "Обратно към поръчката" link at the top (also hidden when printing)
- [ ] Receipt page works when opened directly via URL (fetches data independently)

**Tasks:**
- Create JS interop function for `window.print()` in `wwwroot/js/print.js` or inline
- Register JS file in `index.html`
- Add print button with JS interop call
- Write `@media print` CSS rules to hide non-receipt elements
- Write `@page` CSS rules for margins
- Add "back to order" navigation link
- Test printing in Chrome and Firefox

---

### Story 10.3: Print Button on Order Detail Page

**As** the admin, **I want** a print button on the order detail page **so that** I can quickly print a receipt without navigating away.

**Acceptance Criteria:**
- [ ] "Принтирай разписка" button visible on Order Detail page (`/admin/orders/{id}`)
- [ ] Button only shown for orders with status Потвърдена or Завършена (not Чакаща or Cancelled)
- [ ] Clicking the button opens `/admin/orders/{id}/receipt` in a new browser tab
- [ ] Button styled with Bootstrap (e.g., `btn btn-outline-primary` with a printer icon)

**Tasks:**
- Add button to `Pages/Admin/OrderDetail.razor`
- Conditional rendering based on order status
- Use `NavigationManager` or `target="_blank"` to open in new tab
- Add printer icon (Bootstrap Icons or Unicode 🖨️)
