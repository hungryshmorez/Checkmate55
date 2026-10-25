'use strict';
// The WebGL plugin is GENERATED from the tested sources. If anyone edits the engine and forgets to regenerate it, the shipped game would
// silently run old rules - so this test fails until `node Tools/Chess/build-jslib.js` has been run.
const test = require('node:test');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const path = require('node:path');
test('CheckmateChess.jslib is up to date with the engine sources', () => {
  const root = path.resolve(__dirname, '..', '..', '..');
  const r = spawnSync(process.execPath, [path.join('Tools', 'Chess', 'build-jslib.js'), '--check'], { cwd: root, encoding: 'utf8' });
  assert.equal(r.status, 0, r.stdout + r.stderr);
});
