import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { readFileFromUrl } = require('../node_modules/openapi-typescript/node_modules/@redocly/openapi-core/lib/utils.js');
let fetches = 0;
const result = await readFileFromUrl('https://api.example.test/20000000', {
  headers: [{ matches: 'api.example.test/{1..20000000}', name: 'X-Synthetic', value: 'synthetic-header' }],
  customFetch: async (_, options) => {
    fetches++;
    assert.deepEqual(options.headers, {});
    return new Response('synthetic');
  }
});
assert.equal(result.body, 'synthetic');
console.log(JSON.stringify({ fetches, matched: false, body: result.body }));
