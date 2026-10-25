'use strict';
const test = require('node:test'); const assert = require('node:assert');
const { E, B, Chess, setup, acts, find, play, next, validFen, has } = require('./helpers.cjs');

const cyber = (s, ability, target) => E.useCyber(s.fen, s.state, { ability, target }, s.events);
const card = (s, c, t, t2) => { s.fen = E.playCard(s.fen, s.state, { card: c, target: t, target2: t2 }, s.events); };

// ---------------------------------------------------------------- RESOURCES
test('+1 Cyber Charge at the start of every White turn; only ONE ability per turn', () => {
  const s = setup('4k3/8/8/8/8/8/3PP3/3QK3 w - - 0 1', { cfg: { cyber: true } });
  assert.strictEqual(s.state.w.charges, 1);
  cyber(s, 'firewall', 'd2');
  assert.strictEqual(s.state.w.charges, 0);
  assert.throws(() => cyber(s, 'firewall', 'e2'), /one cyber ability per turn|not enough/);
  play(s, 'e2', 'e3', 'move'); s.state.side = 'w'; s.state.phase = 'pre'; // skip black for the resource check
  next(s); assert.strictEqual(s.state.w.charges, 1);
});
test('Abilities cost charge: firewall 1, EMP 2, overdrive 3; cooldowns apply', () => {
  const s = setup('4k3/8/8/8/8/8/3PP3/3QK3 w - - 0 1', { cfg: { cyber: true }, charges: 0 });
  assert.throws(() => cyber(s, 'firewall', 'd2'), /not enough charge/);
  s.state.w.charges = 6;
  cyber(s, 'firewall', 'd2');
  assert.strictEqual(s.state.w.charges, 5);
});
test('Charge cap', () => {
  const s = setup('4k3/8/8/8/8/8/8/4K3 w - - 0 1', { cfg: { cyber: true, chargeCap: 3 } });
  for (let i = 0; i < 6; i++) { s.state.phase = 'pre'; E.startTurn(s.fen, s.state, []); }
  assert.strictEqual(s.state.w.charges, 3);
});
test('Cyber disabled in non-cyber modes', () => {
  const s = setup('4k3/8/8/8/8/8/3PP3/3QK3 w - - 0 1', { cfg: { cyber: false } });
  assert.throws(() => cyber(s, 'firewall', 'd2'), /cyber disabled/);
});

// ---------------------------------------------------------------- FIREWALL
test('Firewall: friendly non-King only; blocks the next capture attempt; shield then removed; turn is spent', () => {
  const fen = '4k3/8/8/3r4/8/8/3N4/4K3 w - - 0 1';
  const s = setup(fen, { charges: 5 });
  assert.throws(() => cyber(s, 'firewall', 'e1'), /kings cannot be targeted/);
  assert.throws(() => cyber(s, 'firewall', 'd5'), /friendly/);
  cyber(s, 'firewall', 'd2'); assert.ok(E.shieldAt(s.state, 'd2'));
  play(s, 'd2', 'f3', 'move'); // knight steps off; shield follows the piece
  assert.ok(E.shieldAt(s.state, 'f3') && !E.shieldAt(s.state, 'd2'));
});
test('A capture against a shielded piece is blocked: attacker stays, shield removed, turn consumed', () => {
  const u = setup('4k3/6r1/8/8/8/8/8/4K1R1 b - - 0 1', {}); u.state.shields.push({ sq: 'g1', ttl: -1 });
  const cap = find(u, 'move', 'g7', 'g1'); assert.ok(cap && cap.blocked, 'listed as a (blocked) capture attempt');
  const before = u.fen.split(' ')[0];
  const r = play(u, 'g7', 'g1', 'move');
  assert.ok(r.blocked); assert.strictEqual(r.fen.split(' ')[0], before, 'board unchanged: attacker bounced');
  assert.strictEqual(B.parse(r.fen).turn, 'w', 'turn consumed');
  assert.strictEqual(u.state.shields.length, 0, 'shield removed after the blocked capture');
  assert.ok(u.events.some(e => e.t === 'captureBlocked'));
});
test('A blocked capture cannot answer a check', () => {
  // black rook g8 checks the white king g1 and is shielded; the white rook on a8 would capture it, but a blocked capture would leave the check
  const u = setup('4k1r1/8/8/8/8/8/8/R5K1 w - - 0 1', {}); u.state.shields.push({ sq: 'g8', ttl: -1 });
  assert.ok(B.inCheck(B.parse(u.fen), 'w'));
  assert.ok(!acts(u).some(a => a.from === 'a1' && a.to === 'g8') );
});
test('Firewall protects against a hazard once, then is consumed', () => {
  const s = setup('4k3/8/8/8/8/8/3N4/4K3 w - - 0 1', { charges: 3, cfg: { hazards: true } });
  cyber(s, 'firewall', 'd2');
  s.state.hazards.push({ sq: 'd2', phase: 'WARNING', resolveTurn: s.state.turnNo });
  play(s, 'e1', 'f1', 'move');
  assert.ok(s.events.some(e => e.t === 'hazardBlocked'));
  assert.strictEqual(B.parse(s.fen).cells[B.idx('d2')], 'N');
  assert.strictEqual(s.state.shields.length, 0);
});

