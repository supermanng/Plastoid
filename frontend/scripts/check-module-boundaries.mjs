#!/usr/bin/env node
/*
 * Module boundary check (ADR 0020). Walks src/ and fails when
 *   - a file under src/modules/<a>/ imports from src/modules/<b>/ (b ≠ a), or
 *   - a file under src/shared/ imports from src/modules/.
 * Only relative imports that resolve inside src/ are considered. No dependencies; run with `node`.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SRC = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../src');
const EXTS = ['.js', '.jsx', '.ts', '.tsx', '.css'];
const IMPORT_RE = /(?:\bimport\s*(?:[^'"]*?\sfrom\s*)?|\bexport\s+[^'"]*?\sfrom\s*|\bimport\s*\(\s*|@import\s+)(['"])([^'"]+)\1/g;

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else if (EXTS.includes(path.extname(entry.name))) out.push(full);
  }
  return out;
}

/** 'modules/<name>' for a module file, 'shared' for a shared file, null otherwise. */
function ownerOf(file) {
  const rel = path.relative(SRC, file).split(path.sep);
  if (rel[0] === 'modules' && rel[1]) return `modules/${rel[1]}`;
  if (rel[0] === 'shared') return 'shared';
  return null;
}

function resolveInsideSrc(fromFile, spec) {
  if (!spec.startsWith('.')) return null; // packages, aliases and absolute URLs are not source imports
  const target = path.resolve(path.dirname(fromFile), spec);
  if (!target.startsWith(SRC + path.sep)) return null;
  return target;
}

const offenders = [];
for (const file of walk(SRC)) {
  const owner = ownerOf(file);
  if (!owner) continue;
  const text = fs.readFileSync(file, 'utf8');
  for (const match of text.matchAll(IMPORT_RE)) {
    const spec = match[2];
    const target = resolveInsideSrc(file, spec);
    if (!target) continue;
    const targetOwner = ownerOf(target);
    if (!targetOwner || targetOwner === owner) continue;
    if (targetOwner === 'shared') continue; // modules may use shared
    const line = text.slice(0, match.index).split('\n').length;
    const reason = owner === 'shared' ? 'shared must not import from a module' : `module ${owner.slice(8)} must not import from module ${targetOwner.slice(8)}`;
    offenders.push(`${path.relative(process.cwd(), file)}:${line}  imports '${spec}'  (${reason})`);
  }
}

if (offenders.length) {
  console.error(`Module boundary violations (${offenders.length}):`);
  for (const o of offenders) console.error('  ' + o);
  process.exit(1);
}
console.log('Module boundaries OK.');
