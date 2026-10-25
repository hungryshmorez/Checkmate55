'use strict';
const test = require('node:test'); const assert = require('node:assert');
const G = require('../movegen.cjs'); const { Chess } = require('../chess.cjs'); const B = require('../board.cjs');

const PERFT = [
  ['rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1', [20, 400, 8902, 197281]],
  ['r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1', [48, 2039, 97862]],
  ['8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1', [14, 191, 2812, 43238]],
  ['r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1', [6, 264, 9467]],
  ['rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8', [44, 1486, 62379]],
];
for (const [fen, counts] of PERFT) {
  test('perft ' + fen.slice(0, 24), () => {
    const pos = G.fromFen(fen);
    counts.forEach((n, i) => assert.strictEqual(G.perft(pos, i + 1), n, 'depth ' + (i + 1)));
  });
}
test('fast generator agrees with chess.js move lists on random positions', () => {
  let seed = 99; const rnd = () => (seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0) / 4294967296;
  let n = 0;
  for (let g = 0; g < 40; g++) {
    const c = new Chess();
    for (let ply = 0; ply < 100 && !c.game_over(); ply++) {
      const fen = c.fen(), pos = G.fromFen(fen);
      const mine = G.legal(pos).map(m => { const o = G.toObj(m); return o.from + o.to + o.promotion; }).sort();
      const theirs = c.moves({ verbose: true }).map(m => m.from + m.to + (m.promotion || '')).sort();
      assert.deepStrictEqual(mine, theirs, fen); n++;
      assert.strictEqual(G.inCheck(pos, pos.side), c.in_check());
      const ms = c.moves(); c.move(ms[Math.floor(rnd() * ms.length)]);
    }
  }
  assert.ok(n > 1500);
});
test('make/unmake restores the position exactly', () => {
  const fen = 'r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1';
  const pos = G.fromFen(fen); const before = G.toFen(pos);
  (function walk(d) { if (!d) return; for (const m of G.legal(pos)) { G.make(pos, m); walk(d - 1); G.unmake(pos); } })(3);
  assert.strictEqual(G.toFen(pos), before);
});
