'use strict';
// Random full games with every feature switched on. After every single action the position must still
// be a valid chess.js position and every rule invariant from the design document must hold.
const test = require('node:test'); const assert = require('node:assert');
const { E, B, Chess, validFen } = require('./helpers.cjs');

function lcg(seed) { let s = seed >>> 0; return () => (s = (Math.imul(s, 1664525) + 1013904223) >>> 0) / 4294967296; }
const FENS = [
  'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1',
  'r1bq1rk1/ppp2ppp/2np1n2/2b1p3/2B1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 7',
  'r3k2r/pp1b1ppp/2n1pn2/q1bp4/3P4/2NBPN2/PPQ2PPP/R3K2R w KQkq - 0 10',
  '2r2rk1/pp2bppp/2n1pn2/q2p4/3P4/P1NBPN2/1PQ2PPP/2R2RK1 w - - 0 12',
];

function invariants(fen, st, who) {
  assert.ok(validFen(fen), 'chess.js can load ' + fen);
  const b = B.parse(fen);
  const wk = b.cells.filter(c => c === 'K').length, bk = b.cells.filter(c => c === 'k').length;
  assert.strictEqual(wk, 1, 'one white king'); assert.strictEqual(bk, 1, 'one black king');
  assert.strictEqual(b.turn, st.side, 'FEN turn matches state side');
  // the side that just moved is never left in check (hazards/spare rule included)
  assert.ok(!B.inCheck(b, B.other(b.turn)), 'mover left in check after ' + who + ': ' + fen);
  for (let i = 0; i < 64; i++) { const c = b.cells[i]; if (c && c.toLowerCase() === 'p') { const r = B.rankOf(i); assert.ok(r !== 0 && r !== 7, 'pawn on back rank ' + fen); } }
  for (const s of st.shields) assert.ok(b.cells[B.idx(s.sq)], 'shield square has a piece ' + s.sq + ' ' + fen);
  for (const e of st.emp) assert.ok(b.cells[B.idx(e.sq)], 'emp square has a piece');
  for (const h of st.hazards) assert.strictEqual(h.phase, 'WARNING', 'hazards only persist as WARNING between turns');
  const castle = b.castling;
  if (castle.includes('K')) assert.ok(b.cells[4] === 'K' && b.cells[7] === 'R', 'castling flag K valid ' + fen);
  if (castle.includes('Q')) assert.ok(b.cells[4] === 'K' && b.cells[0] === 'R', 'castling flag Q valid ' + fen);
  if (castle.includes('k')) assert.ok(b.cells[60] === 'k' && b.cells[63] === 'r', 'castling flag k valid ' + fen);
  if (castle.includes('q')) assert.ok(b.cells[60] === 'k' && b.cells[56] === 'r', 'castling flag q valid ' + fen);
  const snap = E.snapshot(fen, st);
  assert.strictEqual(snap.pieces.length, b.cells.filter(Boolean).length, 'physical pieces == FEN pieces');
}