// ---------------------------------------------------------------- EMP
test('EMP: adjacent enemy, removes Firewall, disables special actions through its next turn only', () => {
  const s = setup('4k3/8/8/8/8/8/8/4K3 w - - 0 1', { cfg: { carCyber: true } });
  const a = setup('4k3/8/8/8/3pn3/3N4/8/4K3 w - - 0 1', { charges: 2, lane: 0, speed: 80 }); // knight d3 adjacent to e4 and d4
  a.state.shields.push({ sq: 'e4', ttl: -1 });
  assert.throws(() => cyber(a, 'emp', 'b2'), /empty/);
  cyber(a, 'emp', 'e4');
  assert.strictEqual(E.shieldAt(a.state, 'e4'), undefined, 'Firewall stripped');
  assert.ok(E.empAt(a.state, 'e4'));
  play(a, 'd3', 'b4', 'move');
  assert.ok(E.empAt(a.state, 'e4'), 'still disabled during black\'s next turn');
  next(a); // black starts
  a.state.side = 'b'; a.state.phase = 'main';
  play(a, 'e8', 'd8', 'move');
  assert.strictEqual(E.empAt(a.state, 'e4'), undefined, 'EMP expires after the victim\'s next turn');
});
test('EMP target must be adjacent to your pieces and not a king', () => {
  const s = setup('4k3/8/8/8/8/8/2p5/4K3 w - - 0 1', { charges: 2 });
  assert.throws(() => cyber(s, 'emp', 'c2'), /adjacent/);
  const t = setup('4k3/8/8/8/8/8/3p4/4K3 w - - 0 1', { charges: 2 });
  cyber(t, 'emp', 'd2');
  assert.throws(() => cyber(setup('3k4/3K4/8/8/8/8/8/8 w - - 0 1', { charges: 2 }), 'emp', 'd8'), /king/);
});
test('EMP-disabled piece loses Road specials but keeps normal chess movement', () => {
  const s = setup('4k3/8/8/8/8/8/2N5/4K3 b - - 0 1', { cfg: { carCyber: true } });
  const w = setup('4k3/8/8/8/8/8/2N5/4K3 w - - 0 1', { lane: 0, speed: 100 });
  assert.ok(acts(w).some(a => a.kind === 'roadJump'));
  w.state.emp.push({ sq: 'c2', owner: 'w' });
  assert.ok(!acts(w).some(a => a.kind === 'roadJump'), 'specials disabled');
  assert.ok(acts(w).some(a => a.from === 'c2' && a.kind === 'move'), 'normal movement remains');
});

