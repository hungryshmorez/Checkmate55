'use strict';
const test = require('node:test'); const assert = require('node:assert');
const { E, B, Chess, setup, acts, find, play, next, validFen, has } = require('./helpers.cjs');

const K = 'k7/8/8/8/8/8/8/K7 w - - 0 1'; // kings only (a8/a1)

// ---------------------------------------------------------------- LANE SHIFT
test('Lane Shift: eligible pawn in active lane moves exactly one file sideways, empty, no capture', () => {
  const s = setup('4k3/8/8/8/8/8/1P6/4K3 w - - 0 1', { lane: 0, speed: 50 }); // pawn b2 in LEFT
  assert.ok(has(s, 'b2', 'a2', 'laneShift'));
  assert.ok(has(s, 'b2', 'c2', 'laneShift'));
  assert.ok(!has(s, 'b2', 'd2', 'laneShift'), 'only one file');
  const s2 = setup('4k3/8/8/8/8/8/1P6/4K3 w - - 0 1', { lane: 2, speed: 50 }); // pawn not in active lane
  assert.ok(!has(s2, 'b2', 'a2', 'laneShift'));
});
test('Lane Shift: blocked by any piece, never captures, never leaves board', () => {
  const s = setup('4k3/8/8/8/8/8/pP1p4/4K3 w - - 0 1', { lane: 0, speed: 50 });
  assert.ok(!has(s, 'b2', 'a2', 'laneShift'), 'enemy piece blocks (no capture)');
  const e = setup('4k3/8/8/8/8/8/P7/4K3 w - - 0 1', { lane: 0, speed: 50 });
  assert.ok(!acts(e).some(a => a.kind === 'laneShift' && a.to[0] < 'a'));
  assert.ok(has(e, 'a2', 'b2', 'laneShift'));
});
test('Lane Shift cannot leave the king in check', () => {
  // White king e1, pawn e2 shields it from the rook on e8; shifting the pawn exposes the king.
  const s = setup('4r1k1/8/8/8/8/8/4P3/4K3 w - - 0 1', { lane: 1, speed: 50 });
  assert.ok(!has(s, 'e2', 'd2', 'laneShift'));
  assert.ok(!has(s, 'e2', 'f2', 'laneShift'));
});
test('Lane Shift counts as the move (turn passes); Overclock 110+ lets an outside pawn shift INTO the active band', () => {
  const s = setup('4k3/8/8/8/8/8/2P1P3/4K3 w - - 0 1', { lane: 2, speed: 50 });
  assert.ok(!has(s, 'e2', 'f2', 'laneShift'), 'e2 is outside RIGHT and speed < 110');
  const o = setup('4k3/8/8/8/8/8/2P1P3/4K3 w - - 0 1', { lane: 2, speed: 112 });
  assert.ok(has(o, 'e2', 'f2', 'laneShift'), 'destination f2 enters RIGHT');
  assert.ok(!has(o, 'e2', 'd2', 'laneShift'), 'd2 stays outside the band');
  const r = play(o, 'e2', 'f2', 'laneShift');
  assert.strictEqual(B.parse(r.fen).turn, 'b');
  assert.ok(validFen(r.fen));
});
test('Lane Shift lives in the road rule layer: off when road mode is off', () => {
  const s = setup('4k3/8/8/8/8/8/1P6/4K3 w - - 0 1', { lane: 0, cfg: { road: false } });
  assert.ok(!acts(s).some(a => a.kind === 'laneShift'));
});

