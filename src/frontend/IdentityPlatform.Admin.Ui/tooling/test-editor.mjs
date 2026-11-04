import { build, transform } from 'esbuild';
import { readFile, writeFile, mkdtemp, rm, mkdir, symlink } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
const scratch = await mkdtemp(path.join(os.tmpdir(), 'admin-editor-tests-'));
const modules = ['input','observation','controller','client','lifecycle','focus'];
try {
  await symlink(path.resolve('node_modules'), path.join(scratch,'node_modules'), 'dir');
  for (const name of modules) {
    const source = (await readFile(`Features/Applications/Editor/${name}.ts`, 'utf8')).replace(/from "\.\/(\w+)"/g, 'from "./$1.mjs"');
    await writeFile(path.join(scratch,`${name}.mjs`), (await transform(source,{loader:'ts',target:'es2022'})).code);
  }
  await build({ stdin:{contents:'export { CreateForm, CreateFields } from "./Features/Applications/Editor/CreateForm"; export { UpdateForm } from "./Features/Applications/Editor/UpdateForm"; export { DetailsView } from "./Features/Applications/Editor/DetailsView"; export { ProblemView } from "./Features/Applications/Editor/ProblemView";',resolveDir:process.cwd()}, outfile:path.join(scratch,'views.mjs'),bundle:true,platform:'node',format:'esm',jsx:'automatic',external:['react','react-dom/server','react/jsx-runtime'] });
  await writeFile(path.join(scratch,'editor.test.mjs'),await readFile('tests/editor.test.mjs'));
  let report;
  try { report=execFileSync(process.execPath,['--test','--experimental-test-coverage','--test-coverage-lines=95','--test-coverage-branches=90','--test-coverage-functions=100',`--test-coverage-include=${scratch}/{${modules.join(',')}}.mjs`,'--test-reporter=tap','--test-reporter-destination=stdout','--test-reporter=junit',`--test-reporter-destination=${path.join(scratch,'editor.xml')}`,path.join(scratch,'editor.test.mjs')],{encoding:'utf8',timeout:30000}); }
  catch(error){ console.log(error.stdout);throw new Error('Editor tests or coverage failed'); }
  const inventory=JSON.parse(await readFile('../../../eng/admin-ui/editor-test-identities.json','utf8'));
  const names=[...report.matchAll(/^# Subtest: (.+)$/gm)].map(match=>match[1]);
  if(JSON.stringify(names)!==JSON.stringify(inventory) || !new RegExp(`^# pass ${inventory.length}$`,'m').test(report) || !/^# skipped 0$/m.test(report) || !/^# todo 0$/m.test(report)) throw new Error('Editor test identities differ');
  const covered=[...report.matchAll(/^#\s+(\w+)\.mjs\s+\|/gm)].map(match=>match[1]).sort();
  if(JSON.stringify(covered)!==JSON.stringify([...modules].sort())) throw new Error('Missing editor critical-module coverage');
  for (const match of report.matchAll(/^#\s+\w+\.mjs\s+\|\s+([\d.]+)\s+\|\s+([\d.]+)\s+\|\s+([\d.]+)/gm)) {
    if (+match[1] < 95 || +match[2] < 90 || +match[3] < 100) throw new Error('Editor module coverage below required floor');
  }
  await mkdir('test-results',{recursive:true});
  await writeFile('test-results/editor.xml',await readFile(path.join(scratch,'editor.xml')));
  await writeFile('test-results/editor.tap',report.replaceAll(scratch,'<compiled-editor>'));
  console.log(report);
} finally { await rm(scratch,{recursive:true,force:true}); }
