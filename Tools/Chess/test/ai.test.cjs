'use strict';
const test = require('node:test'); const assert = require('node:assert');
const { E, B, setup, acts, play, next, validFen } = require('./helpers.cjs');
const AI = require('../ai.cjs');

test('AI finds mate in one (back-rank) and the engine accepts the move', () => {
  const s = setup('6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1', { cfg: { cyber: false, cards: false } });
  const r = AI.plan(E, { fen: s.fen, difficulty: 'normal' }, s.state);
  assert.ok(r.ok); assert.strictEqual(r.plan.action.from + r.plan.action.to, 'a1a8');
  const res = play(s, r.plan.action.from, r.plan.action.to, r.plan.action.kind);
  assert.ok(s.state.over && s.state.winner === 'w' && s.state.reason === 'checkmate');
  void res;
});
test('AI avoids hanging its queen and captures a free queen', () => {
  const s = setup('4k3/8/8/3q4/8/8/8/3QK3 w - - 0 1', { cfg: { cyber: false, cards: false } });
  const r = AI.plan(E, { fen: s.fen, difficulty: 'hard' }, s.state);
  assert.strictEqual(r.plan.action.from + r.plan.action.to, 'd1d5');
});
test('AI as Black respects legality over many positions (all plans accepted by the engine)', () => {
  const fens = ['r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3', 'r2q1rk1/ppp2ppp/2n1bn2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 8', '8/5pk1/6p1/8/8/6P1/5PK1/3R4 w - - 0 1'];
  for (const fen of fens) {
    const s = setup(fen, { cfg: { cyber: true, cards: true, hazards: true, carCyber: true }, speed: 100 });
    for (let i = 0; i < 8 && !s.state.over; i++) {
      const side = s.state.side;
      const r = AI.plan(E, { fen: s.fen, difficulty: 'normal' }, s.state);
      assert.ok(r.ok, JSON.stringify(r));
      if (r.plan.card) s.fen = E.playCard(s.fen, s.state, r.plan.card, []);
      if (r.plan.cyber) E.useCyber(s.fen, s.state, r.plan.cyber, []);
      const rr = E.act(s.fen, s.state, r.plan.action, s.events = []); s.fen = rr.fen;
      assert.ok(validFen(s.fen));
      if (!s.state.over) next(s);
      void side;
    }
  }
});
test('AI planning does not mutate the caller state (no hidden side effects)', () => {
  const s = setup('r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3', { cfg: { cyber: true, cards: true }, charges: 3 });
  const before = JSON.stringify(s.state);
  AI.plan(E, { fen: s.fen, difficulty: 'hard' }, s.state);
  assert.strictEqual(JSON.stringify(s.state), before);
});
test('AI searches fast enough for a browser: depth-3 plan under 2.5 s in a middlegame', () => {
  const s = setup('r2q1rk1/ppp2ppp/2n1bn2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 8', { cfg: { cyber: false, cards: false } });
  const t = Date.now(); const r = AI.plan(E, { fen: s.fen, difficulty: 'hard' }, s.state);
  console.log('depth', r.depth, 'nodes', r.nodes, 'ms', Date.now() - t);
  assert.ok(Date.now() - t < 2500);
});
test('difficulty is about reasoning quality only: EASY plans use no abilities, BRUTAL searches deeper', () => {
  const s = setup('r2q1rk1/ppp2ppp/2n1bn2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 8', { cfg: { cyber: true, cards: true }, charges: 6 });
  const e = AI.plan(E, { fen: s.fen, difficulty: 'easy' }, s.state);
  assert.ok(!e.plan.card && !e.plan.cyber); assert.strictEqual(e.depth, 1);
  const h = AI.plan(E, { fen: s.fen, difficulty: 'hard' }, s.state); assert.strictEqual(h.depth, 3);
});
test('time budget is honoured', () => {
  const s = setup('r2q1rk1/ppp2ppp/2n1bn2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 8', { cfg: { cyber: false, cards: false } });
  const t = Date.now(); const r = AI.plan(E, { fen: s.fen, difficulty: 'brutal', budgetMs: 300 }, s.state);
  assert.ok(r.ok); assert.ok(Date.now() - t < 1500, 'took ' + (Date.now() - t));
});
