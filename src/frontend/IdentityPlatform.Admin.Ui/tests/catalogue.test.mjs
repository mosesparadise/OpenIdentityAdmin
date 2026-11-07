import test from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { CatalogueForm } from './views.mjs';
import { draft, validate, addressable, protocol } from './Catalogue/input.mjs';
import { details, page, job, expiry } from './Catalogue/observation.mjs';
import { CatalogueController } from './Catalogue/controller.mjs';
import { catalogueClient } from './Catalogue/client.mjs';
const instant = Date.parse('2026-11-07T12:00:00Z'), until = new Date(instant + 240000).toISOString();
const tag = '"' + 'a'.repeat(64) + '"', next = '"' + 'b'.repeat(64) + '"';
const audience = { id: 'aud-1', resource: 'orders-api', displayName: 'Orders', owner: 'Operations', enabled: true, version: tag, referenced: false, expiresAt: until };
const scope = { id: 'scp-1', name: 'orders.read', displayName: 'Read orders', kind: 'api', enabled: true, consentText: 'Read your orders', audienceId: 'aud-1', resource: 'orders-api', disclosure: 'none', version: tag, referenced: false, expiresAt: until };
const status = { id: '12345678-1234-4234-8234-123456789abc', kind: 'audience', targetId: audience.id, state: 'queued', failure: 'none', createdAt: new Date(instant - 1000).toISOString(), observedAt: new Date(instant).toISOString(), expiresAt: until };
const response = (value = audience, code = 200) => ({ status: code, value: structuredClone(value), etag: value.version, location: `/bff/api/v1/audiences/deletions/${status.id}` });
const list = (items = [{ id: audience.id, name: audience.resource, displayName: audience.displayName, enabled: true }], size = 25) => response({ items, page: 1, pageSize: size, hasMore: false, expiresAt: until });
function fixture(t, kind = 'audience') {
  let now = instant; const calls = [], views = [];
  const client = Object.fromEntries(['list', 'details', 'token', 'save', 'remove', 'status'].map(method => [method, async (...args) => { calls.push([method, ...args]); return method === 'list' ? list(undefined, args[4] ?? 25) : method === 'token' ? { status: 200, value: { requestToken: 'native-proof' } } : method === 'save' ? response({ ...audience, version: args[4] ? next : tag }, args[4] ? 200 : 201) : method === 'remove' ? response(status, 202) : method === 'status' ? response({ ...status, state: 'succeeded' }) : response(kind === 'scope' ? scope : audience); }]));
  const owner = new CatalogueController(kind, client, value => views.push(value), () => now, () => '87654321-1234-4234-8234-123456789abc');
  t.after(() => owner.dispose());
  const session = { kind: 'authenticated', subject: 'operator', expires: instant + 300000 };
  owner.authorize(session);
  return { owner, client, calls, views, session, advance(value) { now = value; } };
}
const deferred = () => { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; };
test('Given scope details When editing Then the name never becomes its resource and private fields never enter input', () => {
  assert.equal(draft(scope).name, 'orders.read'); assert.equal(draft(audience).name, 'orders-api');
  assert.deepEqual(validate('scope', draft(scope)).input, { name: 'orders.read', displayName: 'Read orders', kind: 'api', enabled: true, consentText: 'Read your orders', audienceId: 'aud-1', disclosure: 'none' });
  assert.deepEqual(Object.keys(draft()).sort(), ['audienceId','consentText','disclosure','displayName','enabled','kind','name','owner'].sort());
});
test('Given invalid identifiers and field bounds When validated Then reserved and ambiguous policies are rejected', () => {
  for (const value of ['', 'openid', 'IDENTITYPLATFORM.test', 'bad name', '\u0000', 'a'.repeat(201), null]) assert.equal(protocol(value, true), false);
  for (const value of ['.', '..', 'deletions', 'ANTIFORGERY', 'with space', '', null]) assert.equal(addressable(value), false);
  for (const [key, value] of Object.entries({ name: 'openid', displayName: '', owner: '', enabled: 'yes' })) assert.ok(validate('audience', { ...draft(audience), [key]: value }).errors[key]);
  for (const [key, value] of Object.entries({ kind: 'unknown', consentText: '', audienceId: '' })) assert.ok(validate('scope', { ...draft(scope), [key]: value }).errors[key]);
  for (const name of ['email', 'Email']) {
    const invalid = validate('scope', { ...draft(scope), name });
    assert.ok(invalid.errors.kind); assert.equal(invalid.errors.disclosure, undefined);
  }
  assert.ok(validate('scope', { ...draft(scope), disclosure: 'roles' }).errors.kind);
  assert.ok(validate('scope', { ...draft(scope), kind: 'identity', disclosure: 'roles' }).errors.disclosure);
  assert.ok(validate('scope', { ...draft(scope), name: 'email', kind: 'identity', audienceId: '', disclosure: 'email' }).input);
});
test('Given exact native projections When observed Then valid scope audience page and status remain usable', () => {
  assert.deepEqual(details(response(scope), 'scope', scope.id, instant), scope);
  assert.deepEqual(details(response(), 'audience', audience.id, instant, validate('audience', draft(audience)).input), audience);
  assert.ok(details(response({ ...scope, name: 'email', kind: 'identity', audienceId: null, resource: null, disclosure: 'email' }), 'scope', undefined, instant));
  assert.ok(page(list(), 'audience', 'orders', 1, instant)); assert.ok(job(response(status, 202), 'audience', audience.id, instant));
  assert.equal(expiry(until, instant), instant + 240000);
});
test('Given hostile detail response When observed Then exact fields versions bounds policy and submitted values fail closed', () => {
  for (const patch of [{ extra: 1 }, { id: '..' }, { displayName: '' }, { enabled: 'true' }, { referenced: 0 }, { version: 'bad' }, { expiresAt: new Date(instant).toISOString() }, { owner: '' }, { resource: 'openid' }]) assert.equal(details(response({ ...audience, ...patch }), 'audience', audience.id, instant), null);
  assert.equal(details({ ...response(), etag: next }, 'audience', audience.id, instant), null);
  assert.equal(details(response(), 'audience', 'other', instant), null);
  assert.equal(details(response(), 'audience', undefined, instant, { ...validate('audience', draft(audience)).input, owner: 'Other' }), null);
  for (const patch of [{ name: 'openid' }, { kind: 'machine' }, { disclosure: 'roles' }, { consentText: '' }, { audienceId: null }, { resource: null }, { disclosure: 'email' }, { kind: 'identity', resource: 'unwanted' }]) assert.equal(details(response({ ...scope, ...patch }), 'scope', scope.id, instant), null);
  assert.ok(Number.isNaN(expiry(new Date(instant + 300001).toISOString(), instant)));
});
test('Given hostile list response When observed Then paging shape duplicates and prefix substitutions are rejected', () => {
  const good = list();
  for (const patch of [{ page: '1' }, { pageSize: 100 }, { hasMore: true }, { items: [good.value.items[0], good.value.items[0]] }, { items: [{ ...good.value.items[0], enabled: 1 }] }, { extra: 1 }, { expiresAt: 'never' }, { items: {} }]) assert.equal(page({ ...good, value: { ...good.value, ...patch } }, 'audience', '', 1, instant), null);
  assert.equal(page(good, 'audience', 'other', 1, instant), null); assert.equal(page({ ...good, status: 403 }, 'audience', '', 1, instant), null);
});
test('Given hostile durable status When observed Then job target finite state location and time bindings are enforced', () => {
  for (const patch of [{ id: '0'.repeat(36) }, { kind: 'scope' }, { targetId: 'other' }, { state: 'done' }, { failure: 'authority' }, { observedAt: new Date(instant + 1).toISOString() }, { createdAt: new Date(instant + 1).toISOString() }, { expiresAt: 'never' }, { extra: 1 }]) assert.equal(job(response({ ...status, ...patch }, 202), 'audience', audience.id, instant), null);
  assert.equal(job({ ...response(status, 202), location: '/elsewhere' }, 'audience', audience.id, instant), null);
  assert.equal(job(response(status), 'audience', audience.id, instant, 'other'), null);
  assert.ok(job(response({ ...status, state: 'rejected', failure: 'stale' }), 'audience', audience.id, instant, status.id));
  assert.equal(job(response({ ...status, state: 'rejected', failure: 'none' }), 'audience', audience.id, instant), null);
});
test('Given authorized reader When listing and opening details Then exact current response is displayed', async t => {
  const f = fixture(t); await f.owner.list(); assert.equal(f.owner.state.kind, 'list'); await f.owner.details(audience.id); assert.equal(f.owner.state.kind, 'details');
  const count = f.calls.length; for (const args of [['x'.repeat(101)], ['\u0000'], ['', 0], ['', 10001], ['', 1.5]]) await f.owner.list(...args); assert.equal(f.calls.length, count);
});
test('Given overlapping reads When the older response arrives last Then it cannot replace or shorten the newer view', async t => {
  const f = fixture(t), wait = deferred(); f.client.list = () => wait.promise;
  const stale = f.owner.list(); await f.owner.details(audience.id); wait.resolve(list()); await stale; assert.equal(f.owner.state.kind, 'details');
});
test('Given read failures When no trustworthy response exists Then a finite problem is shown', async t => {
  const f = fixture(t); f.client.details = async () => ({ status: 404 }); await f.owner.details(audience.id); assert.equal(f.owner.state.reason, 'missing');
  f.client.list = async () => { throw Error('private'); }; await f.owner.list(); assert.equal(f.owner.state.reason, 'unavailable');
});
test('Given new and existing entries When submitted Then create and edit use one proof and one exact mutation', async t => {
  const f = fixture(t); f.owner.form(); assert.ok((await f.owner.save(draft())).name); assert.equal(f.calls.length, 0);
  await f.owner.save(draft(audience)); assert.equal(f.owner.state.kind, 'details'); assert.equal(f.calls.filter(x => x[0] === 'save').length, 1);
  f.owner.form(true); await f.owner.save(draft(audience)); assert.equal(f.owner.state.details.version, next);
  const edit = f.calls.filter(x => x[0] === 'save')[1]; assert.equal(edit[5], audience.id); assert.equal(edit[6], tag);
});
test('Given failed or malformed proof When saving Then no mutation is sent and sign-in status is retained', async t => {
  const f = fixture(t); f.client.token = async () => ({ status: 401 }); f.owner.form(); await f.owner.save(draft(audience));
  assert.equal(f.owner.state.reason, 'expired'); assert.equal(f.calls.length, 0); assert.equal(f.owner.state.uncertain, false);
  f.client.token = async () => ({ status: 200, value: { requestToken: 'bad space' } }); f.owner.form(); await f.owner.save(draft(audience)); assert.equal(f.owner.state.reason, 'unavailable');
});
test('Given ambiguous save When transport fails Then the change is never replayed', async t => {
  const f = fixture(t); let sends = 0; f.client.save = async () => { sends++; throw Error('private'); };
  f.owner.form(); await f.owner.save(draft(audience)); assert.equal(f.owner.state.uncertain, true); await f.owner.recover(); assert.equal(sends, 1);
});
test('Given deletion confirmation When accepted Then status polling alone follows admission', async t => {
  const f = fixture(t); await f.owner.details(audience.id); f.owner.confirm(); await f.owner.remove(false); assert.equal(f.owner.state.kind, 'confirm');
  await f.owner.remove(true); assert.equal(f.owner.state.job.state, 'queued'); await f.owner.refresh(); assert.equal(f.owner.state.job.state, 'succeeded'); assert.equal(f.calls.filter(x => x[0] === 'remove').length, 1);
  f.client.status = async () => response(status); await f.owner.refresh(); assert.equal(f.owner.state.kind, 'problem'); assert.equal(f.owner.state.job.state, 'succeeded');
});
test('Given uncertain deletion When explicitly recovered Then only the original key id and version are reused', async t => {
  const f = fixture(t), sends = []; f.client.remove = async (...args) => { sends.push(args); return sends.length === 1 ? { status: 503 } : response(status, 202); };
  await f.owner.details(audience.id); f.owner.confirm(); await f.owner.remove(true); assert.equal(f.owner.state.recoverable, true); await f.owner.recover();
  assert.equal(sends.length, 2); assert.deepEqual(sends[0].slice(0, 4), sends[1].slice(0, 4)); assert.equal(f.owner.state.kind, 'job');
});
test('Given referenced details When deletion is requested Then no confirmation or mutation occurs', async t => {
  const f = fixture(t); f.client.details = async () => response({ ...audience, referenced: true }); await f.owner.details(audience.id); f.owner.confirm(); await f.owner.remove(true);
  assert.equal(f.owner.state.kind, 'details'); assert.equal(f.calls.filter(x => x[0] === 'remove').length, 0);
});
test('Given status failure When explicitly read again Then the known job survives without a mutation retry', async t => {
  const f = fixture(t); await f.owner.details(audience.id); f.owner.confirm(); await f.owner.remove(true);
  f.client.status = async () => { throw Error('private'); }; await f.owner.refresh(); assert.equal(f.owner.state.job.id, status.id);
  f.client.status = async () => response({ ...status, state: 'rejected', failure: 'expired' }); await f.owner.refresh(); assert.equal(f.owner.state.job.failure, 'expired');
});
test('Given original expiry or clock rollback When a checked session is refreshed Then protected state is erased without renewal', async t => {
  const f = fixture(t); await f.owner.details(audience.id); f.owner.authorize({ ...f.session, expires: instant + 600000 }); f.advance(instant + 240000); f.owner.checkDeadline(); assert.equal(f.owner.state.reason, 'cleared');
  const count = f.calls.length; await f.owner.list(); assert.equal(f.calls.length, count);
  f.advance(instant); f.owner.authorize(f.session); await f.owner.details(audience.id); f.advance(instant - 1); f.owner.checkDeadline(); assert.equal(f.owner.state.reason, 'cleared');
});
test('Given hidden view during send When a late result arrives Then neither protected data nor pending recovery survives', async t => {
  const f = fixture(t), wait = deferred(); f.client.save = () => wait.promise; f.owner.form(); const saving = f.owner.save(draft(audience)); await Promise.resolve();
  f.owner.suspend(); wait.resolve(response(audience, 201)); await saving; assert.equal(f.owner.state.reason, 'cleared'); assert.equal(f.owner.state.recoverable, false);
});
test('Given session loss or changed account When state is owned Then old operations are cancelled', async t => {
  const f = fixture(t), wait = deferred(); f.client.details = () => wait.promise; const reading = f.owner.details(audience.id);
  f.owner.authorize({ ...f.session, subject: 'another' }); wait.resolve(response()); await reading; assert.notEqual(f.owner.state.kind, 'details');
  f.owner.authorize({ kind: 'anonymous' }); assert.equal(f.owner.state.reason, 'cleared'); f.owner.dispose(); f.owner.checkDeadline();
});
test('Given scope editor When choices load fail or overlap Then form identity remains current', async t => {
  const f = fixture(t, 'scope'); await f.owner.details(scope.id); f.owner.form(true); await Promise.resolve(); await Promise.resolve(); assert.equal(f.owner.state.base.name, 'orders.read');
  f.client.list = async () => ({ status: 403 }); await f.owner.choices('orders'); assert.equal(f.owner.state.choicesError, 'forbidden');
  f.client.list = async () => { throw Error('private'); }; await f.owner.choices(); assert.equal(f.owner.state.choicesError, 'unavailable');
});
test('Given catalogue form When rendered Then required labels immutable identity and bounded consent controls are accessible', () => {
  const props = { searchAudiences() {}, submit: async () => ({}), cancel() {} };
  const html = renderToStaticMarkup(React.createElement(CatalogueForm, { ...props, kind: 'scope', base: scope, audiences: list().value }));
  assert.match(html, /readOnly=""/); assert.match(html, /for="catalogue-consentText"/); assert.match(html, /orders.read/); assert.match(html, /maxLength="500"/);
  const audienceHtml = renderToStaticMarkup(React.createElement(CatalogueForm, { ...props, kind: 'audience' })); assert.match(audienceHtml, /Owner or team/); assert.doesNotMatch(audienceHtml, /Consent text/);
});
test('Given generated client When all operations are sent Then fixed BFF routes credentials and original headers remain exact', async t => {
  const original = globalThis.fetch, requests = []; globalThis.window = { location: { origin: 'https://admin.example' } };
  globalThis.fetch = async request => { requests.push(request); return new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' } }); };
  t.after(() => { globalThis.fetch = original; delete globalThis.window; });
  const client = catalogueClient(), signal = new AbortController().signal;
  for (const kind of ['scope', 'audience']) {
    await client.list(kind, 'orders', 1, signal); await client.details(kind, "a/b!'()", signal); await client.token(kind, signal);
    await client.save(kind, validate(kind, draft(kind === 'scope' ? scope : audience)).input, 'proof', signal);
    await client.save(kind, validate(kind, draft(kind === 'scope' ? scope : audience)).input, 'proof', signal, 'a/b', tag);
    await client.remove(kind, 'a/b', tag, status.id, 'proof', signal); await client.status(kind, status.id, signal);
  }
  assert.equal(requests.length, 14);
  for (const request of requests) { assert.equal(request.credentials, 'same-origin'); assert.equal(request.redirect, 'error'); assert.equal(request.cache, 'no-store'); assert.equal(request.headers.has('Authorization'), false); assert.match(request.url, /^https:\/\/admin.example\/bff\/api\/v1\/(scopes|audiences)/); }
  assert.match(requests[1].url, /a%2Fb%21%27%28%29$/); assert.equal(requests[4].headers.get('If-Match'), tag); assert.equal(requests[5].headers.get('X-CSRF-TOKEN'), 'proof');
  await assert.rejects(client.details('scope', '..', signal)); await assert.rejects(client.status('scope', 'bad', signal)); assert.equal(requests.length, 14);
});

test('Given account replacement When details form or deletion status exist Then all earlier ownership is erased', async t => {
  for (const state of ['details', 'form', 'job']) {
    const f = fixture(t); await f.owner.details(audience.id);
    if (state === 'form') f.owner.form(true);
    if (state === 'job') { f.owner.confirm(); await f.owner.remove(true); }
    f.owner.authorize({ ...f.session, subject: 'replacement' }); assert.deepEqual(f.owner.state, { kind: 'idle' });
    const count = f.calls.length; await f.owner.refresh(); await f.owner.recover(); assert.equal(f.calls.length, count);
  }
});
test('Given deletion transport failure and lost proof When recovery fails Then uncertainty and the original intent survive', async t => {
  const f = fixture(t); f.client.remove = async () => { throw Error('private'); }; await f.owner.details(audience.id); f.owner.confirm(); await f.owner.remove(true);
  assert.equal(f.owner.state.uncertain, true); f.client.token = async () => ({ status: 403 }); await f.owner.recover(); assert.equal(f.owner.state.reason, 'forbidden'); assert.equal(f.owner.state.recoverable, true);
});
test('Given queued deletion When its polling timer runs Then it sends only a status read', async t => {
  const f = fixture(t); await f.owner.details(audience.id); f.owner.confirm(); await f.owner.remove(true);
  await new Promise(resolve => setTimeout(resolve, 1600)); assert.equal(f.owner.state.job.state, 'succeeded'); assert.equal(f.calls.filter(x => x[0] === 'remove').length, 1);
});
test('Given original observation deadline When the timer fires Then the view clears independently of an action', async t => {
  const f = fixture(t); f.owner.authorize({ ...f.session, expires: instant + 5 }); f.advance(instant + 5);
  await new Promise(resolve => setTimeout(resolve, 15)); assert.equal(f.owner.state.reason, 'cleared');
});
test('Given production key generation When an explicit deletion is confirmed Then a fresh canonical UUID is submitted once', async t => {
  const now = Date.now(), value = { ...audience, expiresAt: new Date(now + 100000).toISOString() }, sent = [];
  const owner = new CatalogueController('audience', { details: async () => response(value), token: async () => ({ status: 200, value: { requestToken: 'proof' } }), remove: async (...args) => { sent.push(args); return { status: 503 }; } }, () => {});
  t.after(() => owner.dispose()); owner.authorize({ kind: 'authenticated', subject: 'operator', expires: now + 100000 });
  await owner.details(value.id); owner.confirm(); await owner.remove(true); assert.equal(sent.length, 1); assert.match(sent[0][3], /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
});

test('Given manual status read When navigation supersedes it Then late success or failure cannot authorize a focus change', async t => {
  for (const fail of [false, true]) {
    const f = fixture(t), wait = deferred();
    await f.owner.details(audience.id); f.owner.confirm(); await f.owner.remove(true);
    f.client.status = async () => { await wait.promise; if (fail) throw Error('private'); return response({ ...status, state: 'succeeded' }); };
    const refreshing = f.owner.refresh();
    await f.owner.details(audience.id); f.owner.form(true); const current = f.owner.state;
    wait.resolve(); assert.equal(await refreshing, false); assert.equal(f.owner.state, current);
  }
});
