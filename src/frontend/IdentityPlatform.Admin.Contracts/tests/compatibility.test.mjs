import fs from 'node:fs';
import assert from 'node:assert/strict';
import test from 'node:test';
import { inventory } from '../tooling/contracts.mjs';
import { validate } from '../tooling/validate.mjs';
import { compatibility } from '../tooling/compatibility.mjs';

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
  test(`rejects ${name}`, () => assert.throws(() => compatibility(fixture, changed(mutate), 'bff')));
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
