import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { adapted, policy, sha256 } from '../tooling/prepare-generator.mjs';

const review = policy();
const file = fileURLToPath(new URL('../' + review.target, import.meta.url));
const require = createRequire(file);
const { readFileFromUrl } = require(file);

/** Exercise the real parser call site with only its network boundary replaced. */
async function request(reader, url, pattern, matched) {
  let calls = 0;
  const result = await reader(url, {
    headers: [{ matches: pattern, name: 'X-Synthetic', value: 'synthetic-header' }],
    customFetch: async (actual, options) => {
      calls++;
      assert.equal(actual, url);
      assert.deepEqual(options.headers, matched ? { 'X-Synthetic': 'synthetic-header' } : {});
      return new Response('synthetic', { headers: { 'content-type': 'application/json' } });
    }
  });
  assert.equal(calls, 1);
  assert.deepEqual(result, { body: 'synthetic', mimeType: 'application/json' });
}

test('Given reviewed parser When URL patterns select headers Then only intended destinations match', async () => {
  for (const [url, pattern, expected] of [
    ['https://api.example.test/schema.json', 'https://api.example.test/**', true],
    ['http://api.example.test/schema.json', 'https://api.example.test/**', false],
    ['https://api.example.test/schema.json', 'api.example.test/**', true],
    ['http://api.example.test/schema.json', '*.example.test/**', true],
    ['https://api.example.test/schema.json?mode=json', 'api.example.test/*?mode=json', true],
    ['https://api.example.test/schema.json?mode=xml', 'api.example.test/*?mode=json', false],
    ['https://api.example.test.evil.invalid/schema.json', 'api.example.test/**', false],
    ['https://api.example.test/other/schema.json', 'api.example.test/schema.json', false],
    ['https://API.example.test/schema.json', 'api.example.test/**', false]
  ]) await request(readFileFromUrl, url, pattern, expected);
});

test('Given current matcher When the import adaptation is omitted Then the genuine call site rejects its incompatible API', async () => {
  const patched = fs.readFileSync(file, 'utf8');
  assert.equal(sha256(patched), review.patchedSha256);
  const original = patched.replace(review.after, review.before);
  assert.equal(sha256(original), review.originalSha256);
  // Evaluate exact reviewed CommonJS source with its actual installed dependencies.
  const module = { exports: {} };
  const load = vm.runInThisContext('(function(exports, require, module, __filename, __dirname) {' + original + '\n})', { filename: file });
  load(module.exports, require, module, file, path.dirname(file));
  await assert.rejects(request(module.exports.readFileFromUrl, 'https://api.example.test/schema.json', 'api.example.test/**', true),
    error => error instanceof TypeError && error.message === 'minimatch is not a function');
  await request(readFileFromUrl, 'https://api.example.test/schema.json', 'api.example.test/**', true);
});

test('Given reviewed upstream bytes When preparation runs Then only the exact idempotent import adaptation is accepted', () => {
  const patched = fs.readFileSync(file, 'utf8');
  const original = patched.replace(review.after, review.before);
  assert.equal(adapted(original, review), patched);
  assert.equal(adapted(patched, review), patched);
  assert.throws(() => adapted(original + '\n', review), /differs from reviewed upstream bytes/);
  assert.throws(() => adapted(patched + '\n', review), /differs from reviewed upstream bytes/);
});

test('Given an amplifying header pattern When the fixed parser matches Then bounded execution omits the out-of-limit header', () => {
  const child = spawnSync(process.execPath, ['--max-old-space-size=256', fileURLToPath(new URL('./generator-matcher-child.mjs', import.meta.url))],
    { encoding: 'utf8', timeout: 10000 });
  assert.equal(child.error, undefined, 'Bounded matcher child did not complete');
  assert.equal(child.status, 0, 'Bounded matcher child failed');
  assert.deepEqual(JSON.parse(child.stdout), { fetches: 1, matched: false, body: 'synthetic' });
});
