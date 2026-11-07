import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { key, count, job, page, accepted } from './observation.mjs';
import { GrantController } from './controller.mjs';
import { grantClient } from './client.mjs';
const now = Date.parse('2026-11-05T12:00:00Z');
const target = 'synthetic-app';
const jobId = 'f27bf8d2-84a7-4fc6-a6ce-309521a0976b';
const requestKey = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const tag = id => `"${createHash('sha256').update(id).digest('hex')}-1"`;
const instant = time => new Date(time).toISOString();
const projection = { id: jobId, applicationId: target, version: tag(jobId), state: 'queued', failure: 'none', authorizationCount: '0', tokenCount: '0', createdAt: instant(now), updatedAt: instant(now), expiresAt: instant(now + 60000) };
const recent = jobs => ({ status: 200, value: { items: jobs, expiresAt: projection.expiresAt } });
const acceptance = value => ({ status: 202, value: { job: value, requestKey }, etag: value.version, location: `/bff/api/v1/applications/${target}/grant-revocations/${jobId}` });
const proof = { status: 200, value: { requestToken: 'synthetic-proof' } };
const base = { details: { id: target, clientId: 'synthetic-client', enabled: false, editable: true }, etag: tag(target), expires: now + 60000 };
function setup(t, overrides = {}, changed = {}, observe = () => {}) {
  let clock = now;
  const states = [], sends = [];
  const client = { recent: async () => recent([]), token: async () => proof, submit: async (...args) => { sends.push(args); return acceptance(projection); }, ...overrides };
  const owner = new GrantController(client, { ...base, ...changed }, value => { states.push(value); observe(value); }, () => clock, () => requestKey);
  t.after(() => owner.dispose());
  return { owner, states, sends, client, advance: value => { clock = value; } };
}
function deferred() { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }

test('Given wire aliases When validated Then UUIDs and Int64 counts remain exact', () => {
  assert.ok(key(jobId));
  for (const value of [null, '', jobId.toUpperCase(), jobId.replaceAll('-', ''), '00000000-0000-0000-0000-000000000000', ` ${jobId}`]) assert.equal(key(value), false);
  for (const value of ['0', '9007199254740993', '9223372036854775807']) assert.ok(count(value));
  for (const value of [0, null, '-1', '+1', '01', ' 1', '1.0', '9223372036854775808']) assert.equal(count(value), false);
});
test('Given hostile progress When observed Then exact identity state chronology and expiry are required', async () => {
  assert.deepEqual(await job(projection, target, now), projection);
  for (const changes of [{ id: requestKey }, { applicationId: 'peer' }, { version: tag(target) }, { state: 'other' }, { failure: 'other' }, { state: 'stopped' }, { failure: 'storage' }, { tokenCount: 4 }, { createdAt: instant(now + 1) }, { updatedAt: instant(now - 1) }, { expiresAt: instant(now) }, { expiresAt: instant(now + 300001) }, { private: 'reject' }]) assert.equal(await job({ ...projection, ...changes }, target, now), null);
  assert.equal(await job(null, target, now), null);
  assert.equal(await job(projection, target, now, requestKey), null);
});
test('Given recent jobs When observed Then bounded unique exact-expiry pages are required', async () => {
  assert.deepEqual(await page(recent([]), target, now), []);
  assert.deepEqual(await page(recent([projection]), target, now), [projection]);
  for (const result of [{ status: 403 }, { status: 200, value: {} }, recent(Array(11).fill(projection)), recent([projection, projection]), recent([{ ...projection, expiresAt: instant(now + 30000) }]), recent([null]), { status: 200, value: { items: [], expiresAt: instant(now) } }]) assert.equal(await page(result, target, now), null);
});
test('Given acceptance When observed Then key target job ETag and Location must all match', async () => {
  assert.deepEqual(await accepted(acceptance(projection), target, requestKey, now), projection);
  for (const changes of [{ status: 200 }, { value: { job: projection, requestKey: jobId } }, { etag: tag(target) }, { location: 'https://untrusted.example/job' }]) assert.equal(await accepted({ ...acceptance(projection), ...changes }, target, requestKey, now), null);
  assert.equal(await accepted(acceptance(projection), target, requestKey, now, requestKey), null);
});
test('Given current permission When explicitly confirmed Then one mutation carries original preconditions', async t => {
  const x = setup(t); await x.owner.refresh();
  assert.equal(await x.owner.submit('security', false), false);
  assert.equal(await x.owner.submit('arbitrary', true), false);
  assert.equal(await x.owner.submit('security', true), true);
  assert.equal(x.sends.length, 1);
  assert.deepEqual(x.sends[0].slice(0, 5), [target, base.etag, requestKey, { reason: 'security', confirm: true }, 'synthetic-proof']);
  assert.equal(x.states.at(-1).jobs[0].state, 'queued');
});
test('Given enabled target or missing version When confirmed Then no mutation is sent', async t => {
  for (const change of [{ details: { ...base.details, enabled: true } }, { etag: null }]) {
    const x = setup(t, {}, change); await x.owner.refresh(); assert.equal(await x.owner.submit('security', true), false); assert.equal(x.sends.length, 0);
  }
});
test('Given an incomplete job When recovery is confirmed Then only the stopped exact job can continue', async t => {
  for (const state of ['queued', 'processing', 'stopped']) {
    const stopped = { ...projection, state, failure: state === 'stopped' ? 'storage' : 'none' };
    const x = setup(t, { recent: async () => recent([stopped]) }); await x.owner.refresh();
    assert.equal(await x.owner.submit('security', true), false);
    assert.equal(await x.owner.submit('security', true, requestKey), false);
    assert.equal(await x.owner.submit('security', true, jobId), state === 'stopped');
    if (state === 'stopped') assert.deepEqual(x.sends[0][3], { reason: 'security', confirm: true, jobVersion: projection.version });
  }
});
test('Given lost acceptance When explicitly checked Then the same key and immutable intent are sent again', async t => {
  const sends = [];
  const x = setup(t, { submit: async (...args) => { sends.push(args); if (sends.length === 1) throw new Error('private failure'); return acceptance(projection); } });
  await x.owner.refresh(); await x.owner.submit('security', true);
  assert.equal(x.states.at(-1).uncertain, true); assert.equal(sends.length, 1);
  await x.owner.recover(); assert.equal(sends.length, 2); assert.deepEqual(sends[0].slice(0, 4), sends[1].slice(0, 4));
  assert.equal(x.states.at(-1).kind, 'ready');
});
test('Given an unresolved original acceptance When recovery is denied Then original uncertainty survives', async t => {
  for (const status of [400, 401, 403, 409, 412, 428, 503]) {
    let calls = 0; const x = setup(t, { submit: async () => { if (++calls === 1) throw new Error('lost'); return { status }; } });
    await x.owner.refresh(); await x.owner.submit('security', true); await x.owner.recover();
    assert.equal(x.states.at(-1).uncertain, true); assert.equal(calls, 2);
  }
});
test('Given fresh or ambiguous intent When CSRF fails Then no send occurs and prior uncertainty is retained', async t => {
  for (const uncertain of [false, true]) for (const failure of ['throw', 403, 'shape']) {
    let bad = false, sends = 0;
    const x = setup(t, { token: async () => { if (!bad) return proof; if (failure === 'throw') throw new Error('private'); return failure === 'shape' ? { status: 200, value: { requestToken: '\n' } } : { status: failure }; }, submit: async () => { sends++; throw new Error('lost'); } });
    await x.owner.refresh(); if (uncertain) await x.owner.submit('security', true); bad = true;
    if (uncertain) await x.owner.recover(); else await x.owner.submit('security', true);
    assert.equal(x.states.at(-1).uncertain, uncertain); assert.equal(sends, uncertain ? 1 : 0);
  }
});
test('Given actual read failures When checked Then permissions are hidden and raw data is never published', async t => {
  for (const failure of [400, 401, 403, 404, 409, 412, 428, 503, 'throw', 'shape']) {
    const x = setup(t, { recent: async () => { if (failure === 'throw') throw new Error('private failure'); return failure === 'shape' ? { status: 200, value: {} } : { status: failure, value: 'private failure' }; } });
    await x.owner.refresh(); assert.equal(x.states.at(-1).kind, 'problem'); assert.equal(await x.owner.submit('security', true), false); assert.equal(JSON.stringify(x.states).includes('private failure'), false);
  }
});
test('Given a pending request When hidden expired or disposed Then late acceptance cannot restore output', async t => {
  for (const boundary of ['hidden', 'expired', 'disposed']) for (const phase of ['read', 'proof', 'send']) {
    const wait = deferred(); const x = setup(t, phase === 'read' ? { recent: () => wait.promise } : phase === 'proof' ? { token: () => wait.promise } : { submit: () => wait.promise });
    if (phase !== 'read') await x.owner.refresh();
    const pending = phase === 'read' ? x.owner.refresh() : x.owner.submit('security', true);
    await Promise.resolve(); await Promise.resolve();
    if (boundary === 'hidden') x.owner.suspend(); else if (boundary === 'expired') { x.advance(base.expires); x.owner.checkDeadline(); } else x.owner.dispose();
    const before = x.states.length; wait.resolve(phase === 'read' ? recent([projection]) : phase === 'proof' ? proof : acceptance(projection)); await pending;
    assert.equal(x.states.length, before); if (boundary !== 'disposed') assert.equal(x.states.at(-1).kind, 'cleared');
  }
});
test('Given overlapping status reads When an older response arrives Then only the current observation is published', async t => {
  const wait = deferred(); let reads = 0; const x = setup(t, { recent: async () => ++reads === 1 ? wait.promise : recent([]) });
  const old = x.owner.refresh(); await x.owner.refresh(); wait.resolve(recent([projection])); await old;
  assert.deepEqual(x.states.at(-1), { kind: 'ready', jobs: [] });
});
test('Given malformed acceptance When submitted Then no progress is invented and recovery stays explicit', async t => {
  for (const result of [{ status: 503 }, { status: 202, value: {} }, { ...acceptance(projection), etag: tag(target) }]) {
    const x = setup(t, { submit: async () => result }); await x.owner.refresh(); await x.owner.submit('security', true);
    assert.deepEqual(x.states.at(-1), { kind: 'problem', reason: 'unavailable', uncertain: true });
  }
});
test('Given generated transport When used Then routes headers cookies and single sends remain bounded', async t => {
  const previousFetch = globalThis.fetch, previousWindow = globalThis.window; const requests = [];
  globalThis.window = { location: { origin: 'https://admin.example.test' } };
  globalThis.fetch = async request => { requests.push(request); return new Response(JSON.stringify(acceptance(projection).value), { status: 202, headers: { 'content-type': 'application/json', 'cache-control': 'no-store', 'x-content-type-options': 'nosniff', etag: projection.version, location: acceptance(projection).location } }); };
  t.after(() => { globalThis.fetch = previousFetch; globalThis.window = previousWindow; });
  const client = grantClient(); const signal = new AbortController().signal;
  const result = await client.submit(target, base.etag, requestKey, { reason: 'security', confirm: true }, 'csrf', signal);
  assert.equal(result.status, 202); assert.equal(requests.length, 1); assert.equal(requests[0].credentials, 'same-origin'); assert.equal(requests[0].redirect, 'error'); assert.equal(requests[0].headers.get('authorization'), null); assert.equal(requests[0].headers.get('idempotency-key'), requestKey);
  await client.submit(target, base.etag, requestKey, { reason: 'security', confirm: true, jobVersion: projection.version }, 'csrf', signal, jobId);
  assert.ok(requests[1].url.endsWith(`/${jobId}/continue`));
  await client.recent(target, signal); assert.equal(requests[2].method, 'GET');
  await assert.rejects(() => client.recent('..', signal)); await assert.rejects(() => client.submit(target, base.etag, 'bad', {}, 'csrf', signal));
});

