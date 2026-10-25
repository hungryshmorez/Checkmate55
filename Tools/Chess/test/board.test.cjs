'use strict';
const test = require('node:test'); const assert = require('node:assert');
const { B, Chess, E } = require('./helpers.cjs');

test('FEN round trip and attack tests agree with chess.js on random playouts', () => {
  let seed = 12345; const rnd = () => (seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296;
  let checked = 0;
  for (let g = 0; g < 60; g++) {
    const c = new Chess();
    for (let ply = 0; ply < 120 && !c.game_over(); ply++) {
      const ms = c.moves(); c.move(ms[Math.floor(rnd() * ms.length)]);
      const fen = c.fen(), b = B.parse(fen);
      assert.strictEqual(B.toFen(b), fen);
      assert.strictEqual(B.inCheck(b, c.turn()), c.in_check(), fen);
      checked++;
    }
  }
  assert.ok(checked > 3000);
});
test('lanes: A-C left, D-E center, F-H right', () => {
  const l = f => E.laneOfFile('abcdefgh'.indexOf(f));
  assert.deepStrictEqual('abcdefgh'.split('').map(l), [0, 0, 0, 1, 1, 2, 2, 2]);
});
test('speed tiers 69/70/89/90/109/110', () => {
  assert.deepStrictEqual([0, 69, 70, 89, 90, 109, 110, 150].map(E.tierOf), [0, 0, 1, 1, 2, 2, 3, 3]);
});