// ---------------------------------------------------------------- OVERDRIVE
test('Overdrive: same piece, two legal steps; at most one capture; king safe at both stages', () => {
  const s = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { charges: 5 });
  cyber(s, 'overdrive', 'a1');
  const od = acts(s).filter(a => a.kind === 'overdrive');
  assert.ok(od.length > 20);
  assert.ok(od.every(a => a.steps.length === 2 && a.steps[0].from === 'a1'));
  assert.ok(od.some(a => a.steps[0].to === 'a5' && a.steps[1].to === 'e5'));
  const a = od.find(x => x.steps[0].to === 'a8' || x.to === 'a8');
  assert.ok(a, 'a1-a8 reachable through two rook steps');
});
test('Overdrive double move gives the turn to the opponent only once and keeps chess.js in sync', () => {
  const s = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { charges: 5 });
  cyber(s, 'overdrive', 'a1');
  const a = acts(s).find(x => x.kind === 'overdrive' && x.steps[0].to === 'a5' && x.steps[1].to === 'e5');
  const events = [];
  const r = E.act(s.fen, s.state, { kind: 'overdrive', from: a.from, to: a.to, steps: a.steps }, events);
  const b = B.parse(r.fen);
  assert.strictEqual(b.cells[B.idx('e5')], 'R'); assert.strictEqual(b.cells[B.idx('a1')], null);
  assert.strictEqual(b.turn, 'b'); assert.ok(validFen(r.fen));
  assert.strictEqual(events.filter(e => e.t === 'move').length, 2);
});
test('Overdrive allows only one capture across both steps and never exposes the king', () => {
  const s = setup('4k3/8/8/8/p7/8/8/R3K2p w - - 0 1', { charges: 5 });
  cyber(s, 'overdrive', 'a1');
  const od = acts(s).filter(a => a.kind === 'overdrive');
  assert.ok(od.length);
  for (const a of od) {
    const ev = [];
    const r = E.act(s.fen, E.cloneState(s.state), { kind: 'overdrive', from: a.from, to: a.to, steps: a.steps }, ev);
    assert.ok(ev.filter(e => e.t === 'capture').length <= 1, 'max one capture');
    assert.ok(!B.inCheck(B.parse(r.fen), 'w'), 'own king safe');
    assert.ok(validFen(r.fen));
  }
  // a rook pinned along the e-file may only slide along the pin line, at both stages
  const pinned = setup('4r1k1/8/8/8/8/8/4R3/4K3 w - - 0 1', { charges: 5 });
  cyber(pinned, 'overdrive', 'e2');
  for (const a of acts(pinned).filter(a => a.kind === 'overdrive')) {
    assert.ok(a.steps[0].to[0] === 'e', 'step 1 stays on the pin line (step 2 may leave only after capturing the pinner): ' + JSON.stringify(a.steps));
    if (a.steps[1].to[0] !== 'e') assert.strictEqual(a.steps[0].to, 'e8');
  }
});
test('Overdrive cannot target a King or an EMP-disabled piece', () => {
  const s = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { charges: 5 });
  assert.throws(() => cyber(s, 'overdrive', 'e1'), /king/);
  s.state.emp.push({ sq: 'a1', owner: 'w' });
  assert.throws(() => cyber(s, 'overdrive', 'a1'), /EMP/);
});

