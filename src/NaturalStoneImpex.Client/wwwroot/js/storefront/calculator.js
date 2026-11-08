// Live result for the product calculator (spec §7.3). It mirrors DecimalInput and QuantityCalculator in
// BigInt fixed point, so it is exact: 4 × 3 + 10% is 13.20, never 13.21. It only rewrites the result line
// and the button label; the POST recalculates on the server, which stays authoritative. All copy comes from
// the form's data-* templates (ProductView), so the script has no text of its own.
const FORM = 'form[data-calc]';
const MAX = 10000n; // QuantityCalculator.MaxQuantity
const DECIMAL_MAX = 79228162514264337593543950335n; // decimal.MaxValue: larger input does not parse in C#

// DecimalInput.TryParse: drop spaces, NBSP and NNBSP, read a comma as the point, digits only.
// Returns { n, s } meaning n / 10^s, or null.
function parse(text) {
  const clean = String(text ?? '').replace(/[   ]/g, '').replace(/,/g, '.');
  const m = /^(\d*)(?:\.(\d*))?$/.exec(clean);
  if (!m || !(m[1] || m[2])) return null;
  if (BigInt(m[1] || '0') > DECIMAL_MAX) return null;
  const frac = m[2] || '';
  return { n: BigInt((m[1] || '0') + frac), s: frac.length };
}

// QuantityCalculator.TryInput: missing or not above 0, or above the cap.
function read(form, name) {
  const v = parse(form.elements.namedItem('Calc.' + name)?.value);
  if (!v || v.n === 0n) return 'missing';
  return v.n > MAX * 10n ** BigInt(v.s) ? 'big' : v;
}

function isArea(form) {
  return form.querySelector('input[name="Calc.Mode"]:checked')?.value === 'area';
}

// QuantityCalculator.Calculate: hundredths of the unit as a BigInt, or an error code.
function quantity(form) {
  let raw;
  let waste = false;
  if (form.hasAttribute('data-kg')) {
    raw = read(form, 'Kilograms');
  } else {
    const area = isArea(form);
    waste = !!form.elements.namedItem('Calc.Waste')?.checked;
    if (area) {
      raw = read(form, 'Area');
    } else {
      const length = read(form, 'Length');
      if (typeof length === 'string') return length;
      const width = read(form, 'Width');
      raw = typeof width === 'string' ? width : { n: length.n * width.n, s: length.s + width.s };
    }
  }
  if (typeof raw === 'string') return raw;
  if (waste) raw = { n: raw.n * 11n, s: raw.s + 1 }; // × 1.10
  // Math.Ceiling(raw * 100) / 100
  let q;
  if (raw.s <= 2) {
    q = raw.n * 10n ** BigInt(2 - raw.s);
  } else {
    const d = 10n ** BigInt(raw.s - 2);
    q = (raw.n + d - 1n) / d;
  }
  return q > MAX * 100n ? 'big' : q;
}

// PriceFormat: invariant, two decimals, put into a template such as "{0} м²" or "{0} €".
function format(template, hundredths) {
  const number = (hundredths / 100n) + '.' + String(hundredths % 100n).padStart(2, '0');
  return (template || '{0}').replace('{0}', number);
}

function update(form) {
  const result = form.querySelector('[data-calc-result]');
  const button = form.querySelector('[data-calc-submit]');
  if (!result || !button) return;
  const data = form.dataset;
  const q = quantity(form);

  const line = [];
  if (typeof q === 'string') {
    // ProductView.ResultNote: "too large", or what to fill in for the chosen mode.
    const note = q === 'missing' && isArea(form) ? result.dataset.missingArea : result.dataset[q];
    line.push(['product-calc__qty', note || '']);
    button.textContent = data.addPlain || '';
  } else {
    const price = parse(data.price);
    const cents = price ? (price.n * 10n ** BigInt(2 - Math.min(price.s, 2)) * q + 50n) / 100n : 0n;
    line.push(['product-calc__qty', format(data.qtyTemplate, q)], ['product-calc__total', format(data.moneyTemplate, cents)]);
    button.textContent = format(data.addTemplate, q);
  }

  // Rewrite only on change, so the live region does not repeat itself.
  const text = line.map((part) => part[1]).join(' ');
  if (result.textContent.replace(/\s+/g, ' ').trim() === text) return;
  result.replaceChildren(...line.map(([cls, value]) => {
    const span = document.createElement('span');
    span.className = cls;
    span.textContent = value;
    return span;
  }));
}

function onChange(event) {
  const form = event.target instanceof Element ? event.target.closest(FORM) : null;
  if (form) update(form);
}

export function onLoad() {
  document.addEventListener('input', onChange);
  document.addEventListener('change', onChange);
}

export function onUpdate() {
  document.querySelectorAll(FORM).forEach(update);
}

export function onDispose() {
  document.removeEventListener('input', onChange);
  document.removeEventListener('change', onChange);
}
