import { build, transform } from 'esbuild';
import { readFile, writeFile, mkdtemp, rm, mkdir } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
const scratch = await mkdtemp(path.join(os.tmpdir(), 'admin-ui-tests-'));
try {
  for (const name of ['observation', 'controller']) {
    const source = (await readFile(`Features/Session/${name}.ts`, 'utf8')).replace('"./observation"', '"./observation.mjs"');
    await writeFile(path.join(scratch, `${name}.mjs`), (await transform(source, { loader: 'ts', target: 'es2022' })).code);
  }
  await build({ entryPoints: ['Features/Session/client.ts'], outfile: path.join(scratch, 'client.mjs'), bundle: true, platform: 'node', format: 'esm' });
  await writeFile(path.join(scratch, 'session.test.mjs'), await readFile('tests/session.test.mjs'));
  const report = execFileSync(process.execPath, ['--test', '--experimental-test-coverage', '--test-coverage-lines=95',
    '--test-coverage-branches=90', '--test-coverage-functions=100', `--test-coverage-include=${scratch}/{observation,controller}.mjs`,
    '--test-reporter=tap', '--test-reporter-destination=stdout', '--test-reporter=junit',
    `--test-reporter-destination=${path.join(scratch, 'session.xml')}`, path.join(scratch, 'session.test.mjs')], { encoding: 'utf8', timeout: 30_000 });
  if (!/^# tests 11$/m.test(report) || !/^# pass 11$/m.test(report) || !/^# skipped 0$/m.test(report) || !/^# todo 0$/m.test(report))
    throw new Error('Unexpected frontend test inventory');
  const covered = [...report.matchAll(/^#\s+(controller|observation)\.mjs\s+\|/gm)].map(match => match[1]).sort();
  if (JSON.stringify(covered) !== JSON.stringify(['controller', 'observation'])) throw new Error('Missing critical-module coverage');
  await mkdir('test-results', { recursive: true });
  await writeFile('test-results/session.xml', await readFile(path.join(scratch, 'session.xml'))); await writeFile('test-results/session.tap', report.replaceAll(scratch, '<compiled-session>'));
  console.log(report);
} finally { await rm(scratch, { recursive: true, force: true }); }
