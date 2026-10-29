import { test } from 'node:test';
import assert from 'node:assert/strict';
import { observe } from './observation.mjs';
import { SessionController } from './controller.mjs';
import { readSession } from './client.mjs';
const now = Date.parse('2026-10-29T12:00:00Z');
const live = { authenticated: true, subject: 'synthetic-account', expiresAt: '2026-10-29T12:01:00Z' };
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };

test('Given_wire_status_When_observed_Then_only_complete_current_identity_is_exposed', () => {
  assert.deepEqual(observe(live, now), { kind: 'authenticated', subject: live.subject, expires: now + 60_000 });
  assert.deepEqual(observe({ authenticated: false }, now), { kind: 'anonymous' });
  assert.deepEqual(observe({ authenticated: false, subject: null, expiresAt: null }, now), { kind: 'anonymous' });
  assert.deepEqual(observe(live, now + 60_000), { kind: 'elapsed' });
  assert.deepEqual(observe(live, now + 60_001), { kind: 'elapsed' });
  assert.equal(observe({ ...live, expiresAt: '2026-10-29T12:01:00.1234567+00:00' }, now).expires, now + 60_123);
  for (const value of [null, [], true, {}, { authenticated: 'true' }, { ...live, extra: 'token' },
    { ...live, authenticated: false }, { ...live, subject: '' }, { ...live, subject: ' ' }, { ...live, subject: 'a'.repeat(513) },
    { ...live, expiresAt: null }, { ...live, expiresAt: '2026-02-30T12:00:00Z' }, { ...live, expiresAt: '2026-13-01T12:00:00Z' },
    { ...live, expiresAt: '2026-10-29T12:01:00' }, { ...live, expiresAt: '2026-10-29T12:01:00+01:00' }]) assert.throws(() => observe(value, now));
});

test('Given_superseded_reads_When_old_success_or_failure_arrives_Then_only_latest_observation_survives', async () => {
  const reads = [deferred(), deferred(), deferred()]; let count = 0; const signals = []; const states = [];
  const owner = new SessionController(signal => { signals.push(signal); return reads[count++].promise; }, s => states.push(s), () => now);
  const first = owner.check(), second = owner.check();
  assert.equal(signals[0].aborted, true);
  reads[1].resolve(live); await second;
  reads[0].resolve({ authenticated: false }); await first;
  assert.equal(states.at(-1).kind, 'authenticated');
  const third = owner.check(); assert.deepEqual(states.at(-1), { kind: 'loading' });
  reads[2].reject(new Error('private-detail')); await third;
  assert.deepEqual(states.at(-1), { kind: 'unavailable' }); owner.dispose();
});

test('Given_late_rejected_read_When_replaced_Then_it_cannot_erase_new_identity', async () => {
  const old = deferred(); let count = 0; const states = [];
  const owner = new SessionController(() => count++ === 0 ? old.promise : Promise.resolve(live), s => states.push(s), () => now);
  const pending = owner.check(); await owner.check(); old.reject(new Error('old')); await pending;
  assert.equal(states.at(-1).kind, 'authenticated'); owner.dispose();
});

test('Given_pagehide_or_disposal_When_pending_response_finishes_Then_identity_cannot_return', async () => {
  for (const stop of ['suspend', 'dispose']) {
    const pending = deferred(); const states = []; let signal;
    const owner = new SessionController(s => { signal = s; return pending.promise; }, s => states.push(s), () => now);
    const read = owner.check(); owner[stop](); const size = states.length;
    assert.equal(signal.aborted, true); pending.resolve(live); await read; assert.equal(states.length, size);
    if (stop === 'dispose') { await owner.check(); assert.equal(states.length, size); }
    owner.dispose();
  }
});

test('Given_live_observation_When_original_deadline_arrives_Then_timer_erases_identity_without_network', async t => {
  t.mock.timers.enable({ apis: ['setTimeout', 'Date'], now }); let reads = 0; const states = [];
  const owner = new SessionController(async () => { reads++; return live; }, s => states.push(s));
  await owner.check(); t.mock.timers.tick(59_999); assert.equal(states.at(-1).kind, 'authenticated');
  t.mock.timers.tick(1); assert.deepEqual(states.at(-1), { kind: 'elapsed' }); assert.equal(reads, 1); owner.dispose();
});

