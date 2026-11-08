// Tap-to-zoom for the product photo (spec §7.3). The photo link names a <dialog> in aria-controls; with JS the
// dialog opens as a modal, without JS the link opens the image file. Esc closes natively; a click on the
// backdrop or on "Затвори" closes it too, and focus returns to the photo link.
let trigger = null;

function onClick(event) {
  const target = event.target instanceof Element ? event.target : null;
  if (!target) return;

  const link = target.closest('a[data-zoom]');
  if (link) {
    const dialog = document.getElementById(link.getAttribute('aria-controls'));
    if (!(dialog instanceof HTMLDialogElement) || event.ctrlKey || event.metaKey || event.shiftKey) return;
    event.preventDefault();
    trigger = link;
    dialog.showModal();
    return;
  }

  // A click on the dialog element itself is a click on its backdrop (the content covers the rest).
  const dialog = target.closest('dialog.photo-zoom');
  if (dialog && (target === dialog || target.closest('[data-zoom-close]'))) dialog.close();
}

// 'close' does not bubble, so it is caught in the capture phase.
function onClose(event) {
  if (!(event.target instanceof HTMLDialogElement) || !event.target.matches('.photo-zoom')) return;
  if (trigger && trigger.isConnected) trigger.focus();
  trigger = null;
}

export function onLoad() {
  document.addEventListener('click', onClick, true);
  document.addEventListener('close', onClose, true);
}

// After an enhanced navigation no zoom stays open.
export function onUpdate() {
  document.querySelectorAll('dialog.photo-zoom[open]').forEach((dialog) => dialog.close());
}

export function onDispose() {
  onUpdate();
  document.removeEventListener('click', onClick, true);
  document.removeEventListener('close', onClose, true);
}
