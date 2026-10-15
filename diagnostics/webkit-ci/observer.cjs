'use strict';
// Disposable evidence observer. No browser commands, acceptance decisions or raw output.
const fs = require('node:fs');
const LIMIT = 128;
const METHODS = new Set(['Network.requestWillBeSent', 'Network.loadingFailed',
  'Network.loadingFinished', 'Network.responseReceived', 'Page.frameDetached',
  'Page.frameNavigated', 'Page.didCheckNavigationPolicy']);
function category(value) {
  if (value === undefined || value === null) return 'missing';
  if (value === '') return 'empty';
  if (typeof value !== 'string') return 'unknown';
  for (const [marker, label] of [
    ['Unacceptable TLS certificate', 'certificate'],
    ['Provisiolal navigation canceled.', 'synthetic-coop-cancelled'],
    ['Navigation canceled by policy check', 'policy-cancelled'],
    ['cancelled', 'cancelled'], ['canceled', 'canceled'],
    ['Connection refused', 'connection-refused'], ['Timeout', 'timeout']]) {
    if (value.includes(marker)) return label;
  }
  return 'unknown';
}
function equal(left, right) {
  return typeof left !== 'string' || left.length === 0 || typeof right !== 'string' || right.length === 0
    ? null : left === right;
}
function createObserver(now = () => process.hrtime.bigint()) {
  const started = now();
  const states = [];
  let events = 0, dropped = 0, failed = false, extraNavigations = 0, requestOverflow = false;
  function state(page) { return states.findLast(item => item.page === page); }
  function lifecycle(page, session) {
    return {
      pageClosed: page._page.isClosed(),
      browserConnected: page._browserContext._browser.isConnected(),
      contextClosing: page._browserContext._closedStatus !== 'open',
      hasProvisional: Boolean(page._provisionalPage),
      currentSession: equal(session, page._session?.sessionId),
      provisionalSession: equal(session, page._provisionalPage?._session.sessionId)
    };
  }
  function record(item, source, values = {}) {
    if (!item) return;
    ++events;
    if (events > LIMIT) { ++dropped; return; }
    item.rows.push({ ordinal: events, elapsedMs: Number((now() - started) / 1000000n),
      source, ...values });
  }
  function navigation(page, frame, url) {
    if (states.length === 3) { ++extraNavigations; return; }
    const item = { page, frame: frame._id, url, loader: undefined, requests: [], ambiguous: false,
      matchingRequests: 0, rows: [], mainFrame: frame === page._page.mainFrame() };
    states.push(item);
    record(item, 'navigate-command', { mainFrame: item.mainFrame,
      ...lifecycle(page, page._session?.sessionId) });
  }
  function navigationResult(page, result) {
    const item = state(page);
    if (item) item.loader = result.loaderId;
    record(item, 'navigate-response', { loaderPresent: typeof result.loaderId === 'string' });
  }
  function protocol(page, envelope) {
    const item = state(page);
    if (!item) return;
    const message = JSON.parse(envelope.message);
    if (!METHODS.has(message.method)) return;
    const event = message.params;
    const session = envelope.targetId;
    const provisional = page._provisionalPage;
    const mainFrame = provisional && equal(provisional._session.sessionId, session) === true
      ? provisional._mainFrameId : page._page.mainFrame()._id;
    const values = { session, ...lifecycle(page, session) };
    if (message.method === 'Network.requestWillBeSent') {
      values.exactUrl = event.request.url === item.url;
      values.mainFrame = equal(event.frameId, mainFrame);
      values.document = event.type === 'Document';
      values.loader = event.loaderId;
      const prior = item.requests.find(x => x.id === event.requestId && x.session === session);
      if (prior) item.ambiguous = true;
      if (values.exactUrl && values.mainFrame === true && values.document &&
          (values.currentSession === true || values.provisionalSession === true)) {
        ++item.matchingRequests;
        if (!prior && item.requests.length === 4) requestOverflow = true;
        else if (!prior) item.requests.push({ id: event.requestId, session, loader: event.loaderId });
      }
      values.request = event.requestId;
    } else if (message.method.startsWith('Network.')) {
      values.request = event.requestId;
      if (message.method === 'Network.loadingFailed') {
        values.category = category(event.errorText);
        values.nativeCancelled = typeof event.canceled === 'boolean' ? event.canceled : null;
      }
      if (message.method === 'Network.responseReceived')
        values.status200 = event.response?.status === 200;
    } else if (message.method === 'Page.frameNavigated') {
      values.mainFrame = equal(event.frame.id, mainFrame);
      values.loader = event.frame.loaderId;
      values.exactUrl = event.frame.url === item.url;
    } else {
      values.mainFrame = equal(event.frameId, mainFrame);
      if (message.method === 'Page.didCheckNavigationPolicy')
        values.policyCancelled = event.cancel === true;
    }
    record(item, message.method, values);
  }
  function provisional(page, event) {
    record(state(page), 'Playwright.provisionalLoadFailed', {
      category: category(event.error), loader: event.loaderId,
      exactPageProxy: equal(event.pageProxyId, page._pageProxySession.sessionId),
      initialized: Boolean(page._page.initializedOrUndefined()),
      ...lifecycle(page, page._provisionalPage?._session.sessionId)
    });
  }
  function mappedFailure(page, session, event) {
    record(state(page), 'mapped-loading-failed-entry', {
      category: category(event.errorText), session: session.sessionId,
      request: event.requestId, ...lifecycle(page, session.sessionId)
    });
  }
  function transition(page, kind, event = {}) {
    const allowed = new Set(['target-created', 'target-destroyed', 'target-committed',
      'page-close-command', 'page-did-close', 'browser-disconnected', 'context-close-command']);
    if (!allowed.has(kind)) { failed = true; return; }
    const target = event.targetId ?? event.targetInfo?.targetId ?? event.newTargetId;
    record(state(page), kind, { ...lifecycle(page, target),
      crashed: typeof event.crashed === 'boolean' ? event.crashed : null,
      createdProvisional: typeof event.targetInfo?.isProvisional === 'boolean'
        ? event.targetInfo.isProvisional : null });
  }
  function snapshot() {
    const negative = states[2];
    let negativeTargetShape = false;
    try { if (negative) {
      const parsed = new URL(negative.url);
      negativeTargetShape = parsed.protocol === 'https:' && parsed.hostname === '127.0.0.1' &&
        parsed.pathname === '/alive' && !parsed.search && !parsed.hash && !parsed.username && !parsed.password;
    } } catch { negativeTargetShape = false; }
    const scopeMatches = states.length === 3 && states.every(item => item.mainFrame) &&
      states[0].page === states[1].page && states[0].page !== states[2].page &&
      states[0].page._browserContext === states[2].page._browserContext && negativeTargetShape;
    const phases = states.map((item, index) => ({ navigation: index + 1,
      mainFrame: item.mainFrame, matchingRequests: item.matchingRequests,
      requestBindings: item.requests.length, ambiguousRequestIdentity: item.ambiguous,
      loaderPresent: typeof item.loader === 'string',
      events: item.rows.map(row => {
        // Only this explicit allowlist is serialized; IDs and URLs stay transient.
        const output = {};
        for (const key of ['ordinal', 'elapsedMs', 'source', 'category', 'mainFrame',
          'exactUrl', 'document', 'nativeCancelled', 'status200', 'policyCancelled',
          'exactPageProxy', 'pageClosed', 'browserConnected', 'contextClosing',
          'hasProvisional', 'currentSession', 'provisionalSession', 'crashed',
          'createdProvisional', 'loaderPresent', 'initialized']) {
          if (Object.hasOwn(row, key)) output[key] = row[key];
        }
        if (Object.hasOwn(row, 'request')) {
          const binding = item.requests.find(x => equal(row.request, x.id) === true && equal(row.session, x.session) === true);
          output.binding = binding ? item.requests.indexOf(binding) + 1 : null;
          output.exactRequest = equal(row.request, row.request) !== true || item.requests.length === 0
            ? null : item.requests.some(x => equal(row.request, x.id) === true);
          output.requestSession = equal(row.session, row.session) !== true || equal(row.request, row.request) !== true || item.requests.length === 0
            ? null : Boolean(binding);
          output.exactLoader = item.ambiguous ? null : equal(binding?.loader, item.loader);
        } else if (Object.hasOwn(row, 'loader')) {
          output.exactLoader = equal(row.loader, item.loader);
        }
        return output;
      }) }));
    return { schema: 1, limit: LIMIT, observed: events, dropped, instrumentationFailed: failed,
      extraNavigations, navigationCount: states.length, scopeMatches, requestOverflow,
      retentionComplete: !failed && dropped === 0 && extraNavigations === 0 && !requestOverflow && !states.some(x => x.ambiguous),
      phases };
  }
  const implementation = { navigation, navigationResult, protocol, provisional, mappedFailure, transition };
  const api = { snapshot };
  for (const [name, operation] of Object.entries(implementation)) {
    api[name] = (...args) => { try { operation(...args); } catch { failed = true; } };
  }
  return api;
}
const observer = createObserver();
if (process.env.IDENTITY_WEBKIT_PROVENANCE) {
  process.once('exit', () => {
    try {
      fs.writeFileSync(process.env.IDENTITY_WEBKIT_PROVENANCE,
        JSON.stringify(observer.snapshot(), null, 2) + '\n', { flag: 'wx', mode: 0o600 });
    } catch { /* A missing receipt is a diagnostic failure; never change the native outcome. */ }
  });
}
module.exports = { ...observer, createObserver, category, equal };
