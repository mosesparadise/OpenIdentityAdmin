// Exercise the exact C#-embedded projection with synthetic document objects, never a browser.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../tests/aspire/IdentityPlatform.Administrator.Tests/Features/Browser/HistoryDocumentEvidence.cs', import.meta.url), 'utf8');
const expression = source.match(/private const string Projection = """\n([\s\S]+?)\n\s*""";/)?.[1];
assert.ok(expression);
const target = 'http://127.0.0.1:12345/post';
const seed = 'synthetic-history-server-canary';
const internal = `about:neterror?e=notCached&u=${encodeURIComponent(target)}`;

function project(uri = internal, location = target, ready = 'complete', html = '<html></html>', recovery = false, current = true) {
  const document = { documentURI: uri, location: { href: location }, readyState: ready,
    querySelectorAll: () => recovery ? [{}] : [] };
  const element = { outerHTML: html, ownerDocument: document, isConnected: current };
  document.documentElement = current ? element : {};
  const run = vm.runInNewContext(`(${expression})`, { document, URL });
  return JSON.parse(JSON.stringify(run(element, { target, seed })));
}

assert.deepEqual(project(), { CurrentRoot: true, InternalError: true, ErrorCode: true, Target: true, Location: true,
  Ready: true, Secret: false, Recovery: false });
for (const [uri, key] of [
  [target, 'InternalError'], ['http://127.0.0.1:12345/safe', 'InternalError'],
  [internal.replace('neterror', 'blank'), 'InternalError'], [internal + '#fragment', 'InternalError'],
  [internal.replace('notCached', 'other'), 'ErrorCode'], [internal + '&e=notCached', 'ErrorCode'],
  [internal.replace(encodeURIComponent(target), encodeURIComponent(target + '/other')), 'Target'],
  [internal + '&u=' + encodeURIComponent(target), 'Target'], ['about:neterror?e=notCached', 'Target']
]) assert.equal(project(uri)[key], false);
assert.equal(project(internal, target + '/other').Location, false);
assert.equal(project(internal, target, 'loading').Ready, false);
assert.equal(project(internal, target, 'complete', `<input value="${seed}">`).Secret, true);
assert.equal(project(internal, target, 'complete', '<html></html>', true).Recovery, true);
assert.equal(project(internal, target, 'complete', '<html></html>', false, false).CurrentRoot, false);
console.log('Exact finite projection: 15 synthetic positive/negative controls passed; no native accessibility claim.');