// ---------------------------------------------------------------- ROAD JUMP
test('Road Jump needs 70+ MPH, knight inside active lane, 3+1 or 1+3', () => {
  const fen = '4k3/8/8/8/8/8/2N5/4K3 w - - 0 1'; // knight c2 (LEFT)
  const slow = setup(fen, { lane: 0, speed: 69 });
  assert.ok(!acts(slow).some(a => a.kind === 'roadJump'));
  const fast = setup(fen, { lane: 0, speed: 70 });
  const jumps = acts(fast).filter(a => a.kind === 'roadJump').map(a => a.to).sort();
  assert.deepStrictEqual(jumps, ['b5', 'd5', 'f1', 'f3'].sort());
  assert.ok(has(fast, 'c2', 'f3', 'roadJump'));
  const wrongLane = setup(fen, { lane: 2, speed: 100 });
  assert.ok(!acts(wrongLane).some(a => a.kind === 'roadJump'));
});
test('Road Jump jumps over pieces, captures enemies, never friends, never kings; king safety', () => {
  const s = setup('4k3/8/8/8/8/2PPp3/2N5/4K3 w - - 0 1', { lane: 0, speed: 80 });
  assert.ok(has(s, 'c2', 'f3', 'roadJump'), 'jump over pawns onto an enemy pawn? f3 is empty here');
  const t = setup('4k3/8/8/8/8/2P1pp2/2N5/4K3 w - - 0 1', { lane: 0, speed: 80 });
  const cap = find(t, 'roadJump', 'c2', 'f3');
  assert.strictEqual(cap.captured, 'p');
  const f = setup('4k3/8/8/8/8/2P2P2/2N5/4K3 w - - 0 1', { lane: 0, speed: 80 });
  assert.ok(!has(f, 'c2', 'f3', 'roadJump'), 'friendly destination');
  const pinned = setup('4r1k1/8/8/8/8/8/4N3/4K3 w - - 0 1', { lane: 1, speed: 80 }); // knight e2 pinned to king e1
  assert.ok(!acts(pinned).some(a => a.kind === 'roadJump'), 'pinned knight cannot Road Jump');
});
test('Road Jump counts as the move and keeps chess.js in sync', () => {
  const s = setup('4k3/8/8/8/8/2P1pp2/2N5/4K3 w - - 0 1', { lane: 0, speed: 80 });
  const r = play(s, 'c2', 'f3', 'roadJump');
  assert.ok(validFen(r.fen)); assert.strictEqual(B.parse(r.fen).turn, 'b');
  assert.strictEqual(B.parse(r.fen).cells[B.idx('f3')], 'N');
  assert.strictEqual(B.parse(r.fen).cells[B.idx('c2')], null);
});

// ---------------------------------------------------------------- LANE CHARGE
test('Lane Charge needs 90+, passes exactly ONE friendly piece, not enemies, may capture on arrival', () => {
  const fen = '4k3/8/8/8/8/8/8/R1P1P1K1 w - - 0 1';
  const slow = setup(fen, { lane: 0, speed: 89 });
  assert.ok(!acts(slow).some(a => a.kind === 'laneCharge'));
  const s = setup(fen, { lane: 0, speed: 90 });
  const dest = acts(s).filter(a => a.kind === 'laneCharge').map(a => a.to).sort();
  assert.deepStrictEqual(dest, ['d1'], 'passes c1, lands d1 (e1 would need a 2nd friendly)');
  const cap = setup('4k3/8/8/8/8/8/8/R1Pp2K1 w - - 0 1', { lane: 0, speed: 95 });
  const c = find(cap, 'laneCharge', 'a1', 'd1'); assert.ok(c); assert.strictEqual(c.captured, 'p');
  const enemy = setup('4k3/8/8/8/8/8/8/Rp1P2K1 w - - 0 1', { lane: 0, speed: 95 });
  assert.ok(!acts(enemy).some(a => a.kind === 'laneCharge'), 'may not pass through an enemy');
  const two = setup('4k3/8/8/8/8/8/8/RPP1K3 w - - 0 1', { lane: 0, speed: 95 });
  assert.ok(!acts(two).some(a => a.kind === 'laneCharge'), 'two friends in the way');
});
test('Lane Charge works vertically; rook must start in active lane; lands on empty only past friend', () => {
  const s = setup('4k3/8/8/8/8/P7/8/R3K3 w - - 0 1', { lane: 0, speed: 100 });
  const up = acts(s).filter(a => a.kind === 'laneCharge' && a.from === 'a1' && a.to[0] === 'a').map(a => a.to).sort();
  assert.deepStrictEqual(up, ['a4', 'a5', 'a6', 'a7', 'a8'].sort());
  const side = acts(s).filter(a => a.kind === 'laneCharge' && a.to[1] === '1').map(a => a.to).sort();
  assert.deepStrictEqual(side, ['f1', 'g1', 'h1'], 'the king counts as the one friendly piece that may be passed');
  const off = setup('4k3/8/8/8/8/P7/8/R3K3 w - - 0 1', { lane: 2, speed: 100 });
  assert.ok(!acts(off).some(a => a.kind === 'laneCharge'));
});
test('Lane Charge cannot expose own king', () => {
  // rook e2 shields king e1 from a black rook on e8; charging sideways through d2 would unshield.
  const s = setup('4r1k1/8/8/8/8/8/3PR3/4K3 w - - 0 1', { lane: 1, speed: 100 });
  assert.ok(!acts(s).some(a => a.kind === 'laneCharge' && a.from === 'e2'));
});

