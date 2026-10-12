import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { securityCompatibility } from './contracts.mjs';

const tool = fileURLToPath(new URL('../../../../artifacts/tools/oasdiff', import.meta.url));
export function requireCleanDiff(result) {
  if (result.error || ![0, 1].includes(result.status)) throw new Error('Compatibility tool did not complete', { cause: result.error });
  const findings = JSON.parse(result.stdout);
  if (!Array.isArray(findings) || result.status !== 0 || findings.length !== 0) throw new Error('Breaking or review-required contract change');
}

export function compatibility(previous, current, surface, allowEmpty = false) {
  securityCompatibility(previous, current, surface, allowEmpty);
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'identity-diff-'));
  try {
    const oldFile = path.join(directory, 'before.json'), newFile = path.join(directory, 'after.json');
    fs.writeFileSync(oldFile, JSON.stringify(previous)); fs.writeFileSync(newFile, JSON.stringify(current));
    const result = spawnSync(tool, ['breaking', oldFile, newFile, '--format', 'json', '--fail-on', 'WARN', '--allow-external-refs=false'], { encoding: 'utf8', timeout: 30000 });
    requireCleanDiff(result);
    return true;
  } finally {
    fs.rmSync(directory, { recursive: true, force: true });
  }
}
