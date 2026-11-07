// <page-script src="..."> loader: the documented pattern for page JavaScript on static SSR pages with
// enhanced navigation. A module may export onLoad (once, when first needed), onUpdate (after it loads and
// after every enhanced page update) and onDispose (when no page uses it any more).
const pageScripts = new Map();

function register(src) {
  if (!src) return;
  const info = pageScripts.get(src);
  if (info) {
    info.refs++;
    return;
  }
  const created = { refs: 1, module: null };
  pageScripts.set(src, created);
  load(src, created);
}

function unregister(src) {
  const info = src && pageScripts.get(src);
  if (info) info.refs--;
}

async function load(src, info) {
  const module = await import(new URL(src, document.baseURI).toString());
  if (info.refs <= 0) return;
  info.module = module;
  module.onLoad?.();
  module.onUpdate?.();
}

function onEnhancedLoad() {
  for (const [src, info] of pageScripts) {
    if (info.refs <= 0) {
      info.module?.onDispose?.();
      pageScripts.delete(src);
    }
  }
  for (const info of pageScripts.values()) info.module?.onUpdate?.();
}

customElements.define('page-script', class extends HTMLElement {
  static observedAttributes = ['src'];

  attributeChangedCallback(name, oldValue, newValue) {
    if (name !== 'src') return;
    this.src = newValue;
    unregister(oldValue);
    register(newValue);
  }

  disconnectedCallback() {
    unregister(this.src);
  }
});

window.Blazor?.addEventListener('enhancedload', onEnhancedLoad);