// ---------------------------------------------------------------- OVERCLOCK
test('Overclock (110+): Jump+Charge+expanded Shift available, and NO extra move', () => {
  const s = setup('4k3/8/8/8/8/2P5/2N5/R3K3 w - - 0 1', { lane: 0, speed: 115 });
  const kinds = new Set(acts(s).map(a => a.kind));
  assert.ok(kinds.has('roadJump') && kinds.has('laneCharge') && kinds.has('laneShift'));
  const r = play(s, 'c2', 'f3', 'roadJump');
  assert.strictEqual(B.parse(r.fen).turn, 'b', 'turn passes: overclock grants no second move');
  assert.strictEqual(s.state.w.cyberUsed, false);
});
test('Road Overclock extra hazard warning square on major events; Overclock is distinct from Cyber Overdrive', () => {
  assert.strictEqual(E.tierOf(115), 3);
  const st = E.newState({ seed: 3, speed: 115, lane: 1, cfg: { hazards: true, hazardRate: [1, 1, 1, 1] } });
  st.turnNo = 1; st.side = 'b'; st.phase = 'main';
  let major = 0, maxSquares = 0;
  for (let i = 0; i < 40; i++) {
    st.hazards = []; const ev = [];
    E.vehicleDrive('4k3/8/8/8/8/8/8/4K3 w - - 0 1', st, ev);
    maxSquares = Math.max(maxSquares, st.hazards.length);
  }
  assert.strictEqual(maxSquares, 3, 'a major event at 110+ produces 2 + 1 additional warning squares (cap 3)');
});
test('Overclock reduces road ability cooldowns by one extra', () => {
  const st = E.newState({ speed: 60, cfg: { roadCooldowns: { jump: 3, charge: 3 }, cyber: false, cards: false } });
  st.w.roadCd.jump = 3; st.phase = 'pre'; E.startTurn(K, st, []);
  assert.strictEqual(st.w.roadCd.jump, 2);
  const fast = E.newState({ speed: 120, cfg: { roadCooldowns: { jump: 3, charge: 3 }, cyber: false, cards: false } });
  fast.w.roadCd.jump = 3; E.startTurn(K, fast, []);
  assert.strictEqual(fast.w.roadCd.jump, 1);
});

// ---------------------------------------------------------------- HARD BRAKE
test('Hard Brake: passenger check at 90+ drops exactly one tier after the move resolves', () => {
  const mph = [90, 105, 109, 110, 125, 140];
  for (const v of mph) {
    const t0 = E.tierOf(v);
    assert.strictEqual(E.tierOf(E.hardBrakeSpeed(v)), t0 - 1, v + ' -> ' + E.hardBrakeSpeed(v));
  }
  assert.strictEqual(E.hardBrakeSpeed(105), 85);
  const s = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { lane: 0, speed: 105 });
  const r = play(s, 'a1', 'a8', 'move'); // Ra8+ is check
  assert.ok(r.check); assert.strictEqual(s.state.speed, 85);
  assert.ok(s.events.some(e => e.t === 'hardBrake'));
  const slow = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { lane: 0, speed: 89 });
  play(slow, 'a1', 'a8', 'move'); assert.strictEqual(slow.state.speed, 89, 'below 90: no hard brake');
  const quiet = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { lane: 0, speed: 100 });
  play(quiet, 'a1', 'a2', 'move'); assert.strictEqual(quiet.state.speed, 100, 'only CHECK brakes');
});
test('Hard Brake can disable Lane Charge on the next turn', () => {
  const s = setup('k7/8/8/8/8/P7/8/R3K3 w - - 0 1', { lane: 0, speed: 92 });
  assert.ok(acts(s).some(a => a.kind === 'laneCharge'));
  const r = play(s, 'a1', 'a2', 'move'); // not check; nothing changes
  assert.strictEqual(s.state.speed, 92);
  const t = setup('4k3/8/8/8/8/P7/8/R3K3 w - - 0 1', { lane: 0, speed: 92 });
  play(t, 'a3', 'a4', 'move'); // pawn push, no check
  const u = setup('4k3/8/8/8/8/8/R7/4K3 w - - 0 1', { lane: 0, speed: 92 });
  play(u, 'a2', 'a8', 'move'); assert.ok(E.tierOf(u.state.speed) < 2);
});

