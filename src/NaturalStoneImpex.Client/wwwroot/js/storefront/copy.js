// Copy button (spec §7.6): shown only where the clipboard works; reads "Копирано" for 2 s after a copy.
let timer = 0;

async function onClick(event) {
  const button = event.target instanceof Element ? event.target.closest('button[data-copy]') : null;
  if (!button) return;
  try {
    await navigator.clipboard.writeText(button.dataset.copy);
  } catch {
    return; // the number stays selectable as text
  }
  const label = button.querySelector('[data-copy-label]');
  const status = document.querySelector('[data-copy-status]');
  label.dataset.text ??= label.textContent;
  label.textContent = 'Копирано';
  status.textContent = 'Номерът е копиран.';
  clearTimeout(timer);
  timer = setTimeout(() => {
    label.textContent = label.dataset.text;
    status.textContent = '';
  }, 2000);
}

export function onLoad() {
  document.addEventListener('click', onClick);
}

export function onUpdate() {
  if (navigator.clipboard) for (const b of document.querySelectorAll('button[data-copy]')) b.hidden = false;
}

export function onDispose() {
  clearTimeout(timer);
  document.removeEventListener('click', onClick);
}
