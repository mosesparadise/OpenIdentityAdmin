/* Pinned local Swagger bootstrap: no string eval, credential entry or interactive requests. */
const selector = document.getElementById('contract');
const container = document.getElementById('swagger-ui');
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
