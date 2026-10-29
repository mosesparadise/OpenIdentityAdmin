import { build, transform } from 'esbuild';
import { readFile, writeFile, mkdtemp, rm, mkdir } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
const scratch = await mkdtemp(path.join(os.tmpdir(), 'admin-application-tests-'));
try {
  for (const name of ['observation', 'controller']) {
    const source = (await readFile(`Features/Applications/${name}.ts`, 'utf8')).replace('"./observation"', '"./observation.mjs"');
    await writeFile(path.join(scratch, `${name}.mjs`), (await transform(source, { loader: 'ts', target: 'es2022' })).code);
  }
  await build({ entryPoints: ['Features/Applications/client.ts'], outfile: path.join(scratch, 'client.mjs'), bundle: true, platform: 'node', format: 'esm' });
  await writeFile(path.join(scratch, 'applications.test.mjs'), await readFile('tests/applications.test.mjs'));
  const report = execFileSync(process.execPath, ['--test', '--experimental-test-coverage', '--test-coverage-lines=95',
    '--test-coverage-branches=90', '--test-coverage-functions=100', `--test-coverage-include=${scratch}/{observation,controller}.mjs`,
    '--test-reporter=tap', '--test-reporter-destination=stdout', '--test-reporter=junit',
    `--test-reporter-destination=${path.join(scratch, 'applications.xml')}`, path.join(scratch, 'applications.test.mjs')], { encoding: 'utf8', timeout: 30_000 });
  if (!/^# tests 9$/m.test(report) || !/^# pass 9$/m.test(report) || !/^# skipped 0$/m.test(report) || !/^# todo 0$/m.test(report))
    throw new Error('Unexpected frontend test inventory');
  const covered = [...report.matchAll(/^#\s+(controller|observation)\.mjs\s+\|/gm)].map(match => match[1]).sort();
  if (JSON.stringify(covered) !== JSON.stringify(['controller', 'observation'])) throw new Error('Missing critical-module coverage');
  await mkdir('test-results', { recursive: true });
  await writeFile('test-results/applications.xml', await readFile(path.join(scratch, 'applications.xml'))); await writeFile('test-results/applications.tap', report.replaceAll(scratch, '<compiled-applications>'));
  console.log(report);
} finally { await rm(scratch, { recursive: true, force: true }); }