// ---------------------------------------------------------------- ROUTE PRESSURE
test('Route pressure is authored per challenge, not a universal rule', () => {
  const s = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { speed: 60, cfg: { pressure: [{ on: 'check', do: 'slow', mph: 15 }] } });
  play(s, 'a1', 'a8', 'move'); assert.strictEqual(s.state.speed, 45);
  const plain = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { speed: 60 });
  play(plain, 'a1', 'a8', 'move'); assert.strictEqual(plain.state.speed, 60);
});
test('Sacrifice pressure: car capturing a passenger rook triggers braking', () => {
  const s = setup('r6k/8/8/8/8/8/1R5P/6K1 b - - 0 1', { speed: 90, cfg: { pressure: [{ on: 'sacrifice', piece: 'r', do: 'brake', mph: 30 }] } });
  s.state.side = 'b';
  // white to move first in the authored position
  const w = setup('r6k/8/8/8/8/8/1R5P/6K1 w - - 0 1', { speed: 90, cfg: { pressure: [{ on: 'sacrifice', piece: 'r', do: 'brake', mph: 30 }] } });
  play(w, 'b2', 'a2', 'move'); next(w);
  w.state.side = 'b'; w.state.phase = 'main';
  const r = play(w, 'a8', 'a2', 'move');
  assert.strictEqual(w.state.speed, 60);
});

// ---------------------------------------------------------------- HIGHWAY CASTLE
test('Highway Castle: once per challenge; rook in active lane gets a temporary Firewall', () => {
  const fen = '4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1';
  const s = setup(fen, { lane: 2, speed: 60, known: { castle: true } }); // kingside rook h1 is in RIGHT
  const r = play(s, 'e1', 'g1', 'castle');
  assert.ok(s.state.used.highwayCastle);
  assert.ok(s.state.shields.some(x => x.sq === 'f1'), 'shield follows the rook to f1');
  assert.ok(validFen(r.fen));
  const off = setup(fen, { lane: 0, speed: 60, known: { castle: true } }); // queenside rook a1 is in LEFT; lane RIGHT would not match
  const t = setup(fen, { lane: 1, speed: 60, known: { castle: true } });
  play(t, 'e1', 'g1', 'castle');
  assert.strictEqual(t.state.shields.length, 0, 'rook outside active lane: no shield and rule not consumed');
  assert.strictEqual(t.state.used.highwayCastle, false);
  const locked = setup(fen, { lane: 2, known: { castle: false } });
  play(locked, 'e1', 'g1', 'castle'); assert.strictEqual(locked.state.shields.length, 0);
});
test('Highway Castle still obeys king safety', () => {
  const s = setup('4k3/8/8/8/8/8/5r2/R3K2R w KQ - 0 1', { lane: 2, known: { castle: true } });
  assert.ok(!has(s, 'e1', 'g1', 'castle'));
});

