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
      auths: () => () => null,
      // The pinned upstream emits unnamed response regions. Preserve the native table
      // semantics and give each table an operation-specific accessible name.
      responses: (Original, system) => class extends Original {
        render() {
          const label = `${this.props.method.toUpperCase()} ${this.props.path} responses`;
          const decorate = element => {
            if (!system.React.isValidElement(element)) return element;
            const attributes = element.type === 'table' ? { role: undefined, 'aria-label': label } : {};
            return system.React.cloneElement(element, attributes,
              system.React.Children.map(element.props.children, decorate));
          };
          return decorate(super.render());
        }
      }
    } })]
  });
}
selector.addEventListener('change', render);
render();
