'use strict';
const assert = require('node:assert/strict');
const { createObserver, category, equal } = require('./observer.cjs');
let controls = 0;
function test(name, action) { action(); ++controls; }
function fixture() {
  const frame = { _id: 'private-main-frame' };
  const page = { _page: { mainFrame: () => frame, isClosed: () => false,
    initializedOrUndefined: () => true }, _session: { sessionId: 'private-session' },
    _pageProxySession: { sessionId: 'private-proxy' },
    _browserContext: { _closedStatus: 'open', _browser: { isConnected: () => true } } };
  const observer = createObserver(() => 1000000n);
  observer.navigation(page, frame, 'https://127.0.0.1/private-secret-target');
  observer.navigationResult(page, { loaderId: 'private-loader' });
  return { observer, page, frame };
}
function native(f, method, params, session = 'private-session') {
  const envelope = { targetId: session, message: JSON.stringify({ method, params }) };
  const before = JSON.stringify(envelope);
  f.observer.protocol(f.page, envelope);
  assert.equal(JSON.stringify(envelope), before);
}
function request(f, changes = {}, session) {
  native(f, 'Network.requestWillBeSent', { requestId: 'private-request',
    frameId: 'private-main-frame', loaderId: 'private-loader', type: 'Document',
    request: { url: 'https://127.0.0.1/private-secret-target', headers: { Authorization: 'private-bearer-secret' } },
    ...changes }, session);
}
function failure(f, text = 'Unacceptable TLS certificate', changes = {}, session) {
  native(f, 'Network.loadingFailed', { requestId: 'private-request', errorText: text, ...changes }, session);
}
// Independent evidence interpretation, never used by or connected to the actual test oracle.
function nativeCertificate(snapshot) {
  return snapshot.phases.flatMap(x => x.events).some(e =>
    e.source === 'Network.loadingFailed' && e.category === 'certificate' &&
    e.exactRequest === true && e.requestSession === true && e.exactLoader === true);
}
test('valid exact native certificate + privacy', () => {
  const f = fixture(); request(f); failure(f);
  const output = f.observer.snapshot();
  assert.equal(nativeCertificate(output), true);
  assert.equal(output.retentionComplete, true);
  assert.equal(JSON.stringify(output).includes('private-'), false);
});
for (const [name, change, session] of [
  ['wrong URL', { request: { url: 'https://elsewhere/private-other' } }],
  ['wrong frame', { frameId: 'private-subframe' }],
  ['wrong loader', { loaderId: 'private-other-loader' }],
  ['wrong resource', { type: 'Image' }],
  ['wrong request session', {}, 'private-other-session']]) {
  test(name, () => { const f = fixture(); request(f, change, session); failure(f);
    assert.equal(nativeCertificate(f.observer.snapshot()), false); });
}
test('wrong failure session', () => { const f = fixture(); request(f); failure(f, undefined, {}, 'private-other-session');
  assert.equal(nativeCertificate(f.observer.snapshot()), false); });
test('wrong failure request', () => { const f = fixture(); request(f); failure(f, undefined, { requestId: 'private-other' });
  assert.equal(nativeCertificate(f.observer.snapshot()), false); });
for (const value of ['cancelled', 'canceled', '', null, undefined, 'private-unknown-secret', 'ERR_CERT_AUTHORITY_INVALID']) {
  test('noncertificate category', () => { const f = fixture(); request(f);
    native(f, 'Network.loadingFailed', { requestId: 'private-request', errorText: value });
    assert.equal(nativeCertificate(f.observer.snapshot()), false);
    assert.equal(JSON.stringify(f.observer.snapshot()).includes('private-'), false); });
}
test('provisional and mapped provenance remain separate', () => {
  const f = fixture(); request(f); failure(f, 'cancelled');
  f.observer.provisional(f.page, { error: 'Unacceptable TLS certificate', pageProxyId: 'private-proxy', loaderId: 'private-loader' });
  f.observer.mappedFailure(f.page, f.page._session, { requestId: 'private-request', errorText: 'cancelled' });
  const rows = f.observer.snapshot().phases[0].events;
  assert.equal(nativeCertificate(f.observer.snapshot()), false);
  assert.equal(rows.find(x => x.source === 'Playwright.provisionalLoadFailed').category, 'certificate');
  assert.equal(rows.find(x => x.source === 'Playwright.provisionalLoadFailed').exactLoader, true);
  assert.equal(rows.find(x => x.source === 'mapped-loading-failed-entry').category, 'cancelled');
});
test('unrelated page ignored', () => { const f = fixture(); const other = fixture();
  f.observer.provisional(other.page, { error: 'Unacceptable TLS certificate' });
  assert.equal(f.observer.snapshot().observed, 2); });
test('missing native receipt cannot qualify', () => {
  const f = fixture(); request(f);
  assert.equal(nativeCertificate(f.observer.snapshot()), false);
});
test('malformed hook cannot throw and flags evidence failure', () => {
  const f = fixture();
  assert.doesNotThrow(() => f.observer.protocol(f.page, { message: 'private-invalid-json' }));
  assert.equal(f.observer.snapshot().instrumentationFailed, true);
  assert.equal(f.observer.snapshot().retentionComplete, false);
});
test('saturation bounded; no certificate inference', () => {
  const f = fixture(); request(f);
  for (let n = 0; n < 140; ++n) failure(f, 'cancelled');
  failure(f);
  const snapshot = f.observer.snapshot();
  assert.equal(snapshot.phases[0].events.length, 128);
  assert.deepEqual(snapshot.phases[0].events.map(e => e.ordinal), Array.from({ length: 128 }, (_, i) => i + 1));
  assert.equal(snapshot.dropped, 16);
  assert.equal(snapshot.retentionComplete, false);
  assert.equal(nativeCertificate(snapshot), false);
});
test('extra navigation marked and bounded', () => { const f = fixture();
  for (let n = 0; n < 5; ++n) f.observer.navigation(f.page, f.frame, 'https://127.0.0.1/alive');
  const snapshot = f.observer.snapshot();
  assert.equal(snapshot.navigationCount, 3); assert.equal(snapshot.extraNavigations, 3);
  assert.equal(snapshot.retentionComplete, false); });