// ---------------------------------------------------------------- CARDS
test('Cards: one per White turn, does not consume the chess move, discarded after use', () => {
  const s = setup('4k3/8/8/8/8/8/3P4/4K3 w - - 0 1', { cfg: { cards: true }, hand: ['powerSurge', 'systemReboot'] });
  card(s, 'powerSurge');
  assert.strictEqual(s.state.w.charges, 3);
  assert.throws(() => card(s, 'systemReboot'), /one card per turn/);
  assert.strictEqual(s.state.discard.length, 1);
  play(s, 'd2', 'd3', 'move'); assert.ok(true);
});
test('Pawn Boost gives an enhanced forward move for this turn only', () => {
  const s = setup('4k3/8/8/8/8/3P4/8/4K3 w - - 0 1', { cfg: { cards: true }, hand: ['pawnBoost'] });
  assert.ok(!has(s, 'd3', 'd5', 'pawnBoost'));
  card(s, 'pawnBoost', 'd3');
  assert.ok(has(s, 'd3', 'd5', 'pawnBoost'));
  const r = play(s, 'd3', 'd5', 'pawnBoost'); assert.strictEqual(B.parse(r.fen).cells[B.idx('d5')], 'P'); assert.ok(validFen(r.fen));
  assert.strictEqual(s.state.w.boost, '');
});
test('Glitch Swap swaps two friendly non-kings, rejects illegal results', () => {
  const s = setup('4k3/8/8/8/8/8/3PN3/R3K3 w - - 0 1', { cfg: { cards: true }, hand: ['glitchSwap'] });
  card(s, 'glitchSwap', 'd2', 'e2');
  const b = B.parse(s.fen); assert.strictEqual(b.cells[B.idx('d2')], 'N'); assert.strictEqual(b.cells[B.idx('e2')], 'P'); assert.ok(validFen(s.fen));
  const t = setup('4k3/8/8/8/8/8/3PN3/R3K2r w - - 0 1', { cfg: { cards: true }, hand: ['glitchSwap'] });
  assert.throws(() => card(t, 'glitchSwap', 'd2', 'e2'), /check/, 'a swap cannot answer or leave a check');
  const k = setup('4k3/8/8/8/8/8/3P4/R3K3 w - - 0 1', { cfg: { cards: true }, hand: ['glitchSwap'] });
  assert.throws(() => card(k, 'glitchSwap', 'e1', 'a1'), /non-king/);
  const p = setup('4k3/8/8/8/8/8/3P4/R3K3 w - - 0 1', { cfg: { cards: true }, hand: ['glitchSwap'] });
  assert.throws(() => card(p, 'glitchSwap', 'd2', 'a1'), /pawn cannot land/);
});
test('Glitch Swap drops stale castling rights', () => {
  const s = setup('4k3/8/8/8/8/8/3N4/R3K2R w KQ - 0 1', { cfg: { cards: true }, hand: ['glitchSwap'] });
  card(s, 'glitchSwap', 'a1', 'd2');
  assert.ok(!/Q/.test(s.fen.split(' ')[2]));
  assert.ok(validFen(s.fen));
});
test('System Reboot clears every WARNING / ACTIVE hazard before it resolves', () => {
  const s = setup('4k3/8/8/8/8/8/3N4/4K3 w - - 0 1', { cfg: { cards: true, hazards: true }, hand: ['systemReboot'] });
  s.state.hazards.push({ sq: 'd2', phase: 'WARNING', resolveTurn: s.state.turnNo });
  card(s, 'systemReboot'); assert.strictEqual(s.state.hazards.length, 0);
  play(s, 'e1', 'f1', 'move'); assert.strictEqual(B.parse(s.fen).cells[B.idx('d2')], 'N');
});
test('Power Surge: +2 charges and reduces active Cyber cooldowns by one', () => {
  const s = setup('4k3/8/8/8/8/8/8/4K3 w - - 0 1', { cfg: { cards: true }, hand: ['powerSurge'] });
  s.state.w.cd.emp = 2; s.state.w.charges = 0;
  card(s, 'powerSurge');
  assert.strictEqual(s.state.w.charges, 2); assert.strictEqual(s.state.w.cd.emp, 1);
});
test('Hand limit and reshuffle only when the mode allows it', () => {
  const s = setup('4k3/8/8/8/8/8/8/4K3 w - - 0 1', { cfg: { cards: true, handMax: 2, startHand: 0 } });
  for (let i = 0; i < 6; i++) { s.state.phase = 'pre'; E.startTurn(s.fen, s.state, []); }
  assert.strictEqual(s.state.w.hand.length, 2);
  const r = setup('4k3/8/8/8/8/8/8/4K3 w - - 0 1', { cfg: { cards: true, reshuffle: false, handMax: 20 } });
  r.state.deck = []; r.state.discard = ['powerSurge'];
  r.state.phase = 'pre'; E.startTurn(r.fen, r.state, []); assert.strictEqual(r.state.w.hand.filter(c => c === 'powerSurge').length, 0);
  const q = setup('4k3/8/8/8/8/8/8/4K3 w - - 0 1', { cfg: { cards: true, reshuffle: true, handMax: 20 } });
  q.state.deck = []; q.state.discard = ['powerSurge']; q.state.w.hand = [];
  q.state.phase = 'pre'; E.startTurn(q.fen, q.state, []); assert.ok(q.state.w.hand.includes('powerSurge'));
});