test('Given polling When queued progress changes Then automatic work remains read-only', { timeout: 5000 }, async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  let reads = 0;
  const completed = { ...projection, state: 'succeeded' };
  const published = deferred(), hashing = deferred(), digestEntered = deferred(); let watching = false;
  const x = setup(t, { recent: async () => recent([++reads === 1 ? projection : completed]) }, {}, value => {
    if (watching && value.kind !== 'loading') published.resolve(value);
  });
  await x.owner.refresh(); watching = true;
  const digest = crypto.subtle.digest.bind(crypto.subtle);
  t.mock.method(crypto.subtle, 'digest', async (...args) => { digestEntered.resolve(); await hashing.promise; return digest(...args); });
  t.mock.timers.tick(2000); await digestEntered.promise;
  assert.equal(x.states.at(-1).kind, 'loading'); assert.equal(x.sends.length, 0);
  hashing.resolve(); assert.equal((await published.promise).kind, 'ready');
  assert.equal(reads, 2); assert.equal(x.states.at(-1).jobs[0].state, 'succeeded'); assert.equal(x.sends.length, 0);
  t.mock.timers.tick(2000); assert.equal(reads, 2);
});
test('Given accepted progress When polling runs Then no mutation is repeated', { timeout: 5000 }, async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const published = deferred(); let watching = false;
  const x = setup(t, {}, {}, value => { if (watching && value.kind !== 'loading') published.resolve(value); });
  await x.owner.refresh(); await x.owner.submit('security', true); watching = true; t.mock.timers.tick(2000);
  assert.equal((await published.promise).kind, 'ready');
  assert.equal(x.sends.length, 1); assert.equal(x.states.at(-1).kind, 'ready');
});
test('Given original display deadline When either timer expires Then progress and pending keys are cleared', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  for (const refreshed of [false, true]) {
    const x = setup(t); if (refreshed) await x.owner.refresh(); x.advance(base.expires); t.mock.timers.tick(60000);
    assert.equal(x.states.at(-1).kind, 'cleared'); await x.owner.refresh(); await x.owner.recover(); assert.equal(x.sends.length, 0);
  }
});
test('Given terminal recovered acceptance When status is reauthorized Then continuation or a new request is actionable', async t => {
  for (const state of ['stopped', 'succeeded']) {
    let sends = 0;
    const terminal = { ...projection, state, failure: state === 'stopped' ? 'storage' : 'none' };
    const x = setup(t, { recent: async () => recent(sends ? [terminal] : []), submit: async () => { if (++sends === 1) throw new Error('lost'); return acceptance(terminal); } });
    await x.owner.refresh(); await x.owner.submit('security', true); await x.owner.recover();
    assert.equal(x.states.at(-1).jobs[0].state, state);
    assert.equal(await x.owner.submit('security', true, state === 'stopped' ? jobId : undefined), true);
    assert.equal(sends, 3);
  }
});
test('Given default key generation When confirmed Then a fresh canonical UUID binds acceptance', async t => {
  let captured;
  const owner = new GrantController({ recent: async () => recent([]), token: async () => proof, submit: async (_id, _version, id) => {
    captured = id; return { ...acceptance(projection), value: { job: projection, requestKey: id } };
  } }, base, () => {}, () => now);
  t.after(() => owner.dispose()); await owner.refresh(); await owner.submit('security', true);
  assert.ok(key(captured)); assert.notEqual(captured, requestKey);
});
test('Given an encoded opaque application When accepted Then transport and observation agree on its exact segment', async t => {
  const previousFetch = globalThis.fetch, previousWindow = globalThis.window;
  const id = "synthetic!'()*app", encoded = "synthetic%21%27%28%29%2Aapp";
  const value = { ...projection, applicationId: id };
  const result = { ...acceptance(value), location: `/bff/api/v1/applications/${encoded}/grant-revocations/${jobId}` };
  assert.deepEqual(await accepted(result, id, requestKey, now), value);
  globalThis.window = { location: { origin: 'https://admin.example.test' } };
  globalThis.fetch = async request => { assert.ok(request.url.includes(encoded)); return new Response(null, { status: 403 }); };
  t.after(() => { globalThis.fetch = previousFetch; globalThis.window = previousWindow; });
  assert.equal((await grantClient().recent(id, new AbortController().signal)).status, 403);
});
