import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { assignment } from './Applications/Editor/Scopes/observation.mjs';
import { ScopeController } from './Applications/Editor/Scopes/controller.mjs';
import { scopeClient } from './Applications/Editor/Scopes/client.mjs';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { ScopePanel } from './views.mjs';
const now = Date.parse('2026-11-08T00:00:00Z'), id = 'synthetic-app';
const tag = (revision = 1, target = id) => `"${createHash('sha256').update(target).digest('hex')}-${revision}"`;
const instant = value => new Date(value).toISOString();
const result = (scopes = [], revision = 1, expiry = now + 60000) => ({ status: 200, etag: tag(revision), value: { applicationId: id, scopes, expiresAt: instant(expiry) } });
const option = (name, enabled = true) => ({ id: `scope-${name}`, name, displayName: `Display ${name}`, enabled });
const page = (items = [option('email'), option('orders.read')], number = 1, hasMore = false) => ({ status: 200, value: { items, page: number, pageSize: 25, hasMore, expiresAt: instant(now + 60000) } });
const proof = { status: 200, value: { requestToken: 'synthetic-proof' } };
const base = { details: { id, editable: true }, etag: tag(), expires: now + 60000 };
function setup(t, overrides = {}, altered = {}) {
  let clock = now; const states = [], sends = [], changes = [];
  const client = { read: async () => result(), list: async () => page(), token: async () => proof,
    save: async (...args) => { sends.push(args); return result(args[2], 2); }, ...overrides };
  const owner = new ScopeController(client, { ...base, ...altered }, state => states.push(state), () => changes.push(true), () => clock);
  t.after(() => owner.dispose());
  return { owner, client, states, sends, changes, advance: value => { clock = value; } };
}
function deferred() { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }

test('Given assignment output When observed Then exact names identity versions and expiry are required', async () => {
  assert.deepEqual(await assignment(result(['email']), id, now), { scopes: ['email'], etag: tag(), expires: now + 60000 });
  for (const changes of [{ applicationId: 'peer' }, { scopes: null }, { scopes: ['openid'] }, { scopes: ['email', 'email'] }, { scopes: ['orders.read', 'email'] }, { scopes: Array(33).fill('email') }, { scopes: [null] }, { expiresAt: instant(now) }, { expiresAt: instant(now + 300001) }, { extra: true }]) {
    const value = result(); value.value = { ...value.value, ...changes }; assert.equal(await assignment(value, id, now), null);
  }
  for (const invalid of [{ status: 403 }, { status: 200, value: null }, { ...result(), etag: '*' }, { ...result(), etag: tag(1, 'peer') }]) assert.equal(await assignment(invalid, id, now), null);
});
test('Given saved assignment When observed Then requested names and exact successor revision must match', async () => {
  const prior = await assignment(result(), id, now);
  assert.ok(await assignment(result(['email'], 2), id, now, prior, ['email']));
  for (const next of [result(['email'], 1), result(['email'], 3), result(['orders.read'], 2)]) assert.equal(await assignment(next, id, now, prior, ['email']), null);
  assert.equal(await assignment(result(['email'], 2), id, now, { ...prior, expires: now }, ['email']), null);
});
test('Given current assignment When selected and confirmed Then one original-version write is sent', async t => {
  const x = setup(t); await x.owner.load(); x.owner.toggle('email', true);
  await x.owner.save(false); assert.equal(x.sends.length, 0);
  await x.owner.save(true); await x.owner.save(true);
  assert.equal(x.sends.length, 1); assert.deepEqual(x.sends[0].slice(0, 4), [id, tag(), ['email'], 'synthetic-proof']);
  assert.equal(x.states.at(-1).kind, 'saved'); assert.equal(x.changes.length, 1);
});
test('Given selected scopes When catalogue searches change Then hidden selections remain explicitly removable', async t => {
  const x = setup(t); await x.owner.load(); x.owner.toggle('email', true);
  x.client.list = async () => page([option('orders.read')]); await x.owner.search('orders', 1);
  assert.deepEqual(x.states.at(-1).selected, ['email']); x.owner.toggle('orders.read', true);
  x.owner.toggle('email', false); assert.deepEqual(x.states.at(-1).selected, ['orders.read']);
  x.owner.toggle('orders.read', false); await x.owner.save(true); assert.deepEqual(x.sends[0][2], []);
});
test('Given unavailable catalogue permission When current assignments are read Then removal remains possible', async t => {
  const x = setup(t, { read: async () => result(['email']), list: async () => ({ status: 403 }) });
  await x.owner.load(); assert.equal(x.states.at(-1).catalogueDenied, true);
  x.owner.toggle('email', false); await x.owner.save(true); assert.deepEqual(x.sends[0][2], []);
});
test('Given disabled absent duplicate or excessive choices When selected Then no unsupported set is produced', async t => {
  const names = Array.from({ length: 32 }, (_, i) => `scope${String(i).padStart(2, '0')}`);
  const x = setup(t, { read: async () => result(names), list: async () => page([option('disabled', false), option('extra')]) });
  await x.owner.load(); for (const name of ['disabled', 'missing', 'extra', names[0]]) x.owner.toggle(name, true);
  assert.deepEqual(x.states.at(-1).selected, names); x.owner.toggle(names[0], false); x.owner.toggle('extra', true);
  assert.equal(x.states.at(-1).selected.length, 32);
});
test('Given stale denied malformed or failed reads When loaded Then no mutation becomes available', async t => {
  for (const response of [{ status: 401 }, { status: 403 }, { status: 404 }, { status: 409 }, { status: 400 }, result([], 2), { ...result(), value: {} }]) {
    const x = setup(t, { read: async () => response }); await x.owner.load(); await x.owner.save(true); x.owner.toggle('email', true);
    assert.equal(x.states.at(-1).kind, 'problem'); assert.equal(x.sends.length, 0);
  }
  const x = setup(t, { read: async () => { throw new Error('synthetic'); } }); await x.owner.load(); assert.equal(x.states.at(-1).reason, 'unavailable');
});
test('Given invalid or failed catalogue result When searched Then unsafe options cannot be selected', async t => {
  for (const response of [{ status: 401 }, { status: 503 }, page([option('foreign')])]) {
    const x = setup(t); await x.owner.load(); x.client.list = async () => response;
    await x.owner.search('orders', 1); assert.equal(x.states.at(-1).kind, 'problem'); await x.owner.save(true); assert.equal(x.sends.length, 0);
  }
  const x = setup(t); await x.owner.load(); x.client.list = async () => { throw new Error('synthetic'); };
  await x.owner.search('', 1); assert.equal(x.states.at(-1).reason, 'unavailable');
});
test('Given invalid search or initial state When commands are attempted Then no implicit work is dispatched', async t => {
  const x = setup(t); x.owner.toggle('email', true); await x.owner.save(true); await x.owner.search('', 1);
  assert.equal(x.states.length, 0); await x.owner.load(); const before = x.states.length;
  await x.owner.search('x'.repeat(101), 1); await x.owner.search('', 0); await x.owner.search('', 1.1); assert.equal(x.states.length, before);
});
test('Given catalogue request in flight When save is attempted Then it cannot race an unfinished selection observation', async t => {
  const pending = deferred(), x = setup(t); await x.owner.load(); x.client.list = () => pending.promise;
  const work = x.owner.search('', 2); await x.owner.save(true); assert.equal(x.sends.length, 0);
  pending.resolve(page([], 2)); await work; assert.equal(x.states.at(-1).page, 2);
});
test('Given rejected CSRF acquisition When saving Then no mutation is sent', async t => {
  for (const value of [{ status: 401 }, { status: 200, value: {} }, { status: 200, value: { requestToken: 'bad proof' } }]) {
    const x = setup(t, { token: async () => value }); await x.owner.load(); await x.owner.save(true);
    assert.equal(x.sends.length, 0); assert.equal(x.states.at(-1).uncertain, false);
  }
  const x = setup(t, { token: async () => { throw new Error('synthetic'); } }); await x.owner.load(); await x.owner.save(true); assert.equal(x.states.at(-1).uncertain, false);
});
test('Given dispatched save When response is denied lost or invalid Then recovery never repeats the write', async t => {
  for (const status of [400, 401, 403, 409, 412, 428, 503]) {
    const x = setup(t, { save: async () => ({ status }) }); await x.owner.load(); await x.owner.save(true);
    assert.equal(x.states.at(-1).uncertain, status === 401 || status >= 500); await x.owner.save(true); assert.equal(x.changes.length, 0);
  }
  for (const save of [async () => result(['substituted'], 2), async () => { throw new Error('synthetic'); }]) {
    const x = setup(t, { save }); await x.owner.load(); await x.owner.save(true); assert.equal(x.states.at(-1).uncertain, true);
  }
});
test('Given changed lifecycle When read or search finishes late Then cleared data is never republished', async t => {
  for (const stage of ['read', 'list']) {
    const pending = deferred(), x = setup(t, { [stage]: () => pending.promise });
    const work = x.owner.load(); if (stage === 'list') await new Promise(resolve => setImmediate(resolve));
    x.owner.suspend(); pending.resolve(stage === 'read' ? result() : page()); await work;
    assert.equal(x.states.at(-1).kind, 'cleared'); await x.owner.load(); await x.owner.search('', 1); assert.equal(x.states.at(-1).kind, 'cleared');
  }
});
test('Given changed lifecycle When proof or mutation finishes late Then no subsequent send or success is published', async t => {
  for (const stage of ['token', 'save']) {
    const pending = deferred(), x = setup(t, { [stage]: () => pending.promise }); await x.owner.load();
    const work = x.owner.save(true); await new Promise(resolve => setImmediate(resolve)); await x.owner.load();
    x.owner.suspend(); pending.resolve(stage === 'token' ? proof : result([], 2)); await work;
    assert.equal(x.states.at(-1).kind, 'cleared'); assert.equal(x.changes.length, 0);
  }
});
test('Given original deadline When expired before save Then state clears and no mutation is sent', async t => {
  const x = setup(t); await x.owner.load(); x.advance(now + 60000); await x.owner.save(true);
  assert.equal(x.states.at(-1).kind, 'cleared'); assert.equal(x.sends.length, 0); x.owner.checkDeadline();
});
test('Given already expired authority When its real timer fires Then the view clears without any request', async t => {
  const states = [], owner = new ScopeController({}, { ...base, expires: Date.now() - 1 }, state => states.push(state), () => {});
  t.after(() => owner.dispose());
  await new Promise(resolve => setTimeout(resolve, 5));
  assert.equal(states.at(-1).kind, 'cleared'); await owner.load(); assert.equal(states.length, 1);
});
test('Given asynchronous version validation When returned authority expires Then no ready or saved state is published', async t => {
  const original = crypto.subtle.digest.bind(crypto.subtle); t.after(() => { crypto.subtle.digest = original; });
  for (const stage of ['read', 'save']) {
    const x = setup(t, { read: async () => result([], 1, now + 1000), save: async () => result([], 2, now + 500) });
    if (stage === 'save') await x.owner.load();
    crypto.subtle.digest = async (...args) => { const hash = await original(...args); x.advance(now + (stage === 'read' ? 1000 : 500)); return hash; };
    if (stage === 'read') await x.owner.load(); else await x.owner.save(true);
    assert.equal(x.states.at(-1).reason, 'expired'); assert.equal(x.changes.length, 0);
    crypto.subtle.digest = original;
  }
});
test('Given overlapping searches When an older result completes Then only the latest query owns the view', async t => {
  const pending = deferred(), x = setup(t); await x.owner.load(); let calls = 0;
  x.client.list = () => ++calls === 1 ? pending.promise : Promise.resolve(page([option('orders.read')]));
  const earlier = x.owner.search('email', 1); await x.owner.search('orders', 1); pending.resolve(page([option('email')])); await earlier;
  assert.equal(x.states.at(-1).search, 'orders'); assert.equal(x.states.at(-1).choices[0].name, 'orders.read');
});
test('Given disposed owner When deferred failures arrive Then no unmounted state is published', async t => {
  for (const stage of ['read', 'list', 'save']) {
    const pending = deferred(), x = setup(t); if (stage !== 'read') await x.owner.load();
    x.client[stage] = () => pending.promise;
    const work = stage === 'read' ? x.owner.load() : stage === 'list' ? x.owner.search('', 1) : x.owner.save(true);
    await new Promise(resolve => setImmediate(resolve)); x.owner.dispose(); const count = x.states.length;
    pending.reject(new Error('late')); await work; assert.equal(x.states.length, count);
  }
});
test('Given generated browser client When reading and saving Then exact fixed routes carry only native proof and version', async t => {
  const oldWindow = globalThis.window, oldFetch = globalThis.fetch, requests = [];
  globalThis.window = { location: { origin: 'https://bff.example.test' } };
  globalThis.fetch = async request => { requests.push(request); return new Response(JSON.stringify(request.url.endsWith('antiforgery') ? { requestToken: 'proof' } : result().value),
    { status: 200, headers: { 'content-type': 'application/json', 'cache-control': 'no-store', 'x-content-type-options': 'nosniff', etag: tag() } }); };
  t.after(() => { globalThis.window = oldWindow; globalThis.fetch = oldFetch; });
  const client = scopeClient(), signal = new AbortController().signal;
  await client.read(id, signal); await client.save(id, tag(), ['email'], 'proof', signal); await client.token(signal); await client.list('email', 2, signal);
  assert.equal(requests[0].url, 'https://bff.example.test/bff/api/v1/applications/synthetic-app/scopes');
  assert.equal(requests[1].method, 'PUT'); assert.equal(requests[1].headers.get('If-Match'), tag()); assert.equal(requests[1].headers.get('X-CSRF-TOKEN'), 'proof');
  assert.deepEqual(await requests[1].json(), { scopes: ['email'] });
  assert.ok(requests[2].url.endsWith('/applications/antiforgery')); assert.ok(requests[3].url.includes('/scopes?search=email&page=2'));
  assert.ok(requests.every(request => !request.headers.has('authorization') && request.redirect === 'error' && request.credentials === 'same-origin' && request.cache === 'no-store'));
  await client.read('synthetic!app', signal); assert.ok(requests.at(-1).url.endsWith('/synthetic%21app/scopes'));
  await assert.rejects(client.read('..', signal)); await assert.rejects(client.save('antiforgery', tag(), [], 'proof', signal));
});
test('Given scope panel When first rendered Then staged sign-in consequences and loading status are accessible', () => {
  const html = renderToStaticMarkup(React.createElement(ScopePanel, { base, reload: () => {} }));
  assert.match(html, /Application scopes/); assert.match(html, /Users review and approve each request after password and MFA/);
  assert.match(html, /Assignment changes invalidate pending sign-ins and codes/); assert.match(html, /Existing grants are not revoked/);
  assert.match(html, /aria-labelledby="application-scopes-heading"/); assert.match(html, /role="status"/); assert.match(html, /aria-busy="true"/);
});
