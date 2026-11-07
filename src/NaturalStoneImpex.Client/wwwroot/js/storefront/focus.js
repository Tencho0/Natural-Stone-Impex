// Focus management for static SSR pages (spec §8). Loaded on every page, after blazor.web.js.
(() => {
  let lastPath = location.pathname;

  function focusHeading() {
    const heading = document.querySelector('main h1');
    if (!heading) return;
    heading.setAttribute('tabindex', '-1');
    heading.focus({ preventScroll: true });
  }

  // After an enhanced navigation, focus the new page's h1. A same-page update (a form post that redirects
  // back, a live search) keeps focus where it is, unless the focused element was removed.
  function onEnhancedLoad() {
    const path = location.pathname;
    const pageChanged = path !== lastPath;
    lastPath = path;

    const active = document.activeElement;
    if (active && active.closest('[data-permanent]')) return;
    if (pageChanged || !active || active === document.body) focusHeading();
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
