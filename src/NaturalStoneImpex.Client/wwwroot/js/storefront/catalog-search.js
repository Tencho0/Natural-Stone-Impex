// Live search (spec §5.5): 300 ms after typing, replace-navigate to page 1, keeping the category.
// Without JS the GET form works on Enter.
const DELAY = 300;
const FORM = 'form[data-catalog-search]';
let timer = 0;

function urlFor(form, text) {
  const parts = [];
  const category = form.elements.namedItem('categoryId');
  if (category && category.value) parts.push('categoryId=' + encodeURIComponent(category.value));
  if (text) parts.push('search=' + encodeURIComponent(text));
  return '/catalog' + (parts.length ? '?' + parts.join('&') : '');
}

function go(form, text) {
  clearTimeout(timer);
  timer = 0;
  const url = urlFor(form, text);
  if (url !== location.pathname + location.search) window.Blazor.navigateTo(url, { replaceHistoryEntry: true });
}

function schedule(input) {
  const form = input instanceof HTMLInputElement && input.name === 'search' ? input.closest(FORM) : null;
  if (!form) return;
  clearTimeout(timer);
  const text = input.value.trim();
  if (text.length === 1) return; // too broad; empty clears the search
  timer = setTimeout(() => go(form, text), DELAY);
}

// An IME sends input mid-word; wait for compositionend.
function onType(event) {
  if (event.type === 'compositionend' || !event.isComposing) schedule(event.target);
}

// Enter searches at once, even for one letter.
function onSubmit(event) {
  const form = event.target instanceof HTMLFormElement && event.target.matches(FORM) ? event.target : null;
  if (!form || !window.Blazor) return;
  event.preventDefault();
  go(form, form.elements.namedItem('search').value.trim());
}

// Scrolls the phone category strip (not the page) to the current category.
function revealCurrentCategory() {
  const strip = document.querySelector('.catalog-cats .cat-index');
  const current = strip && strip.querySelector('[aria-current="page"]');
  if (!current || strip.scrollWidth <= strip.clientWidth) return;
  const item = current.getBoundingClientRect();
  const box = strip.getBoundingClientRect();
  if (item.left < box.left || item.right > box.right) {
    strip.scrollLeft += item.left - box.left - (box.width - item.width) / 2;
  }
}

export function onLoad() {
  document.addEventListener('input', onType);
  document.addEventListener('compositionend', onType);
  document.addEventListener('submit', onSubmit, true);
}

export function onUpdate() {
  const input = document.querySelector(FORM + ' input[name="search"]');
  if (!input) return;
  // data-permanent keeps stale text when a link or Back changes the search.
  if (document.activeElement !== input) {
    const search = new URLSearchParams(location.search).get('search') || '';
    if (input.value.trim() !== search.trim()) input.value = search;
  }
  revealCurrentCategory();
}

export function onDispose() {
  clearTimeout(timer);
  timer = 0;
  document.removeEventListener('input', onType);
  document.removeEventListener('compositionend', onType);
  document.removeEventListener('submit', onSubmit, true);
}
