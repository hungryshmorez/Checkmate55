'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { Hub, Authority, CoopTurn, CAR_ACTIONS, flagAllowed } = require('../room.cjs');
const E = require('../../Chess/road-engine.cjs');
const vectors = require('./vectors.json');

function rig(opts) {
  let t = 1000; const clock = () => t; let n = 0;
  const hub = new Hub(Object.assign({ clock, seed: () => 4242, codes: () => 'RM' + String(n++).padStart(3, '0'), aiBudgetMs: 60 }, opts || {}));
  const inbox = {};
  const pump = (out) => { out.forEach(o => { if (o.to) (inbox[o.to] = inbox[o.to] || []).push(o.msg); }); return out; };
  const api = {
    hub, inbox, advance(s) { t += s; }, now: () => t,
    connect(id, name) { pump(hub.connect(id)); if (name) api.send(id, { t: 'hello', name }); },
    send(id, m) { return pump(hub.message(id, JSON.stringify(m))); },
    last(id, type) { const l = (inbox[id] || []).filter(m => !type || m.t === type); return l[l.length - 1]; },
    all(id, type) { return (inbox[id] || []).filter(m => !type || m.t === type); },
    clear() { Object.keys(inbox).forEach(k => inbox[k] = []); },
    disconnect(id) { return pump(hub.disconnect(id)); },
    sweep() { return pump(hub.sweep()); },
  };
  return api;
}
function started(mode, extra, opts) {
  const r = rig(opts); r.connect('a', 'Ana'); r.connect('b', 'Bo');
  r.send('a', Object.assign({ t: 'create', mode }, extra || {})); r.send('b', { t: 'join', code: 'RM000' }); r.send('a', { t: 'start' }); r.clear();
  return r;
}
// A client replica of the authoritative chess: replays exactly the ops the server broadcast.
function replica() { return { fen: '', stateJson: '' }; }
function apply(c, msg) {
  for (const op of msg.ops) {
    const q = op.op === 'begin' ? Object.assign({}, op) : Object.assign({}, op, { fen: c.fen, stateJson: c.stateJson });
    const r = E.handle(q); assert.ok(r.ok, 'replica op ' + op.op + ' failed: ' + r.error);
    c.fen = r.fen; c.stateJson = r.stateJson || c.stateJson;
  }
  assert.equal(c.fen, msg.fen, 'fen diverged after seq ' + msg.seq);
}
const CFG = { road: true, cyber: true, cards: true, hazards: true, startHand: 1, startCharges: 2, carCyber: true };
const OPEN = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1';
const veh = { lane: 1, speedMph: 95, knownMask: -1 };
function begin(r, who, fen, cfg) { return r.send(who, { t: 'chessBegin', challengeId: 'final', req: Object.assign({ fen: fen || OPEN, seed: 9, cfgJson: JSON.stringify(cfg || CFG) }, veh) }); }
function firstMove(r, who) { return null; }
function whiteMoves(srv, hub) { const ch = hub.rooms.get('RM000').chess; return E.handle(Object.assign({ op: 'query', fen: ch.fen, stateJson: ch.stateJson }, ch.veh)).moves.filter(m => m.kind === 'move'); }

