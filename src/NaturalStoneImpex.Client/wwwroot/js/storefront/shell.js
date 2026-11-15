// Loaded on every page after blazor.web.js (App.razor): focus after enhanced navigation (spec §8), the phone
// menu, the toast and the <page-script> loader. One module, not four, for the 8 KB JS budget.

// ---------- Focus ----------

// A query as a view: order-free, without blanks or page=1, so "?page=1&categoryId=2" equals "?categoryId=2".
function viewOf(query) {
  const parts = [];
  for (const [key, raw] of new URLSearchParams(query)) {
    const value = raw.trim();
    if (value && !(key === 'page' && value === '1')) parts.push(key + '=' + value);
  }
  return parts.sort().join('&');
}

let lastPath = location.pathname;
let lastView = viewOf(location.search);

function focusHeading() {
  const heading = document.querySelector('main h1');
  if (!heading) return;
  heading.setAttribute('tabindex', '-1');
  heading.focus({ preventScroll: true });
}

// A new page focuses its h1. The same view (a post redirecting back) keeps focus unless it was lost. A new view
// of the same path (catalog page) also scrolls up, which Blazor skips. Live search keeps its data-permanent input.
function moveFocus() {
  const pageChanged = location.pathname !== lastPath;
  const view = viewOf(location.search);
  const queryChanged = !pageChanged && view !== lastView;
  lastPath = location.pathname;
  lastView = view;

  const active = document.activeElement;
  if (active && active.closest('[data-permanent]')) return;
  if (queryChanged) window.scrollTo({ top: 0, behavior: 'instant' });
  if (pageChanged || queryChanged || !active || active === document.body) focusHeading();
}

// ---------- Phone menu (<details class="menu">) ----------

function closeMenus() {
  document.querySelectorAll('details.menu[open]').forEach((menu) => menu.removeAttribute('open'));
}

// Escape closes it and returns focus to "Меню".
document.addEventListener('keydown', (event) => {
  const open = event.key === 'Escape' && document.querySelector('details.menu[open]');
  if (!open) return;
  open.removeAttribute('open');
  open.querySelector('summary')?.focus();
});

document.addEventListener('click', (event) => {
  const target = event.target instanceof Element ? event.target : null;
  // Enhanced navigation turns "#main" into a scroll without moving focus, so the skip link focuses main itself.
  if (target?.closest('a.skip-link')) {
    const main = document.getElementById('main');
    if (main) requestAnimationFrame(() => main.focus());
  }
  // A click outside the menu, or on one of its links, closes it.
  const menu = target?.closest('details.menu');
  if (!menu) closeMenus();
  else if (target.closest('a')) menu.removeAttribute('open');
}, true);

// ---------- Toast ----------

// The toast hides itself in CSS. Enhanced navigation may reuse its element, finished animation included, so a
// reused toast is replaced by a fresh copy, which restarts its 4 s.
const seenToasts = new WeakSet();

function restartToast() {
  const toast = document.querySelector('.toast-line');
  if (!toast) return;
  if (!seenToasts.has(toast)) {
    seenToasts.add(toast);
    return;
  }
  const fresh = toast.cloneNode(true);
  seenToasts.add(fresh);
  toast.replaceWith(fresh);
}

// ---------- <page-script src="..."> loader ----------

// The documented page-script pattern. A module may export onLoad (once), onUpdate (after it loads and after
// every enhanced update) and onDispose (when no page uses it any more).
const pageScripts = new Map();

async function load(src, info) {
  const module = await import(new URL(src, document.baseURI).toString());
  if (info.refs <= 0) return;
  info.module = module;
  module.onLoad?.();
  module.onUpdate?.();
}

customElements.define('page-script', class extends HTMLElement {
  static observedAttributes = ['src'];

  attributeChangedCallback(name, oldValue, newValue) {
    if (name !== 'src') return;
    this.src = newValue;
    const old = oldValue && pageScripts.get(oldValue);
    if (old) old.refs--;
    if (!newValue) return;
    const info = pageScripts.get(newValue);
    if (info) {
      info.refs++;
    } else {
      const created = { refs: 1, module: null };
      pageScripts.set(newValue, created);
      load(newValue, created);
    }
  }

  disconnectedCallback() {
    const info = this.src && pageScripts.get(this.src);
    if (info) info.refs--;
  }
});

// ---------- After every enhanced page update ----------

restartToast(); // marks the first page's toast as fresh

window.Blazor?.addEventListener('enhancedload', () => {
  moveFocus(); // first, so a page module (checkout) can move focus on from the h1
  closeMenus();
  restartToast();
  for (const [src, info] of pageScripts) {
    if (info.refs <= 0) {
      info.module?.onDispose?.();
      pageScripts.delete(src);
    }
  }
  for (const info of pageScripts.values()) info.module?.onUpdate?.();
});