test('Given_throttled_timer_When_visible_after_deadline_Then_local_check_erases_identity', async () => {
  let clock = now; const states = []; const owner = new SessionController(async () => live, s => states.push(s), () => clock);
  await owner.check(); owner.checkDeadline(); assert.equal(states.at(-1).kind, 'authenticated');
  clock += 60_000; owner.checkDeadline(); assert.deepEqual(states.at(-1), { kind: 'elapsed' });
  owner.suspend(); await owner.check(); assert.equal(states.at(-1).kind, 'elapsed'); owner.dispose();
});

test('Given_invalid_response_When_recheck_completes_Then_prior_identity_is_removed', async () => {
  let value = live; const states = []; const owner = new SessionController(async () => value, s => states.push(s), () => now);
  await owner.check(); value = { authenticated: true }; await owner.check(); assert.deepEqual(states.at(-1), { kind: 'unavailable' });
  value = { authenticated: false }; await owner.check(); assert.deepEqual(states.at(-1), { kind: 'anonymous' }); owner.dispose();
});

test('Given_contract_read_When_sent_Then_one_same_origin_nonrenewing_GET_is_used', async t => {
  globalThis.window = { location: { origin: 'https://bff.example' } }; const requests = [];
  t.mock.method(globalThis, 'fetch', async request => { requests.push(request); return Response.json(live); });
  assert.deepEqual(await readSession(new AbortController().signal), live);
  assert.equal(requests.length, 1); const request = requests[0];
  assert.equal(request.url, 'https://bff.example/bff/session'); assert.equal(request.method, 'GET');
  assert.equal(request.credentials, 'same-origin'); assert.equal(request.cache, 'no-store'); assert.equal(request.redirect, 'error');
  assert.equal(request.headers.has('Authorization'), false); assert.equal(request.body, null);
});

test('Given_bad_transport_When_read_Then_errors_redirects_media_and_oversize_fail_closed', async t => {
  globalThis.window = { location: { origin: 'https://bff.example' } };
  for (const make of [() => new Response('', { status: 503 }), () => new Response('', { status: 302, headers: { location: '/login' } }),
    () => new Response('{}', { headers: { 'content-type': 'text/html' } }), () => new Response('x'.repeat(4097), { headers: { 'content-type': 'application/json' } }),
    () => new Response('{', { headers: { 'content-type': 'application/json' } }), () => { throw new Error('offline'); },
    () => { const response = Response.json(live); Object.defineProperty(response, 'redirected', { value: true }); return response; }]) {
    const mock = t.mock.method(globalThis, 'fetch', async () => make());
    await assert.rejects(readSession(new AbortController().signal)); assert.equal(mock.mock.callCount(), 1); mock.mock.restore();
  }
});

test('Given_pending_fetch_When_caller_aborts_Then_transport_is_cancelled_without_retry', async t => {
  globalThis.window = { location: { origin: 'https://bff.example' } }; const started = deferred();
  const mock = t.mock.method(globalThis, 'fetch', request => new Promise((_, reject) => {
    request.signal.addEventListener('abort', () => reject(request.signal.reason), { once: true }); started.resolve();
  }));
  const owner = new AbortController(); const read = readSession(owner.signal); await started.promise;
  owner.abort(); await assert.rejects(read); assert.equal(mock.mock.callCount(), 1);
});

test('Given_unresponsive_fetch_When_read_deadline_elapses_Then_transport_is_cancelled_without_retry', { timeout: 12_000 }, async t => {
  globalThis.window = { location: { origin: 'https://bff.example' } };
  const mock = t.mock.method(globalThis, 'fetch', request => new Promise((_, reject) => {
    request.signal.addEventListener('abort', () => reject(request.signal.reason), { once: true });
  }));
  const keepAlive = setTimeout(() => {}, 11_000);
  try { await assert.rejects(readSession(new AbortController().signal), { name: 'TimeoutError' }); assert.equal(mock.mock.callCount(), 1); }
  finally { clearTimeout(keepAlive); }
});
