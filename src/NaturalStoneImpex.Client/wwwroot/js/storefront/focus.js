// Focus management for static SSR pages (spec §8). Loaded on every page, after blazor.web.js.
(() => {
  // A query as a view: decoded, order-free, without blanks or page=1, so a redirect to the canonical URL
  // ("?page=1&categoryId=2" -> "?categoryId=2") is the same view.
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

  // After enhanced navigation, focus the new page's h1. The same view (a post redirecting back) keeps focus
  // unless the focused element is gone. A new view on the same path (catalog page, category) also scrolls up,
  // which Blazor skips. Live search keeps focus in its data-permanent input.
  function onEnhancedLoad() {
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

  // Enhanced navigation turns "#main" into a scroll without moving focus, so the skip link focuses main itself.
  document.addEventListener('click', (event) => {
    const link = event.target instanceof Element ? event.target.closest('a.skip-link') : null;
    if (!link) return;
    const main = document.getElementById('main');
    if (main) requestAnimationFrame(() => main.focus());
  }, true);

  if (window.Blazor) window.Blazor.addEventListener('enhancedload', onEnhancedLoad);
})();
