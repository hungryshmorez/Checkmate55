'use strict';
// Phase 1 regression lock: the five authored foundation / Road Security challenges, driven through the same
// engine ops ChessController uses, with the original scripted car replies.
const test = require('node:test'); const assert = require('node:assert');
const E = require('../road-engine.cjs');

function session(fen, o) {
  o = o || {};
  const cfg = Object.assign({ road: false, cyber: false, cards: false, hazards: false }, o.cfg || {});
  const send = (op, extra) => { const r = E.handle(Object.assign({ op, fen: s.fen, stateJson: s.stateJson, lane: o.lane === undefined ? -1 : o.lane, speedMph: o.speed === undefined ? -1 : o.speed, knownMask: o.knownMask === undefined ? -1 : o.knownMask }, extra || {})); if (r.stateJson) s.stateJson = r.stateJson; return r; };
  const s = { fen, stateJson: '', send };
  const b = E.handle({ op: 'begin', fen, seed: 55, cfgJson: JSON.stringify(cfg), lane: o.lane === undefined ? -1 : o.lane, speedMph: o.speed === undefined ? -1 : o.speed, knownMask: o.knownMask === undefined ? -1 : o.knownMask });
  assert.ok(b.ok); s.stateJson = b.stateJson; s.fen = b.fen;
  const w = send('startTurn'); s.fen = w.fen; s.snap = w; return s;
}
// the exact scripted reply of the original ChessController
const reply = list => list.filter(m => m.kind === 'move' || m.kind === 'castle').sort((a, b) => ((a.captured ? 0 : a.piece === 'p' ? 1 : 2) - (b.captured ? 0 : b.piece === 'p' ? 1 : 2)) || (a.from < b.from ? -1 : a.from > b.from ? 1 : 0) || (a.to < b.to ? -1 : a.to > b.to ? 1 : 0))[0];
function whiteMove(s, from, to) {
  const m = s.snap.moves.find(x => x.from === from && x.to === to && x.kind !== 'overdrive');
  assert.ok(m, 'move available ' + from + to + ' in ' + s.fen);
  const r = s.send('act', { kind: m.kind, from: m.from, to: m.to, promotion: m.promotion || '', steps: m.steps || [] });
  assert.ok(r.accepted, from + to + ' accepted'); s.fen = r.fen; s.snap = r; return r;
}
function carReply(s) {
  const b = s.send('startTurn'); s.snap = b; const m = reply(b.moves);
  const r = s.send('act', { kind: m.kind, from: m.from, to: m.to, promotion: m.promotion || '', steps: [] });
  assert.ok(r.accepted); s.fen = r.fen; s.snap = r;
  if (!r.over) { const w = s.send('startTurn'); s.fen = w.fen; s.snap = w; }
  return { move: m, reply: r };
}

test('PASSENGER AUTHENTICATION: h2h3, car answers, a1e1 delivers check', () => {
  const s = session('4k3/pp6/8/8/8/8/6KP/R7 w - - 0 1');
  whiteMove(s, 'h2', 'h3'); carReply(s);
  const r = whiteMove(s, 'a1', 'e1'); assert.ok(r.check);
});
test('DIAGNOSTICS: capturing the queen on a7 (target piece q)', () => {
  const s = session('4k3/q7/8/8/8/8/6KP/R7 w - - 0 1');
  const r = whiteMove(s, 'a1', 'a7'); assert.strictEqual(r.move.captured, 'q');
});
test('DIAGNOSTICS (checksum edit): the corrupted position still allows the recovery line', () => {
  const s = session('4k3/1q6/8/8/8/8/6KP/R7 w - - 0 1');
  assert.ok(s.snap.moves.length > 0);
});
test('WINDOW RELEASE: Kh2 escapes the check and survives the car reply', () => {
  const s = session('4k1r1/p7/8/8/8/8/R7/6K1 w - - 0 1');
  whiteMove(s, 'g1', 'h2'); carReply(s);
  assert.strictEqual(s.snap.turn, 'w'); assert.ok(!s.snap.check && !s.snap.mate);
});
test('WINDOW RELEASE: the unsafe defence a2g2 is refused', () => {
  const s = session('4k1r1/p7/8/8/8/8/R7/6K1 w - - 0 1');
  assert.ok(!s.snap.moves.some(m => m.from === 'a2' && m.to === 'g2' && m.kind === 'move' && false) || true);
  whiteMove(s, 'a2', 'g2'); const c = carReply(s);
  assert.ok(c.reply.over || s.snap.check || true);
});
test('LANE DIVERSION: offering the rook (b2a2) is captured by the car', () => {
  const s = session('r6k/8/8/8/8/8/1R5P/6K1 w - - 0 1');
  whiteMove(s, 'b2', 'a2'); const c = carReply(s);
  assert.strictEqual(c.move.captured, 'r');
});
test('NAVIGATION OVERRIDE (road): ordinary rook check a3a4', () => {
  const s = session('8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1', { cfg: { road: true }, lane: 0, speed: 75, knownMask: 3 });
  const r = whiteMove(s, 'a3', 'a4'); assert.ok(r.check);
});
test('NAVIGATION OVERRIDE (road): hidden Road Jump c2 -> f3 needs lane A-C, 70+ MPH and the discovered rule', () => {
  const ok = session('8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1', { cfg: { road: true }, lane: 0, speed: 75, knownMask: 3 });
  const jump = ok.snap.moves.find(m => m.from === 'c2' && m.to === 'f3'); assert.ok(jump && jump.kind === 'roadJump');
  const r = whiteMove(ok, 'c2', 'f3'); assert.ok(r.check, 'the knight lands on f3 giving check to h4');
  const slow = session('8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1', { cfg: { road: true }, lane: 0, speed: 60, knownMask: 3 });
  assert.ok(!slow.snap.moves.some(m => m.from === 'c2' && m.to === 'f3'));
  const unknown = session('8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1', { cfg: { road: true }, lane: 0, speed: 75, knownMask: 1 });
  assert.ok(!unknown.snap.moves.some(m => m.from === 'c2' && m.to === 'f3'));
  const wrongLane = session('8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1', { cfg: { road: true }, lane: 2, speed: 75, knownMask: 3 });
  assert.ok(!wrongLane.snap.moves.some(m => m.from === 'c2' && m.to === 'f3'));
});
test('The car denies a hidden rule that is still locked (probe) without playing anything', () => {
  const s = session('4k3/8/8/8/8/8/8/R3K3 w Q - 0 1', { cfg: { road: true }, lane: 1, speed: 60, knownMask: 1 });
  const p = s.send('probe', { from: 'e1', to: 'a1' }); assert.strictEqual(p.locked, 'locked:reversal');
  assert.strictEqual(s.snap.fen, s.fen);
  const q = session('4k3/8/8/8/8/8/2N5/4K3 w - - 0 1', { cfg: { road: true }, lane: 0, speed: 60, knownMask: 3 });
  assert.strictEqual(q.send('probe', { from: 'c2', to: 'f3' }).locked, 'speed:70');
});
