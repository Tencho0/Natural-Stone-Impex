// Checkout (spec §7.5); the form works without JS. Here: switches show their groups ([data-when="name=value"])
// at once; a submit sets aria-busy, disables the buttons and drops a second submit (the server token is the
// real guard); after a failed enhanced post focus moves to the problems box, whose links focus their field.
const FORM = 'form[data-checkout]';

function sync(form) {
  for (const group of form.querySelectorAll('[data-when]')) {
    const [name, value] = group.dataset.when.split('=');
    group.hidden = form.querySelector(`input[name="${name}"]:checked`)?.value !== value;
  }
}

function onChange(event) {
  const form = event.target.type === 'radio' ? event.target.closest(FORM) : null;
  if (form) sync(form);
}

// Capture phase, before Blazor's enhanced-form handler (which skips a prevented submit).
function onSubmit(event) {
  const form = event.target;
  if (!form.matches?.(FORM)) return;
  if (form.ariaBusy === 'true') return event.preventDefault();
  form.ariaBusy = 'true';
  for (const button of form.querySelectorAll('button')) button.disabled = true;
}

function onClick(event) {
  const link = event.target.closest?.('a[data-problem-link]');
  const field = link && document.getElementById(link.hash.slice(1));
  if (!field) return;
  event.preventDefault();
  field.focus();
}

export function onLoad() {
  document.addEventListener('change', onChange);
  document.addEventListener('submit', onSubmit, true);
  document.addEventListener('click', onClick);
}

export function onUpdate() {
  const form = document.querySelector(FORM);
  if (!form) return;
  form.ariaBusy = null;
  for (const button of form.querySelectorAll('button')) button.disabled = false;
  sync(form);
  document.querySelector('[data-checkout-problems]')?.focus();
}

export function onDispose() {
  document.removeEventListener('change', onChange);
  document.removeEventListener('submit', onSubmit, true);
  document.removeEventListener('click', onClick);
}
