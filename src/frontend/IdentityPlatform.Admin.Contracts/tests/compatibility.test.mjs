import fs from 'node:fs';
import assert from 'node:assert/strict';
import test from 'node:test';
import { inventory, ContractViolation } from '../tooling/contracts.mjs';
import { validate } from '../tooling/validate.mjs';
import { requireBaselineInventory } from '../tooling/baseline.mjs';
import { compatibility, requireCleanDiff } from '../tooling/compatibility.mjs';

const fixture = JSON.parse(fs.readFileSync(new URL('./generated/bff-v1.fixture.json', import.meta.url)));
const list = '/bff/api/v1/examples', item = list + '/{id}';
const changed = mutate => { const candidate = structuredClone(fixture); mutate(candidate); return candidate; };

test('nonempty native fixture inventory is exact', () => assert.equal(inventory(fixture, 'bff').size, 3));
test('unchanged baseline passes the real diff process', () => assert.equal(compatibility(fixture, fixture, 'bff'), true));
test('additive optional request field passes', () => assert.equal(compatibility(fixture, changed(d => {
  d.components.schemas.ExampleUpdate.properties.note = { type: 'string' };
}), 'bff'), true));

const mutations = {
  'removed operation': d => delete d.paths[item].put,
  'duplicate operation ID': d => { d.paths[item].put.operationId = 'ListExamples'; },
  'missing operation ID': d => delete d.paths[item].put.operationId,
  'security removed': d => delete d.paths[item].put.security,
  'anonymous security alternative': d => { d.paths[item].put.security.push({}); },
  'explicit anonymous security': d => { d.paths[item].put.security = []; },
  'permission weakened': d => { d.paths[item].put['x-required-permission'] = 'examples.read'; },
  'downstream major silently changed': d => { d.paths[item].put['x-downstream-api-version'] = 2; },
  'browser bearer credential added': d => { d.components.securitySchemes.Bearer = { type: 'http', scheme: 'bearer' }; },
  'new POST lacks antiforgery proof': d => { d.paths[list].post = structuredClone(d.paths[item].put); d.paths[list].post.operationId = 'CreateExample'; d.paths[list].post.parameters = []; },
  'antiforgery precondition removed': d => { d.paths[item].put.parameters = d.paths[item].put.parameters.filter(p => p.name !== 'X-CSRF-TOKEN'); },
  'concurrency precondition optional': d => { d.paths[item].put.parameters.find(p => p.name === 'If-Match').required = false; },
  'etag response header removed': d => delete d.paths[item].get.responses['200'].headers.ETag,
  'required response field removed': d => { delete d.components.schemas.Example.properties.id; d.components.schemas.Example.required = d.components.schemas.Example.required.filter(n => n !== 'id'); },
  'new required request field': d => { d.components.schemas.ExampleUpdate.properties.note = { type: 'string' }; d.components.schemas.ExampleUpdate.required.push('note'); },
  'request type changed': d => { d.components.schemas.ExampleUpdate.properties.label.type = 'integer'; },
  'request bound tightened': d => { d.components.schemas.ExampleUpdate.properties.label.maxLength = 40; },
  'nullable output expansion': d => { d.components.schemas.Example.properties.id.type = ['string', 'null']; },
  'response enum expansion': d => { d.components.schemas.ExampleState.enum.push('Unknown'); },
  'external ref': d => { d.components.schemas.External = { $ref: 'https://example.invalid/schema' }; },
  'dynamic ref unsupported': d => { d.components.schemas.External = { $dynamicRef: '#meta' }; },
  'pathItems unsupported': d => { d.components.pathItems = {}; },
  'wrong browser path': d => { d.paths['/api/v1/leak'] = d.paths[list]; }
};
for (const [name, mutate] of Object.entries(mutations)) {
  test(`rejects ${name}`, () => assert.throws(() => compatibility(fixture, changed(mutate), 'bff'), ContractViolation));
}
test('empty fixture cannot substitute for exercised operations', () => assert.throws(() => inventory({ ...fixture, paths: {} }, 'bff')));