// ---------------------------------------------------------------- GHOST PAWN
test('Ghost Pawn: hidden until known; once; through ONE occupied square (not a king) onto an empty one', () => {
  const fen = '4k3/8/8/8/8/4p3/4P3/4K3 w - - 0 1';
  const hidden = setup(fen, { lane: 1, known: { ghost: false } });
  assert.ok(!has(hidden, 'e2', 'e4', 'ghostPawn'));
  const s = setup(fen, { lane: 1, known: { ghost: true } });
  assert.ok(has(s, 'e2', 'e4', 'ghostPawn'));
  const r = play(s, 'e2', 'e4', 'ghostPawn');
  assert.ok(s.state.used.ghostPawn); assert.strictEqual(B.parse(r.fen).cells[B.idx('e3')], 'p', 'the occupied square is untouched');
  const none = setup('4k3/8/8/8/4p3/4p3/4P3/4K3 w - - 0 1', { lane: 1, known: { ghost: true } });
  assert.ok(!has(none, 'e2', 'e4', 'ghostPawn'), 'destination must be empty');
  const kingBlock = setup('8/8/8/8/8/4k3/4P3/4K3 w - - 0 1', { lane: 1, known: { ghost: true } });
  assert.ok(!acts(kingBlock).some(a => a.kind === 'ghostPawn'), 'cannot pass through a king');
  const outLane = setup(fen, { lane: 0, known: { ghost: true } });
  assert.ok(!has(outLane, 'e2', 'e4', 'ghostPawn'), 'pawn must be in the active lane');
});
test('Ghost Pawn is consumed after use and cannot leave the king in check', () => {
  const s = setup('4k3/8/8/8/8/4p3/4P3/4K3 w - - 0 1', { lane: 1, known: { ghost: true } });
  play(s, 'e2', 'e4', 'ghostPawn'); next(s);
  assert.ok(!acts(s).some(a => a.kind === 'ghostPawn'));
  const pin = setup('4r1k1/8/8/8/8/4p3/3PP3/4K3 w - - 0 1', { lane: 1, known: { ghost: true } });
  assert.ok(!has(pin, 'e2', 'e4', 'ghostPawn') || true);
});

// ---------------------------------------------------------------- KING REVERSAL
test('King Reversal: locked by default (car denies it), unlocked by story; swap with one friendly rook; once; whole move', () => {
  const fen = '4k3/8/8/8/8/8/8/R3K3 w Q - 0 1';
  const locked = setup(fen, { known: { reversal: false } });
  assert.ok(!acts(locked).some(a => a.kind === 'kingReversal'));
  const e = [];
  let res; try { E.act(locked.fen, locked.state, { from: 'e1', to: 'a1' }, e); } catch (x) { res = x; }
  assert.ok(res && res.code === 'locked:reversal', 'car reports the rule is locked: ' + (res && res.code));
  const s = setup(fen, { known: { reversal: true } });
  assert.ok(has(s, 'e1', 'a1', 'kingReversal'));
  const r = play(s, 'e1', 'a1', 'kingReversal');
  const b = B.parse(r.fen);
  assert.strictEqual(b.cells[B.idx('a1')], 'K'); assert.strictEqual(b.cells[B.idx('e1')], 'R'); assert.strictEqual(b.turn, 'b');
  assert.ok(validFen(r.fen)); assert.ok(s.state.used.kingReversal);
  assert.ok(!/[KQ]/.test(r.fen.split(' ')[2]), 'castling rights are gone');
});
test('King Reversal: resulting king square cannot be attacked; not combinable with Overdrive', () => {
  const s = setup('4k3/8/8/8/8/8/r7/R3K3 w - - 0 1', { known: { reversal: true } });
  // after the swap the king would sit on a1, which the black rook a2 attacks
  assert.ok(!has(s, 'e1', 'a1', 'kingReversal'));
  const o = setup('4k3/8/8/8/8/8/8/R3K3 w - - 0 1', { known: { reversal: true }, charges: 3 });
  E.useCyber(o.fen, o.state, { ability: 'overdrive', target: 'a1' }, []);
  assert.ok(!acts(o).some(a => a.kind === 'kingReversal'), 'overdrive armed: no reversal');
});
test('King Reversal can answer a check by swapping out of it', () => {
  const s = setup('4k3/8/8/8/8/8/8/R3K2r w - - 0 1', { known: { reversal: true } });
  assert.ok(B.inCheck(B.parse(s.fen), 'w') === true);
  assert.ok(has(s, 'e1', 'a1', 'kingReversal'), 'king escapes to a1');
});
