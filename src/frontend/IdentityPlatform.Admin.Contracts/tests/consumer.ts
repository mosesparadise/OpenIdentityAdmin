import createClient from 'openapi-fetch';
import type { paths, components } from './generated/bff-v1.fixture.js';

export async function run() {
  const client = createClient<paths>({ baseUrl: window.location.origin, credentials: 'same-origin' });
  const listed = await client.GET('/bff/api/v1/examples', { params: { query: { pageSize: 25 } } });
  if (listed.response.status !== 200 || listed.data?.items?.[0]?.id !== 'fixture') throw new Error('Typed list failed');
  const current = await client.GET('/bff/api/v1/examples/{id}', { params: { path: { id: 'fixture' } } });
  const version = current.response.headers.get('ETag');
  if (!version) throw new Error('Missing edit version');
  const updated = await client.PUT('/bff/api/v1/examples/{id}', {
    params: { path: { id: 'fixture' }, header: { 'If-Match': version, 'X-CSRF-TOKEN': 'synthetic-proof' } },
    body: { label: 'Updated synthetic label' }
  });
  if (updated.response.status !== 200 || updated.data?.label !== 'Updated synthetic label') throw new Error('Typed update failed');
  return { id: listed.data.items[0].id, label: updated.data.label };
}

// These consumers must remain rejected by the compiler. A swallowed schema regression fails tsc.
const valid: components['schemas']['ExampleUpdate'] = { label: 'Synthetic' };
// @ts-expect-error label remains a string, never an arbitrary number
const wrongType: components['schemas']['ExampleUpdate'] = { label: 42 };
// @ts-expect-error label is required
const missing: components['schemas']['ExampleUpdate'] = {};
void [valid, wrongType, missing];
