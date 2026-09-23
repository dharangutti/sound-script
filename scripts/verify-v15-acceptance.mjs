import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const inventory = JSON.parse(fs.readFileSync(path.join(root, 'docs/v15-requirements.json'), 'utf8'));
const report = fs.readFileSync(path.join(root, 'docs/v15-documentation-reliability-report.md'), 'utf8');
const expected = inventory.requirements.map(r => r.id);
const rows = [...report.matchAll(/^\| (REQ-[A-Z0-9-]+-\d+) \| (.+) \| (.+) \| (PASS|FAIL|N\/A) \| (.+) \|$/gm)];
const actual = rows.map(m => m[1]);
const errors = [];
if (new Set(expected).size !== expected.length || expected.length !== 157) errors.push('Specification inventory must contain 157 unique requirement IDs.');
if (new Set(actual).size !== actual.length) errors.push('Duplicate requirement rows.');
for (const id of expected) if (!actual.includes(id)) errors.push(`Missing ${id}.`);
for (const id of actual) if (!expected.includes(id)) errors.push(`Unexpected ${id}.`);
for (const row of rows) {
    if (row[4] === 'FAIL') errors.push(`${row[1]} is FAIL.`);
    if (row[4] === 'N/A' && row[5].trim().length < 20) errors.push(`${row[1]} needs an explicit N/A reason.`);
}
if (errors.length) { console.error('docs/v15-documentation-reliability-report.md [ACCEPTANCE/INVENTORY] ' + errors.join(' ') + ' Correct the report against docs/v15-requirements.json.'); process.exitCode = 1; }
else console.log(`PASS: ${actual.length} unique requirements, ${rows.filter(m => m[4] === 'PASS').length} PASS, ${rows.filter(m => m[4] === 'N/A').length} justified N/A, none missing or failed.`);