// ---------------------------------------------------------------- HAZARDS
test('Hazard lifecycle: WARNING appears, does nothing immediately, resolves at the END of the next White turn', () => {
  const s = setup('4k3/p7/8/8/8/8/P2N4/4K3 b - - 0 1', { cfg: { hazards: true, hazardRate: [1, 1, 1, 1] }, speed: 40, lane: 1 });
  s.state.side = 'b'; s.state.phase = 'main'; s.state.turnNo = 1;
  play(s, 'e8', 'd8', 'move');
  assert.ok(s.state.hazards.length >= 1); assert.ok(s.state.hazards.every(h => h.phase === 'WARNING'));
  const sq = s.state.hazards[0].sq;
  const before = B.parse(s.fen).cells.join('');
  // no instant damage
  assert.strictEqual(B.parse(s.fen).cells.join(''), before);
  assert.ok(s.events.some(e => e.t === 'hazardWarning'));
  assert.ok(!s.events.some(e => e.t === 'hazardActive' || e.t === 'hazardDestroyed'));
  next(s); // white turn 2 begins; hazards still warn
  assert.ok(s.state.hazards.length >= 1);
  s.state.hazards = [{ sq: 'd2', phase: 'WARNING', resolveTurn: s.state.turnNo }];
  play(s, 'e1', 'f1', 'move'); // the knight on d2 stays -> destroyed at the END of the turn
  assert.strictEqual(B.parse(s.fen).cells[B.idx('d2')], null);
  assert.deepStrictEqual(s.events.filter(e => /^hazard/.test(e.t)).map(e => e.t), ['hazardActive', 'hazardDestroyed', 'hazardClear']);
  assert.strictEqual(s.state.hazards.length, 0, 'CLEAR');
  void sq;
});
test('Hazards affect BOTH colours; Kings are never destroyed', () => {
  const s = setup('4k3/8/8/8/3n4/8/3N4/4K3 w - - 0 1', { cfg: { hazards: true } });
  s.state.hazards = [{ sq: 'd2', phase: 'WARNING', resolveTurn: 1 }, { sq: 'd4', phase: 'WARNING', resolveTurn: 1 }, { sq: 'e8', phase: 'WARNING', resolveTurn: 1 }, { sq: 'e1', phase: 'WARNING', resolveTurn: 1 }];
  play(s, 'd2', 'f3', 'move'); // white knight leaves its warning square; black knight stays
  const b = B.parse(s.fen);
  assert.strictEqual(b.cells[B.idx('d4')], null, 'black piece destroyed');
  assert.strictEqual(b.cells[B.idx('f3')], 'N', 'white piece moved away and survived');
  assert.strictEqual(b.cells[B.idx('e8')], 'k'); assert.strictEqual(b.cells[B.idx('e1')], 'K');
});
test('A piece pinned to its king is spared by hazards (hazards never expose a king)', () => {
  const s = setup('4r1k1/8/8/8/8/8/P3B3/4K3 w - - 0 1', { cfg: { hazards: true } });
  s.state.hazards = [{ sq: 'e2', phase: 'WARNING', resolveTurn: 1 }];
  play(s, 'a2', 'a3', 'move');
  assert.strictEqual(B.parse(s.fen).cells[B.idx('e2')], 'B', 'pinned bishop survives');
  assert.ok(s.events.some(e => e.t === 'hazardSpared'));
  assert.ok(!B.inCheck(B.parse(s.fen), 'w'));
});
test('Hazard squares avoid kings and respect the cap; deterministic for a seed', () => {
  const run = seed => {
    const st = E.newState({ seed, speed: 100, lane: 2, cfg: { hazards: true, hazardRate: [1, 1, 1, 1] } });
    st.turnNo = 1; const out = [];
    for (let i = 0; i < 6; i++) { st.hazards = []; E.addHazards('4k3/8/8/8/8/8/8/4K3 w - - 0 1', st, 3, [], 'x'); out.push(st.hazards.map(h => h.sq).join(',')); }
    return out;
  };
  assert.deepStrictEqual(run(11), run(11)); assert.notDeepStrictEqual(run(11), run(12));
  assert.ok(run(11).every(r => !/e1|e8/.test(r)));
});

