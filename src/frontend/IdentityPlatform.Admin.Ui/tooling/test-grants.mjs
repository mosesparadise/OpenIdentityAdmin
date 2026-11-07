import { transform } from 'esbuild';
import { readFile, writeFile, mkdtemp, rm, mkdir, symlink } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
const scratch = await mkdtemp(path.join(os.tmpdir(), 'admin-grant-tests-'));
try {
  await symlink(path.resolve('node_modules'), path.join(scratch, 'node_modules'), 'dir');
  for (const name of ['input', 'observation', 'client']) {
    const source = (await readFile(`Features/Applications/Editor/${name}.ts`, 'utf8')).replace(/from "\.\/(\w+)"/g, 'from "./editor-$1.mjs"');
    await writeFile(path.join(scratch, `editor-${name}.mjs`), (await transform(source, { loader: 'ts', target: 'es2022' })).code);
  }
  for (const name of ['observation', 'controller', 'client']) {
    const source = (await readFile(`Features/Applications/Editor/Grants/${name}.ts`, 'utf8'))
      .replace(/from "\.\.\/(\w+)"/g, 'from "./editor-$1.mjs"').replace(/from "\.\/(\w+)"/g, 'from "./$1.mjs"');
    await writeFile(path.join(scratch, `${name}.mjs`), (await transform(source, { loader: 'ts', target: 'es2022' })).code);
  }
  await writeFile(path.join(scratch, 'grants.test.mjs'), await readFile('tests/grants.test.mjs'));
  let report;
  try { report = execFileSync(process.execPath, ['--test', '--experimental-test-coverage', '--test-coverage-lines=95', '--test-coverage-branches=90', '--test-coverage-functions=100', `--test-coverage-include=${scratch}/{observation,controller,client}.mjs`, '--test-reporter=tap', '--test-reporter-destination=stdout', '--test-reporter=junit', `--test-reporter-destination=${path.join(scratch, 'grants.xml')}`, path.join(scratch, 'grants.test.mjs')], { encoding: 'utf8', timeout: 30000 }); }
  catch (error) { console.log(error.stdout); throw new Error('Grant behavior tests failed'); }
  const expected = JSON.parse(await readFile('../../../eng/admin-ui/grant-test-identities.json', 'utf8'));
  const names = [...report.matchAll(/^# Subtest: (.+)$/gm)].map(match => match[1]);
  if (JSON.stringify(names) !== JSON.stringify(expected) || !new RegExp(`^# pass ${expected.length}$`, 'm').test(report) || !/^# skipped 0$/m.test(report)) throw new Error('Grant test inventory differs');
  const covered = [...report.matchAll(/^#\s+(\w+)\.mjs\s+\|/gm)].map(match => match[1]).sort();
  if (JSON.stringify(covered) !== JSON.stringify(['client', 'controller', 'observation'])) throw new Error('Missing grant module coverage');
  for (const match of report.matchAll(/^#\s+\w+\.mjs\s+\|\s+([\d.]+)\s+\|\s+([\d.]+)\s+\|\s+([\d.]+)/gm)) {
    if (+match[1] < 95 || +match[2] < 90 || +match[3] < 100) throw new Error('Grant module coverage below required floor');
  }
  await mkdir('test-results', { recursive: true });
  await writeFile('test-results/grants.xml', await readFile(path.join(scratch, 'grants.xml')));
  await writeFile('test-results/grants.tap', report.replaceAll(scratch, '<compiled-grants>'));
  console.log(report);
} finally { await rm(scratch, { recursive: true, force: true }); }
