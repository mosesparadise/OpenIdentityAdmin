import fs from 'node:fs';
import { pathToFileURL } from 'node:url';
import { BaseResolver, createConfig, lintFromString } from '@redocly/openapi-core';
import { inventory } from './contracts.mjs';

class LocalOnlyResolver extends BaseResolver {
  async loadExternalRef() { throw new Error('External document loading is forbidden'); }
}

export async function validate(document, surface, allowEmpty = false) {
  inventory(document, surface, allowEmpty);
  const config = await createConfig({ rules: {
    struct: 'error', 'no-unresolved-refs': 'error', 'operation-operationId': 'error',
    'operation-operationId-unique': 'error', 'security-defined': 'error'
  } });
  const problems = await lintFromString({ source: JSON.stringify(document), config, externalRefResolver: new LocalOnlyResolver() });
  if (problems.length) throw new Error(JSON.stringify(problems.map(p => ({ rule: p.ruleId, message: p.message }))));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  if (process.argv.length < 3) throw new Error('At least one contract is required');
  for (const file of process.argv.slice(2)) {
    await validate(JSON.parse(fs.readFileSync(file)), file.includes('bff-v1') ? 'bff' : 'api', !file.includes('.fixture.'));
  }
}