test('empty BFF inventory still rejects bearer security definitions', () => {
  const empty = { ...fixture, paths: {}, components: { securitySchemes: { Bearer: { type: 'http', scheme: 'bearer' } } } };
  assert.throws(() => inventory(empty, 'bff', true));
});
test('native schema validator accepts all representative semantics', async () => await validate(fixture, 'bff'));
test('native schema validator rejects unresolved local reference', async () => {
  await assert.rejects(validate(changed(d => { d.components.schemas.ExampleUpdate.properties.label = { $ref: '#/components/schemas/Missing' }; }), 'bff'));
});
test('native schema validator rejects malformed response', async () => {
  await assert.rejects(validate(changed(d => { d.paths[item].put.responses['200'] = { invalid: true }; }), 'bff'));
});
test('diff evidence rejects malformed output and unsuccessful processes', () => {
  requireCleanDiff({ status: 0, stdout: '[]' });
  for (const result of [{ status: 1, stdout: '[]' }, { status: 2, stdout: '[]' }, { status: null, stdout: '[]' },
    { status: 0, stdout: '[]', error: new Error('timeout') }, ...['', 'null', '""', '{"length":0}', '[{}]'].map(stdout => ({ status: 0, stdout }))]) {
    assert.throws(() => requireCleanDiff(result), error => !(error instanceof ContractViolation));
  }
  for (const [status, level] of [[0, 2], [0, 3], [1, 1]]) {
    assert.throws(() => requireCleanDiff({ status, stdout: JSON.stringify([{ id: 'changed', level, text: 'changed' }]) }), error => !(error instanceof ContractViolation));
  }
  for (const [status, level] of [[0, 1], [1, 2], [1, 3]]) {
    assert.throws(() => requireCleanDiff({ status, stdout: JSON.stringify([{ id: 'changed', level, text: 'changed' }]) }), ContractViolation);
  }
});
test('baseline inventory rejects omission except the exact initial commit', () => {
  const base = '0c1de1e8b5dbee5f53d0efff7814fdc03dfd6e93', future = '1'.repeat(40);
  const files = new Set(['generated/v1.json', 'generated/bff-v1.json', 'tests/generated/v1.fixture.json', 'tests/generated/bff-v1.fixture.json'].map(p => 'src/frontend/IdentityPlatform.Admin.Contracts/' + p));
  assert.equal(requireBaselineInventory(base, new Set()), false);
  assert.equal(requireBaselineInventory(future, files), true);
  assert.throws(() => requireBaselineInventory(future, new Set()));
  assert.throws(() => requireBaselineInventory('main', files));
  for (const file of files) {
    const incomplete = new Set(files); incomplete.delete(file);
    assert.throws(() => requireBaselineInventory(base, incomplete));
    assert.throws(() => requireBaselineInventory(future, incomplete));
  }
});
test('unsupported referenced or path-level preconditions fail closed even unchanged', () => {
  const referenced = changed(d => {
    d.components.schemas.EditVersion = { type: 'string', minLength: 1 };
    d.paths[item].put.parameters.find(p => p.name === 'If-Match').schema = { $ref: '#/components/schemas/EditVersion' };
  });
  assert.throws(() => compatibility(referenced, referenced, 'bff'), ContractViolation);
  const relaxed = structuredClone(referenced); relaxed.components.schemas.EditVersion.minLength = 0;
  assert.throws(() => compatibility(referenced, relaxed, 'bff'), ContractViolation);
  for (const mutate of [
    d => { d.paths[item].parameters = [{ in: 'header', name: 'If-Match', required: true, schema: { type: 'string' } }]; },
    d => { d.paths[item].get.responses['200'].headers.ETag = { $ref: '#/components/headers/EditVersion' }; },
    d => { d.paths[item].get.responses['200'] = { $ref: '#/components/responses/Example' }; }
  ]) {
    const unsupported = changed(mutate);
    assert.throws(() => compatibility(unsupported, unsupported, 'bff'), ContractViolation);
  }
});

const browserStatus = JSON.parse(fs.readFileSync(new URL('../generated/bff-v1.browser.json', import.meta.url)));
const statusChanged = mutate => { const candidate = structuredClone(browserStatus); mutate(candidate.paths['/bff/session'].get, candidate); return candidate; };
test('Given dormant browser status When inventoried Then only the explicit public local read is admitted', () => {
  assert.equal(inventory(browserStatus, 'bff').size, 3);
  assert.deepEqual(Object.keys(browserStatus.paths).sort(), ['/bff/api/v1/applications', '/bff/api/v1/applications/antiforgery', '/bff/session']);
  const management = browserStatus.paths['/bff/api/v1/applications'].get;
  assert.deepEqual(management.security, [{ BrowserSession: [] }]);
  assert.equal(management['x-required-permission'], 'applications.read');
  assert.deepEqual(browserStatus.paths['/bff/session'].get.security, [{}, { BrowserSession: [] }]);
  assert.throws(() => inventory(statusChanged((op, doc) => { doc.paths['/bff/api/v1/applications'].get.security = [{}]; }), 'bff'));
  assert.throws(() => inventory(browserStatus, 'api'));
});
test('Given browser status When authentication or cache semantics drift Then validation rejects the contract', () => {
  for (const mutate of [op => { op.security = []; }, op => { delete op['x-browser-session-status']; },
    op => { op.responses['200'].headers['Cache-Control'].schema.const = 'public'; },
    op => { delete op.responses['503']; }, op => { op['x-downstream-api-version'] = 1; }]) {
    assert.throws(() => inventory(statusChanged(mutate), 'bff'));
  }
});
test('Given browser status When a native route or extra verb is substituted Then no broad exception admits it', () => {
  assert.throws(() => inventory(statusChanged((op, doc) => { doc.paths['/bff/login/resume'] = doc.paths['/bff/session']; delete doc.paths['/bff/session']; }), 'bff'));
  assert.throws(() => inventory(statusChanged((op, doc) => { doc.paths['/bff/session'].post = structuredClone(op); }), 'bff'));
});

test('Given application token acquisition When identity cache or method changes Then the exact local boundary fails closed', () => {
  for (const change of [op => { op.security = [{}]; }, op => { delete op.responses['401']; },
    op => { op.responses['200'].headers['Cache-Control'].schema.const = 'public'; },
    op => { op['x-downstream-api-version'] = 1; }, op => { op.operationId = 'GenericToken'; }]) {
    const candidate = structuredClone(browserStatus);
    change(candidate.paths['/bff/api/v1/applications/antiforgery'].get);
    assert.throws(() => inventory(candidate, 'bff'));
  }
  const pathInput = structuredClone(browserStatus);
  pathInput.paths['/bff/api/v1/applications/antiforgery'].parameters = [{ in: 'query', name: 'unexpected', required: false, schema: { type: 'string' } }];
  assert.throws(() => inventory(pathInput, 'bff'));
  const candidate = structuredClone(browserStatus);
  candidate.paths['/bff/api/v1/applications/antiforgery'].post = structuredClone(candidate.paths['/bff/api/v1/applications/antiforgery'].get);
  delete candidate.paths['/bff/api/v1/applications/antiforgery'].get;
  assert.throws(() => inventory(candidate, 'bff'));
});
