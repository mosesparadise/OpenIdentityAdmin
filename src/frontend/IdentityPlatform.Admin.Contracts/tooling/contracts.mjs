import { isDeepStrictEqual } from 'node:util';

const methods = new Set(['get', 'put', 'post', 'delete', 'patch', 'head', 'options', 'trace']);
export class ContractViolation extends Error {}

function forbidReferences(value) {
  if (!value || typeof value !== 'object') return;
  if ('$ref' in value) throw new ContractViolation('Referenced header semantics require explicit compatibility support');
  for (const child of Object.values(value)) forbidReferences(child);
}

export function inventory(document, surface, allowEmpty = false) {
  if (!/^3\.1\.\d+$/.test(document.openapi) || document.info?.version !== '1') throw new ContractViolation('Unsupported contract version');
  function inspect(value) {
    if (!value || typeof value !== 'object') return;
    for (const [key, child] of Object.entries(value)) {
      if (key === '$dynamicRef' || key === '$dynamicAnchor') throw new ContractViolation('Unsupported dynamic schema resolution');
      if (key === '$ref' && (typeof child !== 'string' || !child.startsWith('#/'))) throw new ContractViolation('External references forbidden');
      inspect(child);
    }
  }
  inspect(document);
  if (document.components?.pathItems) throw new ContractViolation('Unsupported reusable path items');
  const prefix = surface === 'bff' ? '/bff/api/v1/' : '/api/v1/';
  const operations = new Map();
  const ids = new Set();
  for (const [route, item] of Object.entries(document.paths ?? {})) {
    if (item.parameters?.some(p => p.in === 'header' || '$ref' in p)) throw new ContractViolation('Path-level header semantics require explicit compatibility support');
    const status = surface === 'bff' && route === '/bff/session';
    if (!status && !route.startsWith(prefix)) throw new ContractViolation('Route outside management surface');
    for (const [method, op] of Object.entries(item)) {
      if (!methods.has(method)) continue;
      for (const parameter of op.parameters ?? []) {
        if ('$ref' in parameter) throw new ContractViolation('Referenced parameters require explicit compatibility support');
        if (parameter.in === 'header') forbidReferences(parameter);
      }
      for (const response of Object.values(op.responses ?? {})) {
        if ('$ref' in response) throw new ContractViolation('Referenced responses require explicit compatibility support');
        forbidReferences(response.headers);
      }
      if (!op.operationId || ids.has(op.operationId)) throw new ContractViolation('Missing or duplicate operation ID');
      ids.add(op.operationId);
      if (status) {
        requireBrowserStatus(op, method);
        operations.set(`${method} ${route}`, op);
        continue;
      }
      const scheme = surface === 'bff' ? 'BrowserSession' : 'AdminBearer';
      if (!isDeepStrictEqual(op.security, [{ [scheme]: [] }])) throw new ContractViolation('Missing or weakened authentication requirement');
      if (typeof op['x-required-permission'] !== 'string' || !op['x-required-permission']) throw new ContractViolation('Missing permission');
      if (surface === 'bff' && op['x-downstream-api-version'] !== 1) throw new ContractViolation('Missing explicit downstream mapping');
      if (['put', 'patch', 'delete', 'post'].includes(method)) {
        const names = method === 'post' ? [] : ['If-Match'];
        if (surface === 'bff') names.push('X-CSRF-TOKEN');
        for (const name of names) {
          if (!op.parameters?.some(p => p.in === 'header' && p.name.toLowerCase() === name.toLowerCase() && p.required === true)) throw new ContractViolation('Missing concurrency/antiforgery precondition');
        }
      }
      operations.set(`${method} ${route}`, op);
    }
  }
  if (!allowEmpty && operations.size === 0) throw new ContractViolation('Empty representative operation inventory');
  const schemes = document.components?.securitySchemes;
  if (operations.size || (schemes && Object.keys(schemes).length)) {
    if (surface === 'bff') {
      if (Object.keys(schemes ?? {}).join() !== 'BrowserSession' || schemes.BrowserSession.type !== 'apiKey' || schemes.BrowserSession.in !== 'cookie') throw new ContractViolation('Browser contract must contain only cookie authentication');
    } else if (schemes?.AdminBearer?.type !== 'http' || schemes.AdminBearer.scheme !== 'bearer') throw new ContractViolation('Downstream contract must use bearer authentication');
  }
  return operations;
}

export function securityCompatibility(previous, current, surface, allowEmpty = false) {
  const before = inventory(previous, surface, allowEmpty), after = inventory(current, surface, allowEmpty);
  if (before.size && !isDeepStrictEqual(previous.components?.securitySchemes, current.components?.securitySchemes)) throw new ContractViolation('Authentication definition changed');
  for (const [key, old] of before) {
    const next = after.get(key);
    if (!next) throw new ContractViolation('Removed operation');
    for (const property of ['operationId', 'security', 'x-required-permission', 'x-downstream-api-version', 'x-browser-session-status']) {
      if (!isDeepStrictEqual(old[property], next[property])) throw new ContractViolation(`Changed ${property}`);
    }
    // Header semantics need explicit review even where generic schema diff calls a relaxation additive.
    const headers = op => (op.parameters ?? []).filter(p => p.in === 'header');
    if (!isDeepStrictEqual(headers(old), headers(next))) throw new ContractViolation('Request precondition/header contract changed');
    for (const [status, response] of Object.entries(old.responses ?? {})) {
      if (!isDeepStrictEqual(response.headers, next.responses?.[status]?.headers)) throw new ContractViolation('Response header contract changed');
    }
  }
  return after.size;
}

// This one public local read is not a management API or downstream permission decision.
function requireBrowserStatus(op, method) {
  if (method !== 'get' || op.operationId !== 'ReadBrowserSession' || op['x-browser-session-status'] !== true ||
      !isDeepStrictEqual(op.security, [{}, { BrowserSession: [] }]) || op.requestBody || op.parameters?.length ||
      op['x-required-permission'] !== undefined || op['x-downstream-api-version'] !== undefined) {
    throw new ContractViolation('Invalid local browser status boundary');
  }
  if (Object.keys(op.responses ?? {}).sort().join() !== '200,400,503') throw new ContractViolation('Missing status failure outcomes');
  for (const response of Object.values(op.responses)) {
    if (response.headers?.['Cache-Control']?.schema?.const !== 'no-store') throw new ContractViolation('Status response can be cached');
  }
}