test('rooms: create, join, roster, errors', () => {
  const r = rig(); r.connect('a'); r.connect('b'); r.connect('c');
  r.send('a', { t: 'create', mode: 'coop', coop: 'shared' });
  assert.equal(r.last('a', 'room').slot, 'A'); assert.equal(r.last('a', 'room').code, 'RM000');
  r.send('b', { t: 'join', code: 'rm000' }); assert.equal(r.last('b', 'room').slot, 'B');
  assert.equal(r.last('a', 'roster').roster.length, 2);
  r.send('c', { t: 'join', code: 'RM000' }); assert.equal(r.last('c', 'deny').reason, 'room-full');
  r.send('c', { t: 'join', code: 'NOPE' }); assert.equal(r.last('c', 'deny').reason, 'no-room');
  r.send('c', { t: 'create', mode: 'chess' }); assert.equal(r.last('c', 'deny').reason, 'bad-mode');
  r.send('a', { t: 'create', mode: 'coop' }); assert.equal(r.last('a', 'deny').reason, 'already-in-room');
});
test('rooms: duel slots and host choice', () => {
  const r = rig(); r.connect('a'); r.connect('b');
  r.send('a', { t: 'create', mode: 'duel', role: 'C' }); assert.equal(r.last('a', 'room').slot, 'C');
  r.send('b', { t: 'join', code: 'RM000' }); assert.equal(r.last('b', 'room').slot, 'P');
});
test('start: host only, needs two, then no more joins', () => {
  const r = rig(); r.connect('a'); r.connect('b'); r.connect('c');
  r.send('a', { t: 'create', mode: 'coop' }); r.send('a', { t: 'start' }); assert.equal(r.last('a', 'deny').reason, 'need-two');
  r.send('b', { t: 'join', code: 'RM000' }); r.send('b', { t: 'start' }); assert.equal(r.last('b', 'deny').reason, 'host-only');
  r.send('a', { t: 'start' }); assert.equal(r.last('a', 'start').seed, 4242); assert.equal(r.last('b', 'start').slot, 'B'); assert.equal(r.last('b', 'start').host, false);
  r.send('c', { t: 'join', code: 'RM000' }); assert.equal(r.last('c', 'deny').reason, 'already-started');
});
test('protocol hardening: bad json, oversize, unknown, rate limit, no room', () => {
  const r = rig(); r.connect('a');
  r.hub.message('a', '{nope'); assert.equal(r.last('a') && r.last('a').t, 'welcome');
  const o1 = r.hub.message('a', '{nope'); assert.equal(o1[0].msg.reason, 'bad-json');
  assert.equal(r.hub.message('a', 'x'.repeat(30000))[0].msg.reason, 'too-large');
  assert.equal(r.hub.message('a', JSON.stringify({ t: 'zzz' }))[0].msg.reason, 'not-in-room');
  assert.equal(r.hub.message('a', JSON.stringify([1]))[0].msg.reason, 'bad-message');
  let limited = false; for (let i = 0; i < 200; i++) { const o = r.hub.message('a', JSON.stringify({ t: 'ping' })); if (o[0] && o[0].msg.reason === 'rate-limited') { limited = true; break; } }
  assert.ok(limited, 'burst must be rate limited'); r.advance(5); assert.equal(r.hub.message('a', JSON.stringify({ t: 'ping' }))[0].msg.t, 'pong');
  assert.equal(r.hub.message('ghost', '{}').length, 0);
});
test('flags: allowed families relay once and never echo; forbidden refused; car cannot set flags', () => {
  const r = started('coop');
  r.send('a', { t: 'flag', flag: 'puzzle.seat' }); assert.equal(r.last('b', 'flag').flag, 'puzzle.seat'); assert.equal(r.last('a', 'flag'), undefined);
  r.clear(); r.send('b', { t: 'flag', flag: 'puzzle.seat' }); assert.equal(r.last('a', 'flag'), undefined, 'duplicate flags are not relayed');
  r.send('a', { t: 'flag', flag: 'ending.checkmate' }); assert.equal(r.last('a', 'deny').reason, 'flag-refused');
  r.send('a', { t: 'flag', flag: 'x; drop' }); assert.equal(r.last('a', 'deny').reason, 'flag-refused');
  assert.ok(flagAllowed('glovebox-authenticated')); assert.ok(!flagAllowed('ending.any')); assert.ok(!flagAllowed(''));
  const d = started('duel'); d.send('b', { t: 'flag', flag: 'rule.jump' }); assert.equal(d.last('b', 'deny') && d.last('b', 'deny').reason !== undefined, true);
});
test('flags: the duel car is refused', () => {
  const r = rig(); r.connect('a'); r.connect('b'); r.send('a', { t: 'create', mode: 'duel', role: 'C' }); r.send('b', { t: 'join', code: 'RM000' }); r.send('a', { t: 'start' }); r.clear();
  r.send('a', { t: 'flag', flag: 'rule.jump' }); assert.equal(r.last('a', 'deny').reason, 'car-cannot-set-flags');
  r.send('b', { t: 'flag', flag: 'rule.jump' }); assert.equal(r.last('a', 'flag').flag, 'rule.jump');
});
test('threat relay: host only, valid kinds only, sanitised', () => {
  const r = started('coop');
  r.send('b', { t: 'threat', threat: { kind: 'TintUp' } }); assert.equal(r.last('b', 'deny').reason, 'host-only');
  r.send('a', { t: 'threat', threat: { kind: 'EraseDisk' } }); assert.equal(r.last('a', 'deny').reason, 'bad-threat');
  r.send('a', { t: 'threat', threat: { kind: 'TintUp', strength: 99, line: 'x'.repeat(500) } });
  const m = r.last('b', 'threat'); assert.equal(m.threat.strength, 3); assert.equal(m.threat.line.length, 140);
});
test('shared vectors: SystemAuthority numbers match the C# Core', () => {
  assert.equal(vectors.authority.start, 60);
  for (const [name, [cost, cd]] of Object.entries(vectors.authority.actions)) { assert.equal(CAR_ACTIONS[name].cost, cost, name + ' cost'); assert.equal(CAR_ACTIONS[name].cd, cd, name + ' cooldown'); }
  assert.equal(Object.keys(CAR_ACTIONS).length, Object.keys(vectors.authority.actions).length);
  const a = new Authority(0); a.try('RouteManipulate', 0); assert.ok(Math.abs(a.value - 12) < 1e-9); a.update(10); assert.ok(Math.abs(a.value - 44) < 1e-9); a.update(1000); assert.equal(a.value, 100);
});
test('shared vectors: CoopTurn operator table matches the C# Core', () => {
  for (const v of vectors.coop) {
    const c = new CoopTurn(v.mode, v.players); for (let i = 0; i < v.turn; i++) c.nextTurn();
    assert.equal(c.chessOperator, v.operator, JSON.stringify(v)); assert.equal(c.supportOperator, v.supportOperator, JSON.stringify(v));
    for (const [p, ok] of Object.entries(v.confirm)) assert.equal(c.canConfirmChess(+p), ok, JSON.stringify(v) + ' p' + p);
  }
});
test('coop turn rules: one White action per team turn; support is limited', () => {
  const c = new CoopTurn('assist', 2);
  assert.ok(c.tryConfirmChess(0)); assert.ok(!c.tryConfirmChess(0)); assert.ok(!c.canConfirmChess(1));
  assert.ok(!c.canSupport(0, 'card')); assert.ok(c.trySupport(1, 'card')); assert.ok(!c.trySupport(1, 'cyber'), 'assist: one support action per turn');
  c.nextTurn(); assert.ok(c.trySupport(1, 'cyber')); assert.ok(c.tryConfirmChess(0));
  const s = new CoopTurn('shared', 2); assert.ok(s.trySupport(0, 'card')); assert.ok(!s.trySupport(1, 'card')); assert.ok(s.trySupport(1, 'cyber'));
});

