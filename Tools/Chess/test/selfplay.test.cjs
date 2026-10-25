'use strict';
const test = require('node:test'); const assert = require('node:assert');
const { playGame } = require('../selfplay.cjs'); const { validFen } = require('./helpers.cjs'); const B = require('../board.cjs');
test('AI vs AI plays complete Road + Cyber + hazard games without desync or illegal states', { timeout: 600000 }, () => {
  const fens = ['r1bq1rk1/ppp2ppp/2n2n2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 7', 'r3k2r/pp1bqppp/2nbpn2/3p4/3P1B2/2NBPN2/PPQ2PPP/R3K2R w KQkq - 0 10'];
  let plies = 0, specials = 0;
  for (const [i, fen] of fens.entries()) {
    const r = playGame(fen, { seed: 5 + i, white: 'normal', black: 'normal', maxPlies: 40, onPly: p => { assert.ok(validFen(p.fen)); const b = B.parse(p.fen); assert.ok(!B.inCheck(b, B.other(b.turn)), 'mover left in check'); } });
    plies += r.stats.plies; specials += r.stats.specials;
  }
  assert.ok(plies >= 60, 'played ' + plies + ' plies');
  console.log('selfplay plies', plies, 'specials', specials);
});
test('same seed, same game: AI vs AI is deterministic', { timeout: 600000 }, () => {
  const fen = 'r1bq1rk1/ppp2ppp/2n2n2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 7';
  const a = playGame(fen, { seed: 9, maxPlies: 24, white: 'normal', black: 'normal' }), b = playGame(fen, { seed: 9, maxPlies: 24, white: 'normal', black: 'normal' });
  assert.strictEqual(a.fen, b.fen); assert.strictEqual(JSON.stringify(a.stats), JSON.stringify(b.stats));
});
