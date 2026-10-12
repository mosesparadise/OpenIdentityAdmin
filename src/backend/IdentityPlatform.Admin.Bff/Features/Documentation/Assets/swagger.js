/* Pinned local Swagger bootstrap: no string eval, credential entry or interactive requests. */
const selector = document.getElementById('contract');
const container = document.getElementById('swagger-ui');
function updateScrollableRegions() {
  container.querySelectorAll('.parameters-container,.responses-wrapper,.model-box,.highlight-code,pre').forEach(node => {
    const scrolls = node.scrollWidth > node.clientWidth + 1 || node.scrollHeight > node.clientHeight + 1;
    if (scrolls && !node.hasAttribute('tabindex')) {
      node.tabIndex = 0; node.dataset.contractScrollFocus = 'true';
    } else if (!scrolls && node.dataset.contractScrollFocus === 'true') {
      node.removeAttribute('tabindex'); delete node.dataset.contractScrollFocus;
    }
  });
}
new ResizeObserver(updateScrollableRegions).observe(container);
// Adapt pinned Swagger5.33.1 semantics to this page's outer title and metadata regions.
// Observe rendered document changes so selector and schema expansion retain labels.
new MutationObserver(() => {
  for (const [selector, label] of [['.information-container', 'Contract information'], ['.scheme-container', 'Server information']]) {
    container.querySelectorAll(selector).forEach(node => {
      node.setAttribute('role', 'region'); node.setAttribute('aria-label', label);
    });
  }
  container.querySelectorAll('.info .title').forEach(node => {
    node.setAttribute('role', 'heading'); node.setAttribute('aria-level', '2');
  });
  container.querySelectorAll('table.responses-table').forEach(node => {
    node.removeAttribute('role'); node.setAttribute('aria-label', node.id.replaceAll('_', ' '));
  });
  container.querySelectorAll('select#servers').forEach(node => node.setAttribute('aria-label', 'Management API server'));
  updateScrollableRegions();
}).observe(container, { childList: true, subtree: true });
function render() {
  const name = selector.value === 'v1' ? 'v1' : 'bff-v1';
  SwaggerUIBundle({
    url: `/openapi/${name}.json`, dom_id: '#swagger-ui',
    supportedSubmitMethods: [], tryItOutEnabled: false, persistAuthorization: false,
    validatorUrl: null, queryConfigEnabled: false, displayOperationId: true,
    docExpansion: 'full', defaultModelsExpandDepth: 1,
    plugins: [() => ({ wrapComponents: {
      authorizeBtn: () => () => null,
      authorizationPopup: () => () => null,
      auths: () => () => null
    } })]
  });
}
selector.addEventListener('change', render);
render();