test('coop (assist): server lets only A move, B supports, replicas stay identical', () => {
  const r = started('coop', { coop: 'assist' }); const ca = replica(), cb = replica();
  const out = begin(r, 'a'); apply(ca, r.last('a', 'ops')); apply(cb, r.last('b', 'ops'));
  r.clear();
  r.send('b', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); assert.equal(r.last('b', 'deny').reason, 'not-your-move');
  r.send('a', { t: 'chess', req: Object.assign({ op: 'cyber', ability: 'firewall', target: 'e2' }, veh) }); assert.equal(r.last('a', 'deny').reason, 'support-unavailable');
  r.clear();
  r.send('b', { t: 'chess', req: Object.assign({ op: 'cyber', ability: 'firewall', target: 'e2' }, veh) });
  const sup = r.last('a', 'ops'); assert.ok(sup && sup.kind === 'support'); apply(ca, sup); apply(cb, r.last('b', 'ops'));
  r.send('b', { t: 'chess', req: Object.assign({ op: 'card', card: 'pawnBoost', target: 'a2' }, veh) }); assert.equal(r.last('b', 'deny').reason, 'support-unavailable', 'assist: only one support action per turn');
  r.clear();
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) });
  const m = r.last('a', 'ops'); assert.equal(m.who, 'w'); assert.ok(m.ops.length >= 4, 'white act + black startTurn/act + white startTurn'); assert.equal(m.turn, 'w');
  apply(ca, m); apply(cb, r.last('b', 'ops'));
  assert.equal(ca.stateJson, cb.stateJson, 'both replicas hold the same state');
  assert.equal(ca.stateJson, r.hub.rooms.get('RM000').chess.stateJson, 'and match the server');
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); assert.equal(r.last('a', 'deny').reason, 'illegal');
});
test('coop (alternating): A then B then A', () => {
  const r = started('coop', { coop: 'alternating' }); begin(r, 'a'); r.clear();
  r.send('b', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); assert.equal(r.last('b', 'deny').reason, 'not-your-move');
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); assert.ok(r.last('a', 'ops'));
  r.clear(); const mv = whiteMoves(null, r.hub)[0];
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: mv.from, to: mv.to }, veh) }); assert.equal(r.last('a', 'deny').reason, 'not-your-move');
  r.send('b', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: mv.from, to: mv.to }, veh) }); assert.ok(r.last('b', 'ops'));
});
test('coop (shared): either passenger may move, but only one move per team turn', () => {
  const r = started('coop', { coop: 'shared' }); begin(r, 'a'); r.clear();
  r.send('b', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'd2', to: 'd4' }, veh) }); assert.ok(r.last('a', 'ops'));
  r.clear(); const mv = whiteMoves(null, r.hub)[0];
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: mv.from, to: mv.to }, veh) }); assert.ok(r.last('b', 'ops'));
});
test('chess begin validation: bad FEN, bad config, car cannot deploy, double begin', () => {
  const r = started('duel');
  r.send('a', { t: 'chessBegin', req: { fen: 'garbage' } }); assert.equal(r.last('a', 'deny').reason, 'bad-fen');
  r.send('a', { t: 'chessBegin', req: { fen: OPEN, cfgJson: '{oops' } }); assert.equal(r.last('a', 'deny').reason, 'bad-config');
  r.send('b', { t: 'chessBegin', req: Object.assign({ fen: OPEN }, veh) }); assert.equal(r.last('b', 'deny').reason, 'car-cannot-deploy');
  r.send('a', { t: 'chessBegin', req: Object.assign({ fen: OPEN }, veh) }); assert.ok(r.last('a', 'ops'));
  r.send('a', { t: 'chessBegin', req: Object.assign({ fen: OPEN }, veh) }); assert.equal(r.last('a', 'deny').reason, 'chess-active');
  r.send('a', { t: 'chess', req: Object.assign({ op: 'nuke' }, veh) }); assert.equal(r.last('a', 'deny').reason, 'bad-op');
});
test('duel: P moves White, C moves Black, sides are enforced, replicas converge', () => {
  const r = started('duel', { role: 'P' }); // a = P (host), b = C
  const cp = replica(), cc = replica();
  begin(r, 'a'); apply(cp, r.last('a', 'ops')); apply(cc, r.last('b', 'ops')); r.clear();
  r.send('b', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e7', to: 'e5' }, veh) }); assert.equal(r.last('b', 'deny').reason, 'not-your-side');
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) });
  const w = r.last('b', 'ops'); assert.equal(w.turn, 'b', 'the server waits for the car'); apply(cp, r.last('a', 'ops')); apply(cc, w); r.clear();
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'd2', to: 'd4' }, veh) }); assert.equal(r.last('a', 'deny').reason, 'not-your-side');
  r.send('b', { t: 'chess', req: Object.assign({ op: 'card', card: 'pawnBoost', target: 'e7' }, veh) }); assert.equal(r.last('b', 'deny').reason, 'cards-not-allowed');
  r.send('b', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'a1', to: 'a5' }, veh) }); assert.equal(r.last('b', 'deny').reason, 'illegal');
  r.send('b', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e7', to: 'e5' }, veh) });
  const bm = r.last('a', 'ops'); assert.equal(bm.who, 'b'); assert.equal(bm.turn, 'w'); apply(cp, bm); apply(cc, r.last('b', 'ops'));
  assert.equal(cp.stateJson, cc.stateJson); assert.equal(cp.stateJson, r.hub.rooms.get('RM000').chess.stateJson);
});
test('duel: an absent car is replaced after the limit and the ride goes on', () => {
  const r = started('duel', { role: 'P' }, { carTurnLimit: 30 }); begin(r, 'a'); r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); r.clear();
  r.advance(20); assert.equal(r.sweep().filter(o => o.msg && o.msg.t === 'ops').length, 0);
  r.advance(15); r.send('a', { t: 'ping' }); r.send('b', { t: 'ping' }); const out = r.sweep().filter(o => o.msg && o.msg.t === 'ops');
  assert.equal(out.length, 2); assert.ok(out[0].msg.timeout); assert.equal(out[0].msg.turn, 'w');
});
test('duel car actions: authority cost, cooldown, forbidden names, passenger refused', () => {
  const r = started('duel', { role: 'P' }); // b is the car
  r.send('a', { t: 'carAction', action: 'RadioHijack' }); assert.equal(r.last('a', 'deny').reason, 'car-only');
  r.send('b', { t: 'carAction', action: 'killPassenger' }); assert.equal(r.last('b', 'deny').reason, 'forbidden-action');
  r.send('b', { t: 'carAction', action: 'openRequiredProgression' }); assert.equal(r.last('b', 'deny').reason, 'forbidden-action');
  r.send('b', { t: 'carAction', action: 'RouteManipulate' }); assert.equal(r.last('a', 'carAction').action, 'RouteManipulate'); assert.ok(Math.abs(r.last('a', 'carAction').authority - 12) < 0.01);
  r.send('b', { t: 'carAction', action: 'RouteManipulate' }); assert.equal(r.last('b', 'deny').reason, 'cooldown');
  r.send('b', { t: 'carAction', action: 'ExtremeAcceleration' }); assert.equal(r.last('b', 'deny').reason, 'authority', 'no god mode: 12 authority cannot buy a 42 action');
  r.advance(10); r.send('b', { t: 'carAction', action: 'RadioHijack' }); assert.equal(r.last('a', 'carAction').action, 'RadioHijack');
  r.advance(1000); r.send('b', { t: 'carAction', action: 'ExtremeAcceleration' }); assert.equal(r.last('a', 'carAction').action, 'ExtremeAcceleration');
  const g = r.hub.rooms.get('RM000').authority; assert.ok(g.value <= 100);
});
test('duel hazard action: needs an open board and White to move; the engine marks WARNING first', () => {
  const r = started('duel', { role: 'P' }); r.advance(100);
  r.send('b', { t: 'carAction', action: 'HazardGenerate' }); assert.equal(r.last('b', 'deny').reason, 'no-board');
  begin(r, 'a'); const cp = replica(), cc = replica(); apply(cp, r.last('a', 'ops')); apply(cc, r.last('b', 'ops')); r.clear();
  r.send('b', { t: 'carAction', action: 'HazardGenerate' });
  const ops = r.last('a', 'ops'); assert.ok(ops && ops.kind === 'hazard'); apply(cp, ops); apply(cc, r.last('b', 'ops'));
  const snap = E.handle(Object.assign({ op: 'query', fen: cp.fen, stateJson: cp.stateJson }, veh));
  assert.ok(snap.state.hazards.length >= 1 && snap.state.hazards.every(h => h.phase === 'WARNING'), 'hazards start as a one-turn warning');
});
test('disconnects: partner leaves coop, car leaves duel (AI takes over), passenger leaves duel (closed)', () => {
  const c = started('coop'); c.disconnect('b'); assert.equal(c.last('a', 'left').takeover, 'partner'); assert.equal(c.hub.rooms.get('RM000').coop.players, 1);
  const d = started('duel', { role: 'P' }); begin(d, 'a'); d.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); d.clear();
  d.disconnect('b'); assert.equal(d.last('a', 'left').takeover, 'car'); const bm = d.all('a', 'ops').pop(); assert.ok(bm && bm.who === 'b' && bm.turn === 'w', 'the AI plays the car’s pending move');
  d.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'd2', to: 'd4' }, veh) }); assert.equal(d.last('a', 'ops').turn, 'w', 'later turns are answered by AI automatically');
  const e = started('duel', { role: 'P' }); e.disconnect('a'); assert.equal(e.last('b', 'left').closed, true); assert.equal(e.hub.rooms.size, 0);
  const f = rig(); f.connect('a'); f.send('a', { t: 'create', mode: 'coop' }); f.disconnect('a'); assert.equal(f.hub.rooms.size, 0, 'empty rooms are removed');
});
test('host migration: host leaves a started coop and the survivor becomes host', () => {
  const r = started('coop'); r.disconnect('a'); assert.equal(r.hub.rooms.get('RM000').hostId, 'b');
  r.send('b', { t: 'ending', ending: 'Checkmate' }); assert.equal(r.last('b', 'deny'), undefined);
});
test('endings: host only, known endings only, relayed once', () => {
  const r = started('coop'); r.send('b', { t: 'ending', ending: 'Checkmate' }); assert.equal(r.last('b', 'deny').reason, 'host-only');
  r.send('a', { t: 'ending', ending: 'Banana' }); assert.equal(r.last('a', 'deny').reason, 'bad-ending');
  r.send('a', { t: 'ending', ending: 'Cheater' }); assert.equal(r.last('b', 'ending').ending, 'Cheater'); r.clear(); r.send('a', { t: 'ending', ending: 'Cheater' }); assert.equal(r.last('b', 'ending'), undefined);
});
test('resync returns the authoritative position', () => {
  const r = started('coop'); begin(r, 'a'); r.clear(); r.send('b', { t: 'resync' }); const m = r.last('b', 'resync'); assert.equal(m.fen, OPEN); assert.ok(m.stateJson.length > 10);
});
test('idle connections are swept; idle rooms are removed', () => {
  const r = rig(); r.connect('a'); r.send('a', { t: 'create', mode: 'coop' }); r.advance(70);
  const out = r.sweep(); assert.ok(out.some(o => o.close === 'a')); assert.equal(r.hub.size().rooms, 0);
});
test('full coop game, white AI-free random play: replicas never diverge and the game ends legally', () => {
  const r = started('coop', { coop: 'shared' }); const ca = replica(), cb = replica();
  begin(r, 'a', '6k1/5ppp/8/8/8/8/5PPP/3QR1K1 w - - 0 1', Object.assign({}, CFG, { hazards: false })); apply(ca, r.last('a', 'ops')); apply(cb, r.last('b', 'ops'));
  let over = false; let plies = 0;
  while (!over && plies < 40) {
    r.clear(); const mvs = whiteMoves(null, r.hub); if (!mvs.length) break;
    const m = mvs.sort((x, y) => (y.captured ? 1 : 0) - (x.captured ? 1 : 0))[plies % Math.max(1, Math.min(3, mvs.length))];
    r.send(plies % 2 ? 'a' : 'b', { t: 'chess', req: Object.assign({ op: 'act', kind: m.kind, from: m.from, to: m.to, promotion: m.promotion || '' }, veh) });
    const ops = r.last('a', 'ops'); assert.ok(ops, 'move accepted: ' + JSON.stringify(m)); apply(ca, ops); apply(cb, r.last('b', 'ops')); over = ops.over; plies++;
    assert.equal(ca.stateJson, cb.stateJson);
  }
  assert.ok(plies > 1);
});
test('chess lifecycle: retract is idempotent, a finished board allows a new one, start hazards are applied', () => {
  const r = started('coop', { coop: 'shared' });
  r.send('a', { t: 'chessBegin', challengeId: 'cal', req: Object.assign({ fen: '4k3/8/8/8/8/2N5/8/4K3 w - - 0 1', cfgJson: JSON.stringify({ road: true, hazards: true }), squares: 'c3' }, veh) });
  const ops = r.last('b', 'ops'); assert.equal(ops.ops.length, 3); assert.equal(ops.ops[2].op, 'roadEvent');
  const cp = replica(); apply(cp, ops); const snap = E.handle(Object.assign({ op: 'query', fen: cp.fen, stateJson: cp.stateJson }, veh)); assert.equal(snap.state.hazards[0].sq, 'c3');
  r.clear(); r.send('a', { t: 'chessEnd' }); assert.equal(r.all('b', 'chessEnd').length, 1);
  r.clear(); r.send('b', { t: 'chessEnd' }); assert.equal(r.all('a', 'chessEnd').length, 0, 'second retract is a no-op');
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e1', to: 'e2' }, veh) }); assert.equal(r.last('a', 'deny').reason, 'no-board');
  r.send('a', { t: 'chessBegin', challengeId: 'again', req: Object.assign({ fen: OPEN }, veh) }); assert.equal(r.last('b', 'ops').kind, 'begin');
});
test('a finished game frees the room for the next board', () => {
  const r = started('coop', { coop: 'shared' });
  begin(r, 'a', '6k1/5ppp/8/8/8/8/1Q6/6K1 w - - 0 1', { road: true });
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'b2', to: 'b8' }, veh) }); assert.equal(r.last('a', 'ops').over, true);
  r.send('a', { t: 'chessBegin', challengeId: 'next', req: Object.assign({ fen: OPEN }, veh) }); assert.equal(r.last('a', 'ops').kind, 'begin');
});

