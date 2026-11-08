import { transform, build } from 'esbuild';
import { readFile, writeFile, mkdtemp, rm, mkdir, symlink } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
const scratch = await mkdtemp(path.join(os.tmpdir(), 'admin-scope-tests-'));
const modules = ['observation', 'controller', 'client'];
try {
  await symlink(path.resolve('node_modules'), path.join(scratch, 'node_modules'), 'dir');
  for (const file of [...modules.map(name => `Applications/Editor/Scopes/${name}`), 'Catalogue/input', 'Catalogue/observation', 'Catalogue/client', 'Applications/Editor/input', 'Applications/Editor/observation', 'Applications/Editor/client']) {
    const source = (await readFile(`Features/${file}.ts`, 'utf8')).replace(/from "(\.[^"]+)"/g, 'from "$1.mjs"');
    const destination = path.join(scratch, `${file}.mjs`); await mkdir(path.dirname(destination), { recursive: true });
    await writeFile(destination, (await transform(source, { loader: 'ts', target: 'es2022' })).code);
  }
  await build({ stdin: { contents: 'export { ScopePanel } from "./Features/Applications/Editor/Scopes/ScopePanel";', resolveDir: process.cwd() }, outfile: path.join(scratch, 'views.mjs'), bundle: true, platform: 'node', format: 'esm', jsx: 'automatic', external: ['react', 'react-dom', 'react-dom/server', 'react/jsx-runtime'] });
  await writeFile(path.join(scratch, 'scopes.test.mjs'), await readFile('tests/scopes.test.mjs'));
  let report;
  try { report = execFileSync(process.execPath, ['--test', '--experimental-test-coverage', '--test-coverage-lines=95', '--test-coverage-branches=90', '--test-coverage-functions=100', `--test-coverage-include=${scratch}/Applications/Editor/Scopes/*.mjs`, '--test-reporter=tap', '--test-reporter-destination=stdout', '--test-reporter=junit', `--test-reporter-destination=${path.join(scratch, 'scopes.xml')}`, path.join(scratch, 'scopes.test.mjs')], { encoding: 'utf8', timeout: 30000 }); }
  catch (error) { console.log(error.stdout); throw new Error('Scope assignment tests failed'); }
  const inventory = JSON.parse(await readFile('../../../eng/admin-ui/scopes-test-identities.json', 'utf8'));
  const names = [...report.matchAll(/^# Subtest: (.+)$/gm)].map(match => match[1]);
  if (JSON.stringify(names) !== JSON.stringify(inventory) || !new RegExp(`^# pass ${inventory.length}$`, 'm').test(report) || !/^# skipped 0$/m.test(report) || !/^# todo 0$/m.test(report)) throw new Error('Scope assignment test identities differ');
  const covered = [...report.matchAll(/^#\s+(\w+)\.mjs\s+\|/gm)].map(match => match[1]).sort();
  if (JSON.stringify(covered) !== JSON.stringify([...modules].sort())) throw new Error('Missing scope assignment critical-module coverage');
  for (const match of report.matchAll(/^#\s+\w+\.mjs\s+\|\s+([\d.]+)\s+\|\s+([\d.]+)\s+\|\s+([\d.]+)/gm)) {
    if (+match[1] < 95 || +match[2] < 90 || +match[3] < 100) throw new Error('Scope assignment module coverage below required floor');
  }
  await mkdir('test-results' , { recursive: true });
  await writeFile('test-results/scopes.xml', await readFile(path.join(scratch, 'scopes.xml')));
  await writeFile('test-results/scopes.tap', report.replaceAll(scratch, '<compiled-scopes>'));
  console.log(report);
} finally { await rm(scratch, { recursive: true, force: true }); }