function playGame(seed, cfgExtra) {
  const rnd = lcg(seed * 7919 + 1);
  const fen0 = FENS[seed % FENS.length];
  let st = E.newState({ seed, lane: seed % 3, speed: 50 + (seed * 13) % 80, known: { castle: true, ghost: true, reversal: true },
    cfg: Object.assign({ road: true, cyber: true, cards: true, hazards: true, carCyber: true, carCards: false, autoVehicle: true, startHand: 2, hazardRate: [0.5, 0.6, 0.7, 0.9] }, cfgExtra || {}) });
  let fen = fen0; st.side = B.parse(fen).turn;
  for (const c of st.deck.splice(0, 2)) st.w.hand.push(c);
  const counts = { card: 0, cyber: 0, kinds: {}, hazardsDestroyed: 0, plies: 0, blocked: 0, hardBrake: 0 };
  for (let ply = 0; ply < 140 && !st.over; ply++) {
    const events = [];
    const side = st.side;
    E.startTurn(fen, st, events);
    const me = side === 'w' ? st.w : st.b;
    // optional card (white) and one cyber ability
    if (side === 'w' && me.hand.length && rnd() < 0.4) {
      const c = me.hand[Math.floor(rnd() * me.hand.length)];
      const b = B.parse(fen);
      const mine = []; for (let i = 0; i < 64; i++) if (b.cells[i] && B.colorOf(b.cells[i]) === 'w' && B.typeOf(b.cells[i]) !== 'k') mine.push(B.sq(i));
      try {
        if (c === 'pawnBoost') { const ps = mine.filter(s => b.cells[B.idx(s)] === 'P'); if (ps.length) fen = E.playCard(fen, st, { card: c, target: ps[Math.floor(rnd() * ps.length)] }, events); }
        else if (c === 'glitchSwap') { if (mine.length >= 2) fen = E.playCard(fen, st, { card: c, target: mine[Math.floor(rnd() * mine.length)], target2: mine[Math.floor(rnd() * mine.length)] }, events); }
        else fen = E.playCard(fen, st, { card: c }, events);
        counts.card++;
      } catch (e) { /* illegal choice: rejected by the authority, state must be untouched */ }
    }
    if ((side === 'w' || st.cfg.carCyber) && rnd() < 0.5) {
      const b = B.parse(fen);
      const ab = ['firewall', 'emp', 'overdrive'][Math.floor(rnd() * 3)];
      const sqs = []; for (let i = 0; i < 64; i++) if (b.cells[i] && B.typeOf(b.cells[i]) !== 'k') sqs.push(B.sq(i));
      try { E.useCyber(fen, st, { ability: ab, target: sqs[Math.floor(rnd() * sqs.length)] }, events); counts.cyber++; } catch (e) { }
    }
    invariantsMid(fen, st);
    const list = E.listActions(fen, st);
    assert.ok(list.length > 0, 'a non-terminal position always has an action: ' + fen);
    // bias toward specials so rare rules are exercised
    const specials = list.filter(a => a.kind !== 'move');
    const a = (specials.length && rnd() < 0.35) ? specials[Math.floor(rnd() * specials.length)] : list[Math.floor(rnd() * list.length)];
    counts.kinds[a.kind] = (counts.kinds[a.kind] || 0) + 1;
    const r = E.act(fen, st, { kind: a.kind, from: a.from, to: a.to, promotion: a.promotion, steps: a.steps }, events);
    fen = r.fen; counts.plies++;
    if (r.blocked) counts.blocked++;
    counts.hazardsDestroyed += events.filter(e => e.t === 'hazardDestroyed').length;
    counts.hardBrake += events.filter(e => e.t === 'hardBrake').length;
    invariants(fen, st, side);
  }
  return counts;
}
function invariantsMid(fen, st) {
  assert.ok(validFen(fen));
  const b = B.parse(fen);
  assert.ok(!B.inCheck(b, B.other(b.turn)) , 'mid-turn: opponent king not capturable');
}

test('fuzz: 120 random full games with all Road/Cyber/Hazard/Card features stay consistent with chess.js', { timeout: 900000 }, () => {
  const total = { card: 0, cyber: 0, kinds: {}, hazardsDestroyed: 0, plies: 0, blocked: 0, hardBrake: 0 };
  for (let seed = 1; seed <= 120; seed++) {
    const c = playGame(seed);
    total.card += c.card; total.cyber += c.cyber; total.hazardsDestroyed += c.hazardsDestroyed; total.plies += c.plies; total.blocked += c.blocked; total.hardBrake += c.hardBrake;
    for (const k of Object.keys(c.kinds)) total.kinds[k] = (total.kinds[k] || 0) + c.kinds[k];
  }
  console.log('FUZZ COVERAGE', JSON.stringify(total));
  assert.ok(total.plies > 3000);
  for (const k of ['move', 'laneShift', 'roadJump', 'laneCharge', 'overdrive', 'castle']) assert.ok(total.kinds[k] > 0, 'exercised ' + k);
  assert.ok(total.hazardsDestroyed > 0 && total.card > 0 && total.cyber > 0);
});
test('fuzz: deterministic - same seed produces identical games', () => {
  const run = seed => { const rnd = lcg(seed); const st = E.newState({ seed, cfg: { hazards: true, autoVehicle: true } }); let out = ''; for (let i = 0; i < 40; i++) out += E.mulberry(st).toFixed(6); return out + JSON.stringify(st.deck); };
  assert.strictEqual(run(9), run(9)); assert.notStrictEqual(run(9), run(10));
});