test('ops carry the co-op team state so every client can show whose move it is', () => {
  const r = started('coop', { coop: 'assist' }); begin(r, 'a');
  let ops = r.last('b', 'ops'); assert.equal(ops.team.operator, 0); assert.equal(ops.team.support, 1); assert.equal(ops.team.locked, false);
  r.send('b', { t: 'chess', req: Object.assign({ op: 'cyber', ability: 'firewall', target: 'e2' }, veh) }); ops = r.last('a', 'ops'); assert.equal(ops.team.cyber, true);
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); ops = r.last('b', 'ops'); assert.equal(ops.team.locked, false, 'a new team turn has begun'); assert.equal(ops.team.turn, 1); assert.equal(ops.team.cyber, false);
  const d = started('duel'); begin(d, 'a'); assert.equal(d.last('b', 'ops').team, undefined);
});

test('a persistent board (the ride game) resumes where it was retracted, and is forgotten once the game is over', () => {
  const r = started('coop', { coop: 'shared' }); const A = replica(), B = replica();
  r.send('a', { t: 'chessBegin', challengeId: 'ridegame', persist: true, req: Object.assign({ fen: OPEN, seed: 3, cfgJson: JSON.stringify(CFG) }, veh) }); replicate2(A, r.last('a', 'ops')); replicate2(B, r.last('b', 'ops'));
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); replicate2(A, r.last('a', 'ops')); replicate2(B, r.last('b', 'ops'));
  const midFen = A.fen; r.send('b', { t: 'chessEnd' });
  // a different board in between does not disturb it
  r.send('a', { t: 'chessBegin', challengeId: 'auth', req: Object.assign({ fen: '6k1/5ppp/8/8/8/8/1Q6/6K1 w - - 0 1', cfgJson: '{}' }, veh) }); assert.equal(r.last('b', 'ops').kind, 'begin'); r.send('a', { t: 'chessEnd' });
  r.clear(); r.send('b', { t: 'chessBegin', challengeId: 'ridegame', persist: true, req: Object.assign({ fen: OPEN }, veh) });
  const m = r.last('a', 'ops'); assert.equal(m.resume, true); assert.equal(m.fen, midFen); assert.equal(m.ops.length, 0); assert.equal(m.stateJson, A.stateJson, 'clients restore the exact engine state');
  r.send('a', { t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'd2', to: 'd4' }, veh) }); assert.ok(r.last('a', 'ops'), 'play continues from the resumed position');
});
function replicate2(c, msg) { if (msg.resume) { c.fen = msg.fen; c.stateJson = msg.stateJson; return; } apply(c, msg); }
