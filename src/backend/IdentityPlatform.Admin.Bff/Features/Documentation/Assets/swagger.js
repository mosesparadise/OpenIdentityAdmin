/* Pinned local Swagger bootstrap: no string eval, credential entry or interactive requests. */
const selector = document.getElementById('contract');
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
