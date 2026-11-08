// Live catalog search (spec §5.5). Without JavaScript the GET form still works on Enter. With it, the page
// updates 300 ms after typing stops, through enhanced navigation that replaces the history entry. The input
// carries data-permanent, so its text and focus survive the update. A new search starts at page 1 and keeps
// the category.
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
  if (url === location.pathname + location.search) return;
  window.Blazor.navigateTo(url, { replaceHistoryEntry: true });
}

function onInput(event) {
  const input = event.target;
  const form = input instanceof HTMLInputElement && input.name === 'search' ? input.closest(FORM) : null;
  if (!form) return;
  clearTimeout(timer);
  const text = input.value.trim();
  if (text.length === 1) return; // one letter matches too much; an empty field clears the search
  timer = setTimeout(() => go(form, text), DELAY);
}

// Enter: search now, with the same clean URL as live search, even for one letter.
function onSubmit(event) {
  const form = event.target instanceof HTMLFormElement && event.target.matches(FORM) ? event.target : null;
  if (!form || !window.Blazor) return;
  event.preventDefault();
  go(form, form.elements.namedItem('search').value.trim());
}

// Keeps the category strip (phones) scrolled to the current category.
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
  document.addEventListener('input', onInput);
  document.addEventListener('submit', onSubmit, true);
}

export function onUpdate() {
  const input = document.querySelector(FORM + ' input[name="search"]');
  if (!input) return;
  // data-permanent keeps the old text when a link (a category, "Изчисти търсенето", Back) changes the search.
  if (document.activeElement !== input) {
    const search = new URLSearchParams(location.search).get('search') || '';
    if (input.value.trim() !== search.trim()) input.value = search;
  }
  revealCurrentCategory();
}

export function onDispose() {
  clearTimeout(timer);
  timer = 0;
  document.removeEventListener('input', onInput);
  document.removeEventListener('submit', onSubmit, true);
}