test('native payloads remain unchanged', () => { const f = fixture(); request(f); failure(f); });
test('synthetic cancellation distinct', () => {
  assert.equal(category('Provisiolal navigation canceled.'), 'synthetic-coop-cancelled');
  assert.equal(category('cancelled'), 'cancelled');
});
test('empty identities stay unknown', () => {
  assert.equal(equal('', ''), null); assert.equal(equal(undefined, undefined), null);
  const f = fixture(); request(f, { loaderId: '' }); failure(f);
  f.observer.navigationResult(f.page, { loaderId: '' });
  assert.equal(nativeCertificate(f.observer.snapshot()), false);
});
test('native failure before command response is retained and late-bound', () => {
  const f = fixture(); f.observer.navigationResult(f.page, {});
  request(f); failure(f); assert.equal(nativeCertificate(f.observer.snapshot()), false);
  f.observer.navigationResult(f.page, { loaderId: 'private-loader' });
  assert.equal(nativeCertificate(f.observer.snapshot()), true);
});
test('same request ID in another valid session remains separately bound', () => {
  const f = fixture(); request(f);
  f.page._provisionalPage = { _session: { sessionId: 'private-provisional' }, _mainFrameId: 'private-new-frame' };
  request(f, { frameId: 'private-new-frame' }, 'private-provisional');
  failure(f, 'Unacceptable TLS certificate', {}, 'private-provisional');
  const snapshot = f.observer.snapshot();
  assert.equal(snapshot.phases[0].requestBindings, 2);
  assert.equal(nativeCertificate(snapshot), true);
});
test('request identity reuse in same session is ambiguous', () => {
  const f = fixture(); request(f); request(f, { loaderId: 'private-other-loader' }); failure(f);
  assert.equal(nativeCertificate(f.observer.snapshot()), false);
  assert.equal(f.observer.snapshot().retentionComplete, false);
});
test('request binding cap is finite', () => {
  const f = fixture();
  for (let n = 0; n < 6; ++n) request(f, { requestId: 'private-' + n });
  const snapshot = f.observer.snapshot();
  assert.equal(snapshot.phases[0].requestBindings, 4);
  assert.equal(snapshot.requestOverflow, true); assert.equal(snapshot.retentionComplete, false);
});
test('wrong proxy and loader provisional event stays explicitly unbound', () => {
  const f = fixture(); f.observer.provisional(f.page, { error: 'Unacceptable TLS certificate',
    loaderId: 'private-other', pageProxyId: 'private-other' });
  const row = f.observer.snapshot().phases[0].events.at(-1);
  assert.equal(row.exactLoader, false); assert.equal(row.exactPageProxy, false);
});
test('three expected navigation owners and target shape bind scope', () => {
  const f = fixture(); f.observer.navigation(f.page, f.frame, 'https://localhost:54321/alive');
  const negative = fixture(); negative.page._browserContext = f.page._browserContext;
  f.observer.navigation(negative.page, negative.frame, 'https://127.0.0.1:54322/alive');
  assert.equal(f.observer.snapshot().scopeMatches, true);
});
for (const shape of ['same-page', 'different-context', 'query', 'wrong-host']) {
  test('invalid navigation scope ' + shape, () => {
    const f = fixture(); f.observer.navigation(f.page, f.frame, 'https://localhost:54321/alive');
    const negative = shape === 'same-page' ? f : fixture();
    if (shape !== 'different-context') negative.page._browserContext = f.page._browserContext;
    const url = shape === 'query' ? 'https://127.0.0.1/alive?other' :
      shape === 'wrong-host' ? 'https://localhost/alive' : 'https://127.0.0.1/alive';
    f.observer.navigation(negative.page, negative.frame, url);
    assert.equal(f.observer.snapshot().scopeMatches, false);
  });
}
for (const change of [{ request: { url: 'https://other/private-secret' } },
  { frameId: 'private-other' }, { type: 'Image' }, { loaderId: 'private-other' }]) {
  test('reused exact identity with unrelated replacement', () => {
    const f = fixture(); request(f); request(f, change); failure(f);
    assert.equal(nativeCertificate(f.observer.snapshot()), false);
    assert.equal(f.observer.snapshot().retentionComplete, false);
  });
}
for (const identity of ['', null, undefined]) {
  test('missing request correlation remains unknown', () => {
    const f = fixture(); request(f);
    native(f, 'Network.loadingFailed', { requestId: identity, errorText: 'Unacceptable TLS certificate' });
    const row = f.observer.snapshot().phases[0].events.at(-1);
    assert.equal(row.exactRequest, null); assert.equal(row.requestSession, null);
  });
  test('missing session correlation remains unknown', () => {
    const f = fixture(); request(f);
    f.observer.protocol(f.page, { targetId: identity, message: JSON.stringify({
      method: 'Network.loadingFailed', params: { requestId: 'private-request', errorText: 'Unacceptable TLS certificate' } }) });
    const row = f.observer.snapshot().phases[0].events.at(-1);
    assert.equal(row.requestSession, null); assert.equal(row.exactLoader, null);
  });
}
console.log(JSON.stringify({ controls, result: 'passed' }));
