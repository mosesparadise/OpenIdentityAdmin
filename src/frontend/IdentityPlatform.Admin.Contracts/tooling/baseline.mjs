import fs from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { compatibility } from './compatibility.mjs';
import { validate } from './validate.mjs';

const root = fileURLToPath(new URL('../../../../', import.meta.url));
const foundationBase = '0c1de1e8b5dbee5f53d0efff7814fdc03dfd6e93';
const files = ['generated/v1.json', 'generated/bff-v1.json', 'tests/generated/v1.fixture.json', 'tests/generated/bff-v1.fixture.json'];
const prefix = 'src/frontend/IdentityPlatform.Admin.Contracts/';
function git(args) {
  const result = spawnSync('git', args, { cwd: root, encoding: 'utf8', timeout: 10000 });
  if (result.error || result.status !== 0) throw new Error('Cannot read the trusted Git baseline');
  return result.stdout;
}

export function requireBaselineInventory(base, paths) {
  if (!/^[a-f0-9]{40}$/.test(base ?? '')) throw new Error('An exact baseline commit SHA is required');
  const present = files.filter(file => paths.has(prefix + file));
  if (present.length !== files.length && !(base === foundationBase && present.length === 0)) {
    throw new Error('Supported baseline inventory is missing or incomplete');
  }
  return present.length > 0;
}

export async function compareBaseline(base) {
  if (!/^[a-f0-9]{40}$/.test(base ?? '')) throw new Error('An exact baseline commit SHA is required');
  const paths = new Set(git(['ls-tree', '-r', '--name-only', base, '--', prefix]).trim().split('\n'));
  const established = requireBaselineInventory(base, paths);
  for (const file of files) {
    const surface = file.includes('bff-v1') ? 'bff' : 'api', allowEmpty = !file.includes('.fixture.');
    const current = JSON.parse(fs.readFileSync(root + prefix + file));
    await validate(current, surface, allowEmpty);
    if (established) {
      const previous = JSON.parse(git(['show', `${base}:${prefix}${file}`]));
      await validate(previous, surface, allowEmpty);
      compatibility(previous, current, surface, allowEmpty);
    }
  }
  return { base, contracts: files.length, initialBaseline: !established };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  console.log(JSON.stringify(await compareBaseline(process.env.IDENTITY_CONTRACT_BASE ?? git(['rev-parse', 'origin/main']).trim())));
}
