// Focus management for static SSR pages (spec §8). Loaded on every page, after blazor.web.js.
(() => {
  let lastPath = location.pathname;
  let lastQuery = location.search;

  function focusHeading() {
    const heading = document.querySelector('main h1');
    if (!heading) return;
    heading.setAttribute('tabindex', '-1');
    heading.focus({ preventScroll: true });
  }

  // After an enhanced navigation, focus the new page's h1. A same-page update (a form post that redirects
  // back to the same URL) keeps focus where it is, unless the focused element was removed. A new query on the
  // same path (a catalog page or category link) is a new view: Blazor does not scroll for it, so scroll up here.
  // Live search also changes the query, but focus is then in the data-permanent input and stays there.
  function onEnhancedLoad() {
    const pageChanged = location.pathname !== lastPath;
    const queryChanged = !pageChanged && location.search !== lastQuery;
    lastPath = location.pathname;
    lastQuery = location.search;

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
