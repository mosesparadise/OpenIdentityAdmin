import fs from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import openapiTS, { astToString } from 'openapi-typescript';

const root = fileURLToPath(new URL('../../../../', import.meta.url));
const packageRoot = fileURLToPath(new URL('../', import.meta.url));
const check = process.argv.includes('--check');
const inputs = [
  ['actual/v1.json', 'generated/v1.json'],
  ['actual/bff-v1.json', 'generated/bff-v1.json'],
  ['v1.fixture.json', 'tests/generated/v1.fixture.json'],
  ['bff-v1.fixture.json', 'tests/generated/bff-v1.fixture.json']
];
function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map(k => [k, canonical(value[k])]));
  return value;
}
async function output(relative, content) {
  const target = path.join(packageRoot, relative);
  if (check) {
    if (await fs.readFile(target, 'utf8') !== content) throw new Error(`Stale generated output: ${relative}`);
  } else {
    await fs.mkdir(path.dirname(target), { recursive: true });
    await fs.writeFile(target, content);
  }
}
for (const [input, destination] of inputs) {
  const document = canonical(JSON.parse(await fs.readFile(path.join(root, 'artifacts/contracts', input), 'utf8')));
  await output(destination, JSON.stringify(document, null, 2) + '\n');
  if (input.includes('bff-v1')) {
    const types = astToString(await openapiTS(document));
    const again = astToString(await openapiTS(document));
    if (types !== again) throw new Error('Type generation is not deterministic');
    await output(destination.replace('.json', '.d.ts'), types);
  }
}
console.log(check ? 'Verified contract/type regeneration has no drift.' : 'Generated actual inventories and separate synthetic fixture types.');
