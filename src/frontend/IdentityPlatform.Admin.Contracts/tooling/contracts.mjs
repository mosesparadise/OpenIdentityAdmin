import { isDeepStrictEqual } from 'node:util';

const methods = new Set(['get', 'put', 'post', 'delete', 'patch', 'head', 'options', 'trace']);
export function inventory(document, surface, allowEmpty = false) {
  if (!/^3\.1\.\d+$/.test(document.openapi) || document.info?.version !== '1') throw new Error('Unsupported contract version');
  function inspect(value) {
    if (!value || typeof value !== 'object') return;
    for (const [key, child] of Object.entries(value)) {
      if (key === '$dynamicRef' || key === '$dynamicAnchor') throw new Error('Unsupported dynamic schema resolution');
      if (key === '$ref' && (typeof child !== 'string' || !child.startsWith('#/'))) throw new Error('External references forbidden');
      inspect(child);
    }
  }
  inspect(document);
  if (document.components?.pathItems) throw new Error('Unsupported reusable path items');
  const prefix = surface === 'bff' ? '/bff/api/v1/' : '/api/v1/';
  const operations = new Map();
  const ids = new Set();
  for (const [route, item] of Object.entries(document.paths ?? {})) {
    if (!route.startsWith(prefix)) throw new Error('Route outside management surface');
    for (const [method, op] of Object.entries(item)) {
      if (!methods.has(method)) continue;
      if (!op.operationId || ids.has(op.operationId)) throw new Error('Missing or duplicate operation ID');
      ids.add(op.operationId);
      const scheme = surface === 'bff' ? 'BrowserSession' : 'AdminBearer';
      if (!isDeepStrictEqual(op.security, [{ [scheme]: [] }])) throw new Error('Missing or weakened authentication requirement');
      if (typeof op['x-required-permission'] !== 'string' || !op['x-required-permission']) throw new Error('Missing permission');
      if (surface === 'bff' && op['x-downstream-api-version'] !== 1) throw new Error('Missing explicit downstream mapping');
      if (['put', 'patch', 'delete', 'post'].includes(method)) {
        const names = method === 'post' ? [] : ['If-Match'];
        if (surface === 'bff') names.push('X-CSRF-TOKEN');
        for (const name of names) {
          if (!op.parameters?.some(p => p.in === 'header' && p.name.toLowerCase() === name.toLowerCase() && p.required === true)) throw new Error('Missing concurrency/antiforgery precondition');
        }
      }
      operations.set(`${method} ${route}`, op);
    }
  }
  if (!allowEmpty && operations.size === 0) throw new Error('Empty representative operation inventory');
  if (operations.size) {
    const schemes = document.components?.securitySchemes;
    if (surface === 'bff') {
      if (Object.keys(schemes ?? {}).join() !== 'BrowserSession' || schemes.BrowserSession.type !== 'apiKey' || schemes.BrowserSession.in !== 'cookie') throw new Error('Browser contract must contain only cookie authentication');
    } else if (schemes?.AdminBearer?.type !== 'http' || schemes.AdminBearer.scheme !== 'bearer') throw new Error('Downstream contract must use bearer authentication');
  }
  return operations;
}

export function securityCompatibility(previous, current, surface, allowEmpty = false) {
  const before = inventory(previous, surface, allowEmpty), after = inventory(current, surface, allowEmpty);
  if (before.size && !isDeepStrictEqual(previous.components?.securitySchemes, current.components?.securitySchemes)) throw new Error('Authentication definition changed');
  for (const [key, old] of before) {
    const next = after.get(key);
    if (!next) throw new Error('Removed operation');
    for (const property of ['operationId', 'security', 'x-required-permission', 'x-downstream-api-version']) {
      if (!isDeepStrictEqual(old[property], next[property])) throw new Error(`Changed ${property}`);
    }
    // Header semantics need explicit review even where generic schema diff calls a relaxation additive.
    const headers = op => (op.parameters ?? []).filter(p => p.in === 'header');
    if (!isDeepStrictEqual(headers(old), headers(next))) throw new Error('Request precondition/header contract changed');
    for (const [status, response] of Object.entries(old.responses ?? {})) {
      if (!isDeepStrictEqual(response.headers, next.responses?.[status]?.headers)) throw new Error('Response header contract changed');
    }
  }
  return after.size;
}
