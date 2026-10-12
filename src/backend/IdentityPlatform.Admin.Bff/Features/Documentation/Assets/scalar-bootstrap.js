/* Scalar2.17.14 bundles Zod, whose JIT probe invokes Function even when caught.
   Opt out before the bundle loads; preserve the CSP prohibition on unsafe-eval. */
globalThis.__zod_globalConfig ??= {};
globalThis.__zod_globalConfig.jitless = true;
document.documentElement.lang = 'en';

// Scalar's endpoint overview anchors have click handlers but no href/keyboard semantics.
// Preserve their existing navigation handler and expose the action to keyboard users.
document.addEventListener('DOMContentLoaded', () => {
  const selector = 'ul.endpoints a.endpoint:not([href])';
  function annotateEndpoints() {
    document.querySelectorAll(selector).forEach(node => {
      node.setAttribute('role', 'button'); node.tabIndex = 0;
    });
  }
  new MutationObserver(annotateEndpoints).observe(document.body, { childList: true, subtree: true });
  document.addEventListener('keydown', event => {
    if ((event.key === 'Enter' || event.key === ' ') && event.target instanceof Element && event.target.matches(selector)) {
      event.preventDefault(); event.target.click();
    }
  });
  annotateEndpoints();
});
