import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';

const packageRoot = fileURLToPath(new URL('../', import.meta.url));
const policyFile = new URL('../../../../eng/contracts/generator-compatibility.json', import.meta.url);
const target = 'node_modules/openapi-typescript/node_modules/@redocly/openapi-core/lib/utils.js';
export const sha256 = value => createHash('sha256').update(value).digest('hex');

/** Load the reviewed single-file adaptation, never a general dependency patch language. */
export function policy() {
  const review = JSON.parse(fs.readFileSync(policyFile));
  if (review.schemaVersion !== 1 || review.target !== target ||
      !/^[a-f0-9]{64}$/.test(review.originalSha256) || !/^[a-f0-9]{64}$/.test(review.patchedSha256) ||
      review.before !== 'const minimatch = require("minimatch");' ||
      review.after !== 'const { minimatch } = require("minimatch");') throw new Error('Unreviewed generator adaptation');
  return review;
}

/** Accept only exact upstream or already adapted bytes; any unrelated change requires review. */
export function adapted(source, review) {
  if (sha256(source) === review.patchedSha256) return source;
  if (sha256(source) !== review.originalSha256 || source.split(review.before).length !== 2) {
    throw new Error('Generator dependency source differs from reviewed upstream bytes');
  }
  const result = source.replace(review.before, review.after);
  if (sha256(result) !== review.patchedSha256) throw new Error('Generator adaptation differs from reviewed output');
  return result;
}

/** Prepare explicitly after npm ci --ignore-scripts; check mode never writes dependency files. */
export function prepare(checkOnly = false) {
  const review = policy(), file = path.join(packageRoot, review.target);
  const source = fs.readFileSync(file, 'utf8');
  if (checkOnly) {
    if (sha256(source) !== review.patchedSha256) throw new Error('Generator compatibility patch is missing or changed');
  } else {
    const result = adapted(source, review);
    if (result !== source) fs.writeFileSync(file, result);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  prepare(process.argv.includes('--check'));
  console.log('Verified the exact development-only generator adaptation.');
}
