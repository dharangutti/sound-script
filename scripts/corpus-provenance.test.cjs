const { test } = require('node:test');
const assert = require('node:assert/strict');
const { sourceUrl, validateEntry, annotateUnresolved, renderSources } = require('./corpus-provenance.cjs');

test('explanatory placeholder never becomes a Commons URL', () => {
  const entry = { lemma: 'example', source: "Pilot clip — no standalone CC-BY/CC0 Commons file found for lemma 'example'", license: 'CC0-1.0' };
  assert.equal(sourceUrl(entry), null);
  assert.ok(validateEntry(entry).includes('unrecognized source must be marked unresolved'));
  const document = { entries: [entry] }; annotateUnresolved(document);
  assert.equal(entry.provenanceStatus, 'unresolved');
  assert.equal(entry.license, 'CC0-1.0');
  assert.deepEqual(validateEntry(entry), []);
  assert.ok(!renderSources(document).includes('https://commons'));
});
test('recognized Commons file is encoded as one title and remains declared', () => {
  const entry = { source: 'Wikimedia Commons — LL-Q1860 (eng)-speaker-a.wav' };
  assert.equal(sourceUrl(entry), 'https://commons.wikimedia.org/wiki/File:LL-Q1860%20(eng)-speaker-a.wav');
  assert.deepEqual(validateEntry(entry), []);
  assert.equal(entry.provenanceStatus, undefined);
});
test('malformed URLs and pretend Commons titles are rejected', () => {
  for (const url of ['narrative note', 'javascript:alert(1)', 'https://commons.wikimedia.org/wiki/File:no%20standalone%20file', 'https://commons.wikimedia.org/wiki/File:../../secret.wav'])
    assert.ok(validateEntry({ sourceUrl: url }).length, url);
});
test('unresolved cannot also claim verification or URL', () => {
  assert.ok(validateEntry({ provenanceStatus: 'unresolved', provenanceNote: 'pending', sourceUrl: 'https://example.org/a.wav' }).length);
  assert.ok(validateEntry({ provenanceStatus: 'unresolved' }).length);
});
test('verified requires evidence, not just a license declaration', () => {
  assert.ok(validateEntry({ provenanceStatus: 'verified', license: 'CC0-1.0', sourceUrl: 'https://example.org/a.wav' }).length);
  assert.ok(validateEntry({ provenanceStatus: 'verified', sourceUrl: 'https://example.org/a.wav', provenanceEvidence: '../outside.md' }).length);
});
test('repository corpus includes five explicitly unresolved records without fabricated links', () => {
  const document = require('../src/SoundScript.Wordbank/Data/corpus/v2026.07/en/lemmas.json');
  for (const entry of document.entries) assert.deepEqual(validateEntry(entry), [], entry.lemma);
  const unresolved = document.entries.filter(e => e.provenanceStatus === 'unresolved');
  assert.deepEqual(unresolved.map(e => e.lemma), ['bobtail', 'dashing', 'sleighing', 'test', 'world']);
  assert.ok(unresolved.every(e => sourceUrl(e) === null));
});
