// The toast shows and hides itself in CSS (components.css). Enhanced navigation reuses DOM elements, so a
// toast that follows another toast would inherit the finished animation and stay hidden. Replacing a reused
// element with a fresh copy restarts its 4 s.
const seen = new WeakSet();

export function onUpdate() {
  const toast = document.querySelector('.toast-line');
  if (!toast) return;
  if (!seen.has(toast)) {
    seen.add(toast);
    return;
  }
  const fresh = toast.cloneNode(true);
  seen.add(fresh);
  toast.replaceWith(fresh);
}