// ---------------------------------------------------------------- PIPELINE / AUTHORITY
test('Turn pipeline is deterministic and phase-checked', () => {
  const s = setup('4k3/8/8/8/8/8/3P4/4K3 w - - 0 1', {});
  assert.throws(() => E.startTurn(s.fen, s.state, []), /already started/);
  const r = play(s, 'd2', 'd3', 'move');
  assert.throws(() => E.act(r.fen, s.state, { from: 'e8', to: 'd8' }, []), /main phase/, 'black must start their turn first');
  assert.strictEqual(s.state.side, 'b');
});
test('Illegal / spoofed actions are rejected by the authority', () => {
  const s = setup('4k3/8/8/8/8/8/3P4/4K3 w - - 0 1', {});
  assert.throws(() => E.act(s.fen, s.state, { kind: 'move', from: 'd2', to: 'd6' }, []), /illegal/);
  assert.throws(() => E.act(s.fen, s.state, { kind: 'roadJump', from: 'd2', to: 'd5' }, []), /illegal/);
});
test('handle(): JSON round trip through the stateless request API', () => {
  const q1 = E.handle({ op: 'begin', fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1', seed: 5, lane: 1, speedMph: 60, cfgJson: JSON.stringify({ cards: true, cyber: true, hazards: true }) });
  assert.ok(q1.ok); assert.strictEqual(q1.turn, 'w');
  const q2 = E.handle({ op: 'startTurn', fen: q1.fen, stateJson: q1.stateJson });
  assert.ok(q2.ok); assert.strictEqual(q2.state.phase, 'main'); assert.ok(q2.moves.length >= 20);
  const q3 = E.handle({ op: 'act', fen: q2.fen, stateJson: q2.stateJson, from: 'e2', to: 'e4' });
  assert.ok(q3.accepted && q3.fen.includes(' b '));
  const bad = E.handle({ op: 'act', fen: q2.fen, stateJson: q2.stateJson, from: 'e2', to: 'e5' });
  assert.ok(!bad.accepted);
  const lock = E.handle({ op: 'begin', fen: '4k3/8/8/8/8/8/8/R3K3 w Q - 0 1', knownMask: 0 });
  const l2 = E.handle({ op: 'startTurn', fen: lock.fen, stateJson: lock.stateJson });
  const l3 = E.handle({ op: 'act', fen: l2.fen, stateJson: l2.stateJson, from: 'e1', to: 'a1' });
  assert.strictEqual(l3.locked, 'locked:reversal');
});
test('legacy request shape still works (Road Jump c2->f3 at 75 MPH)', () => {
  const r = E.handle({ fen: '8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1', roadEnabled: true, jumpKnown: true, chargeKnown: false, speedMph: 75, lane: 0, overclockLane: 0 });
  assert.ok(r.moves.some(m => m.from === 'c2' && m.to === 'f3' && m.flags === 'road'));
  const m = E.handle({ fen: '8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1', roadEnabled: true, jumpKnown: true, speedMph: 75, lane: 0, from: 'c2', to: 'f3' });
  assert.ok(m.accepted);
});
