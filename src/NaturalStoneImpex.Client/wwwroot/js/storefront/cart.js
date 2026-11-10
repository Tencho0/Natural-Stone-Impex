// Cart rows (spec §7.4, §5.5). Without JS every row is an enhanced form: Enter posts the typed quantity,
// − and + post step=-1 or 1, and the server redirects back to /cart. With JS:
// - typing posts the row 400 ms after the last keystroke (never mid-IME-word, never an unparsable value);
// - − and + change the field at once and post the same way, so quick presses become one request;
// - one post at a time, so each request carries the cookie the previous one wrote. Edits made meanwhile
//   wait, and are put back into their fields after the update.
// Focus stays where it was: the post redirects to the same view, which focus.js leaves alone.
const DELAY = 400;
const QTY = 'form[data-cart-qty]';
const REMOVE = 'form[data-cart-remove]';
const STUCK_MS = 8000; // stop waiting for an update that never came (network error)

const edits = new Map(); // productId -> text typed but not sent yet
let timer = 0;
let inFlight = 0;        // when the current post started; 0 when idle
let sent = '';           // "productId:cents" of the quantity post in flight
let removeNext = null;   // productId whose remove was pressed during a post

// "2,5" / "2.5" / "1 204,5" -> cents (250), or null when the server would not store it.
function toCents(text, max) {
  const clean = String(text).replace(/[\s  ]/g, '').replace(',', '.');
  if (!/^(\d+(\.\d+)?|\.\d+)$/.test(clean)) return null;
  const cents = Math.round(parseFloat(clean) * 100);
  return cents > 0 && cents <= max * 100 ? cents : null;
}

function idOf(form) {
  return form.elements.namedItem('productId').value;
}

function find(selector, id) {
  for (const form of document.querySelectorAll(selector)) if (idOf(form) === id) return form;
  return null;
}

function field(form) {
  return { input: form.elements.namedItem('quantity'), max: Number(form.dataset.max) || 10000 };
}

function busy() {
  if (inFlight && Date.now() - inFlight > STUCK_MS) inFlight = 0;
  return inFlight !== 0;
}

let sending = false; // our own requestSubmit: onSubmit lets it through

function submit(form) {
  sending = true;
  try {
    form.requestSubmit(); // no submitter: the quantity form posts the typed value without a step
  } finally {
    sending = false;
  }
  inFlight = Date.now();
}

// Sends the first waiting edit that differs from what the server rendered (input.defaultValue).
function flush() {
  clearTimeout(timer);
  timer = 0;
  if (busy()) return; // onUpdate flushes again
  for (const [id, text] of edits) {
    edits.delete(id);
    const form = find(QTY, id);
    if (!form) continue;
    const { input, max } = field(form);
    const cents = toCents(text, max);
    if (cents === null || cents === toCents(input.defaultValue, max)) continue;
    input.value = text;
    sent = id + ':' + cents;
    submit(form);
    return;
  }
}

function edited(form) {
  edits.set(idOf(form), field(form).input.value);
  clearTimeout(timer);
  timer = setTimeout(flush, DELAY);
}

// The quantity form whose field fired the event, or null.
function qtyInput(event) {
  const target = event.target;
  return target instanceof HTMLInputElement && target.name === 'quantity' ? target.closest(QTY) : null;
}

// An IME sends input mid-word; wait for compositionend.
function onType(event) {
  const form = qtyInput(event);
  if (form && (event.type === 'compositionend' || !event.isComposing)) edited(form);
}

// Leaving the field sends the change now, or puts back the stored quantity if the text is not one.
function onChange(event) {
  const form = qtyInput(event);
  if (!form) return;
  const { input, max } = field(form);
  if (toCents(input.value, max) === null) {
    edits.delete(idOf(form));
    input.value = input.defaultValue;
    return;
  }
  if (edits.has(idOf(form))) flush();
}

// − and +: the server's rule (CartStore.Update): from the typed value when valid, else the stored one,
// kept between 0.01 and the maximum.
function onClick(event) {
  const button = event.target instanceof Element ? event.target.closest('button[name="step"]') : null;
  const form = button && button.closest(QTY);
  if (!form) return;
  event.preventDefault();
  const { input, max } = field(form);
  const base = toCents(input.value, max) ?? toCents(input.defaultValue, max);
  if (base === null) return;
  const next = Math.min(Math.max(base + Number(button.value) * 100, 1), max * 100);
  input.value = (next / 100).toFixed(2);
  edited(form);
}

// Enter in a quantity field and the remove button are native enhanced posts. Hold them while another
// post is in flight; otherwise just keep the bookkeeping right. Enter fires "change" first, which may
// already have sent the same value: that submit is dropped, not queued.
function onSubmit(event) {
  const form = event.target instanceof HTMLFormElement ? event.target : null;
  if (sending || !form || !(form.matches(QTY) || form.matches(REMOVE))) return;
  const id = idOf(form);
  if (form.matches(QTY)) {
    const { input, max } = field(form);
    const key = id + ':' + toCents(input.value, max);
    if (busy()) {
      event.preventDefault();
      if (key !== sent) edited(form);
      return;
    }
    edits.delete(id);
    sent = key;
  } else if (busy()) {
    event.preventDefault();
    removeNext = id;
    return;
  } else {
    edits.delete(id);
  }
  inFlight = Date.now();
}

export function onLoad() {
  document.addEventListener('input', onType);
  document.addEventListener('compositionend', onType);
  document.addEventListener('change', onChange);
  document.addEventListener('click', onClick);
  document.addEventListener('submit', onSubmit, true);
}

export function onUpdate() {
  inFlight = 0;
  sent = '';
  // The DOM merge keeps a field the user typed in and resets one set by script, so set every field
  // here: the waiting edit, else the quantity the server rendered ("2,5" shows as "2.50").
  const seen = new Set();
  for (const form of document.querySelectorAll(QTY)) {
    const id = idOf(form);
    const { input } = field(form);
    seen.add(id);
    input.value = edits.has(id) ? edits.get(id) : input.defaultValue;
  }
  for (const id of edits.keys()) if (!seen.has(id)) edits.delete(id);

  const remove = removeNext && find(REMOVE, removeNext);
  removeNext = null;
  if (remove) {
    edits.delete(idOf(remove));
    submit(remove);
  } else if (edits.size && !timer) {
    flush();
  }
}

export function onDispose() {
  clearTimeout(timer);
  timer = 0;
  inFlight = 0;
  sent = '';
  removeNext = null;
  edits.clear();
  document.removeEventListener('input', onType);
  document.removeEventListener('compositionend', onType);
  document.removeEventListener('change', onChange);
  document.removeEventListener('click', onClick);
  document.removeEventListener('submit', onSubmit, true);
}
