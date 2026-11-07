// Phone menu (<details class="menu">): closes on navigation, on Escape and on a click outside it.
function closeAll() {
  document.querySelectorAll('details.menu[open]').forEach((menu) => menu.removeAttribute('open'));
}

function onKeydown(event) {
  if (event.key !== 'Escape') return;
  const open = document.querySelector('details.menu[open]');
  if (!open) return;
  open.removeAttribute('open');
  open.querySelector('summary')?.focus();
}

function onClick(event) {
  const target = event.target instanceof Element ? event.target : null;
  const menu = target?.closest('details.menu');
  if (!menu) closeAll();
  else if (target.closest('a')) menu.removeAttribute('open');
}

export function onLoad() {
  document.addEventListener('keydown', onKeydown);
  document.addEventListener('click', onClick);
}

export function onUpdate() {
  closeAll();
}

export function onDispose() {
  document.removeEventListener('keydown', onKeydown);
  document.removeEventListener('click', onClick);
}
