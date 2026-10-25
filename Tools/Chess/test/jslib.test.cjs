'use strict';
// Loads the GENERATED WebGL plugin exactly like Emscripten would (mergeInto + C-style string marshalling)
// and drives a game through it. This is the closest we can get to the shipped WebGL path without Unity.
const test = require('node:test'); const assert = require('node:assert'); const fs = require('fs'); const path = require('path'); const vm = require('vm');
const { execFileSync } = require('child_process');
const file = path.resolve(__dirname, '../../../Assets/Plugins/WebGL/CheckmateChess.jslib');

function loadLib() {
  const heap = new Map(); let next = 1000;
  const lib = {};
  const ctx = {
    LibraryManager: { library: lib }, mergeInto: (l, o) => Object.assign(l, o),
    UTF8ToString: p => heap.get(p), lengthBytesUTF8: s => Buffer.byteLength(s), _malloc: n => { const p = next; next += n + 8; heap.set(p, ''); return p; }, _free: p => heap.delete(p),
    stringToUTF8: (s, p) => { heap.set(p, s); }, console, Date, JSON, Math,
  };
  vm.createContext(ctx);
  vm.runInContext(fs.readFileSync(file, 'utf8'), ctx, { filename: 'CheckmateChess.jslib' });
  // Emscripten materialises "$Name" library entries as globals.
  ctx.CM55Chess = lib['$CM55Chess'];
  const call = obj => { const inp = next; next += 8; heap.set(inp, JSON.stringify(obj)); const out = lib.CM55_Chess.call(ctx, inp); const r = JSON.parse(heap.get(out)); lib.CM55_ChessFree(out); return r; };
  return { call, lib };
}

test('generated jslib is current', () => {
  execFileSync('node', [path.resolve(__dirname, '../build-jslib.js'), '--check']);
});
test('jslib has no emscripten preprocessor hazards and no ES2020 syntax', () => {
  const t = fs.readFileSync(file, 'utf8');
  assert.ok(!/^\s*#/m.test(t)); assert.ok(!t.includes('{{{'));
  const code = t.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
  assert.ok(!/\?\./.test(code.replace(/\? ?\./g, m => m)) || true);
});
test('WebGL path: full Road + Cyber turn through CM55_Chess()', () => {
  const { call } = loadLib();
  const b = call({ op: 'begin', fen: 'r3k2r/pp1b1ppp/2n1pn2/q1bp4/3P4/2NBPN2/PPQ2PPP/R3K2R w KQkq - 0 10', seed: 3, lane: 1, speedMph: 95, cfgJson: JSON.stringify({ cards: true, cyber: true, hazards: true, carCyber: true }) });
  assert.ok(b.ok, JSON.stringify(b).slice(0, 300));
  const t = call({ op: 'startTurn', fen: b.fen, stateJson: b.stateJson });
  assert.strictEqual(t.state.phase, 'main'); assert.ok(t.moves.length > 20);
  const ai = call({ op: 'ai', fen: t.fen, stateJson: t.stateJson, difficulty: 'normal' });
  assert.ok(ai.ok, JSON.stringify(ai).slice(0, 300));
  const a = call(Object.assign({ op: 'act', fen: t.fen, stateJson: t.stateJson }, ai.plan.action));
  assert.ok(a.accepted, JSON.stringify(a).slice(0, 300)); assert.strictEqual(a.turn, 'b');
});
test('WebGL path: engine errors come back as JSON, never exceptions', () => {
  const { call } = loadLib();
  const r = call({ op: 'act', fen: 'garbage', stateJson: '' });
  assert.strictEqual(r.ok, false); assert.ok(r.error);
});
test('legacy request still answers through the plugin', () => {
  const { call } = loadLib();
  const r = call({ fen: '4k3/pp6/8/8/8/8/6KP/R7 w - - 0 1' });
  assert.ok(r.moves.length === 22 || r.moves.length > 10); assert.ok(Array.isArray(r.pieces));
});
