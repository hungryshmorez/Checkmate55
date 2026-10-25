'use strict';
// The authored final-confrontation FEN pool lives in C# (FinalPositions.Fens). This test reads it from there so the
// shipped data is what gets validated: legality, side to move, material balance and that the game is playable.
const test = require('node:test'); const assert = require('node:assert'); const fs = require('fs'); const path = require('path');
const { E, B, Chess } = require('./helpers.cjs');
const G = require('../movegen.cjs'); const AI = require('../ai.cjs');
const src = fs.readFileSync(path.resolve(__dirname, '../../../Assets/Checkmate55/Scripts/Core/Notes.cs'), 'utf8');
const block = src.slice(src.indexOf('public static readonly string[] Fens'), src.indexOf('};', src.indexOf('public static readonly string[] Fens')));
const FENS = Array.from(block.matchAll(/"([^"]+)"/g)).map(m => m[1]);
const CAL = Array.from(src.matchAll(/public const string (Calibration\w*)="([^"]+)"/g)).map(m => [m[1], m[2]]);

test('pool has several authored positions', () => assert.ok(FENS.length >= 6, 'positions: ' + FENS.length));
for (const [i, fen] of FENS.entries()) {
  test('final position ' + i + ' is legal, white to move, balanced and playable', () => {
    const c = new Chess(); assert.ok(c.load(fen), 'chess.js accepts it: ' + fen);
    assert.strictEqual(c.turn(), 'w'); assert.ok(!c.in_check()); assert.ok(!c.game_over());
    const b = B.parse(fen);
    assert.strictEqual(b.cells.filter(x => x === 'K').length, 1); assert.strictEqual(b.cells.filter(x => x === 'k').length, 1);
    assert.ok(!B.inCheck(b, 'b'), 'black must not already be in check');
    const wm = B.material(b, 'w', AI.VAL.map ? { p: 100, n: 320, b: 330, r: 500, q: 900, k: 0 } : {}), bm = B.material(b, 'b', { p: 100, n: 320, b: 330, r: 500, q: 900, k: 0 });
    assert.ok(Math.abs(wm - bm) <= 150, 'material is balanced: ' + wm + ' vs ' + bm);
    const ev = AI.evaluate(G.fromFen(fen)); assert.ok(ev > -150 && ev < 250, 'static eval from white view ' + ev);
    assert.ok(c.moves().length >= 20, 'plenty of moves');
  });
}
for (const [name, fen] of CAL) {
  test('calibration position ' + name + ' loads', () => { const c = new Chess(); assert.ok(c.load(fen), fen); assert.strictEqual(c.turn(), 'w'); assert.ok(!c.in_check()); });
}
test('calibration: standard mate in one exists on Calibration1', () => {
  const fen = CAL.find(x => x[0] === 'Calibration1')[1]; const c = new Chess(fen);
  assert.ok(c.moves().some(m => { const d = new Chess(fen); d.move(m); return d.in_checkmate(); }), 'a mating move exists');
});
