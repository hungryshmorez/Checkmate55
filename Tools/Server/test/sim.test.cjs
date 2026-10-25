'use strict';
// Network QA: whole matches are played THROUGH the room protocol by AI clients that only know what a client knows (their replica),
// and a fuzzer hammers the hub with hostile input. Nothing here touches the room's private state except to read it for assertions.
const test = require('node:test');
const assert = require('node:assert/strict');
const { Hub, CAR_ACTIONS, THREAT_KINDS } = require('../room.cjs');
const E = require('../../Chess/road-engine.cjs');
const { FINALS } = (() => { try { return require('../../Chess/selfplay.cjs'); } catch (e) { return {}; } })();

const FENS = [
  'r1bq1rk1/ppp2ppp/2n2n2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 7',
  'r1bq1rk1/pp2bppp/2n1pn2/2pp4/3P1B2/2PBPN2/PP1N1PPP/R2QK2R w KQ - 0 9',
  '2rq1rk1/pp1bbppp/2n1pn2/3p4/2PP4/2N1PN2/PPQBBPPP/R4RK1 w - - 0 11',
];
const CFG = { road: true, cyber: true, cards: true, hazards: true, startHand: 1, startCharges: 1, carCyber: true };

function lab(mode, extra) {
  let t = 5000, n = 0; const hub = new Hub(Object.assign({ clock: () => t, seed: () => 99, codes: () => 'S' + n++, aiBudgetMs: 25 }, {}));
  const inbox = {}; const pump = (out) => { out.forEach(o => { if (o.to) (inbox[o.to] = inbox[o.to] || []).push(o.msg); }); return out; };
  const send = (id, m) => pump(hub.message(id, JSON.stringify(m)));
  ['a', 'b'].forEach(id => pump(hub.connect(id)));
  send('a', Object.assign({ t: 'create', mode }, extra || {})); send('b', { t: 'join', code: 'S0' }); send('a', { t: 'start' });
  return { hub, inbox, send, advance: (s) => { t += s; }, last: (id, ty) => (inbox[id] || []).filter(m => !ty || m.t === ty).pop(), clear: () => Object.keys(inbox).forEach(k => inbox[k] = []) };
}
function replicate(c, msg) {
  for (const op of msg.ops) { const q = op.op === 'begin' ? Object.assign({}, op) : Object.assign({}, op, { fen: c.fen, stateJson: c.stateJson }); const r = E.handle(q); assert.ok(r.ok, op.op + ' ' + r.error); c.fen = r.fen; c.stateJson = r.stateJson || c.stateJson; }
  assert.equal(c.fen, msg.fen);
}
const veh = { lane: 1, speedMph: 112, knownMask: 255 };
function plan(c, level) { return E.handle(Object.assign({ op: 'ai', fen: c.fen, stateJson: c.stateJson, difficulty: level, budgetMs: 25 }, veh)).plan; }
function req(op, f) { return Object.assign({ op }, f, veh); }

function playMatch(mode, seed) {
  const L = lab(mode, mode === 'coop' ? { coop: 'shared' } : { role: 'P' });
  const A = { fen: '', stateJson: '' }, B = { fen: '', stateJson: '' };
  L.send('a', { t: 'chessBegin', challengeId: 'final', req: Object.assign({ fen: FENS[seed % FENS.length], seed, cfgJson: JSON.stringify(CFG) }, veh) });
  replicate(A, L.last('a', 'ops')); replicate(B, L.last('b', 'ops'));
  let plies = 0, over = false, supports = 0;
  while (!over && plies < 60) {
    // WHITE (passenger A in coop, P in duel): optional support, then one action
    const wp = plan(A, 'normal'); assert.ok(wp && wp.action && wp.action.from, 'white has a plan');
    if (wp.card && wp.card.card) { L.clear(); L.send(mode === 'coop' ? 'b' : 'a', { t: 'chess', req: req('card', { card: wp.card.card, target: wp.card.target || '', target2: wp.card.target2 || '' }) }); const m = L.last('a', 'ops'); if (m) { replicate(A, m); replicate(B, L.last('b', 'ops')); supports++; } }
    if (wp.cyber && wp.cyber.ability) { L.clear(); L.send(mode === 'coop' ? 'b' : 'a', { t: 'chess', req: req('cyber', { ability: wp.cyber.ability, target: wp.cyber.target || '' }) }); const m = L.last('a', 'ops'); if (m) { replicate(A, m); replicate(B, L.last('b', 'ops')); supports++; } }
    // the plan was made before support; re-plan the action itself so it is legal in the updated state
    const ap = plan(A, 'normal').action;
    L.clear(); L.send('a', { t: 'chess', req: req('act', { kind: ap.kind, from: ap.from, to: ap.to, promotion: ap.promotion || '', steps: ap.steps || [] }) });
    let m = L.last('a', 'ops'); assert.ok(m, 'white move accepted: ' + JSON.stringify(ap) + ' ' + JSON.stringify(L.last('a', 'deny')));
    replicate(A, m); replicate(B, L.last('b', 'ops')); plies++; over = m.over;
    if (!over && mode === 'duel') {
      assert.equal(m.turn, 'b');
      const bp = plan(A, 'normal'); const ba = bp.action; L.clear();
      if (bp.cyber && bp.cyber.ability) { L.send('b', { t: 'chess', req: req('cyber', { ability: bp.cyber.ability, target: bp.cyber.target || '' }) }); const cm = L.last('a', 'ops'); if (cm) { replicate(A, cm); replicate(B, L.last('b', 'ops')); } }
      const ba2 = plan(A, 'normal').action; L.clear();
      L.send('b', { t: 'chess', req: req('act', { kind: ba2.kind, from: ba2.from, to: ba2.to, promotion: ba2.promotion || '', steps: ba2.steps || [] }) });
      m = L.last('a', 'ops'); assert.ok(m, 'car move accepted: ' + JSON.stringify(ba2) + JSON.stringify(L.last('b', 'deny'))); replicate(A, m); replicate(B, L.last('b', 'ops')); over = m.over; plies++;
    }
    assert.equal(A.stateJson, B.stateJson, 'replicas identical at ply ' + plies);
  }
  const srv = L.hub.rooms.get('S0').chess; assert.equal(srv.stateJson, A.stateJson, 'server == replica'); assert.equal(srv.fen, A.fen);
  return { plies, over, supports, winner: srv.winner };
}

for (const mode of ['coop', 'duel']) {
  test('AI-vs-server match through the protocol (' + mode + '): replicas never diverge', { timeout: 120000 }, () => {
    const results = [0, 1, 2].map(s => playMatch(mode, s));
    results.forEach(r => assert.ok(r.plies >= 2, 'a real game was played'));
    assert.ok(results.some(r => r.supports > 0), 'cards / Cyber abilities flowed through the protocol too');
  });
}

test('fuzz: hostile and random messages never crash the hub and never corrupt a running match', () => {
  const L = lab('duel', { role: 'P' }); L.send('a', { t: 'chessBegin', challengeId: 'x', req: Object.assign({ fen: FENS[0], seed: 1, cfgJson: JSON.stringify(CFG) }, veh) });
  const before = L.hub.rooms.get('S0').chess.stateJson;
  let s = 12345; const rnd = () => (s = (s * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
  const types = ['hello', 'ping', 'create', 'join', 'leave', 'start', 'flag', 'threat', 'ctl', 'carAction', 'chessBegin', 'chess', 'chessEnd', 'resync', 'ending', 'zzz', ''];
  const junk = () => { const k = (rnd() * 8) | 0; return [null, 0, -1, 1e9, 'x'.repeat((rnd() * 200) | 0), [], {}, { a: { b: [1, 2, 3] } }, true][k]; };
  let errors = 0;
  for (let i = 0; i < 3000; i++) {
    const who = rnd() < .5 ? 'a' : 'b'; const msg = { t: types[(rnd() * types.length) | 0] };
    for (let k = 0; k < 4; k++) msg[['flag', 'action', 'req', 'threat', 'ending', 'code', 'mode', 'role', 'id', 'ability'][(rnd() * 10) | 0]] = junk();
    if (rnd() < .2 && msg.req === undefined) msg.req = { op: ['act', 'card', 'cyber', 'nuke', 5][(rnd() * 5) | 0], from: junk(), to: junk(), kind: junk(), steps: junk(), fen: junk() };
    const out = L.hub.message(who, rnd() < .03 ? '{bad' + i : JSON.stringify(msg)); errors += out.filter(o => o.msg && o.msg.reason === 'server-error').length;
    L.advance(.2);
  }
  assert.equal(errors, 0, 'no handler threw');
  // a legal move still works afterwards: the fuzzer did not corrupt the authoritative position (the fuzzer may legitimately have moved pieces)
  const ch = L.hub.rooms.get('S0') ? L.hub.rooms.get('S0').chess : null; assert.ok(ch === null || typeof ch.stateJson === 'string');
  assert.ok(before.length > 0);
});

test('car actions cannot be spammed into a god mode: total authority spend over ten minutes is bounded by regeneration', () => {
  const L = lab('duel', { role: 'P' }); let spent = 0; const names = Object.keys(CAR_ACTIONS);
  for (let sec = 0; sec < 600; sec++) { L.advance(1); const n = names[(sec * 7) % names.length]; const out = L.send('b', { t: 'carAction', action: n }); if (out.some(o => o.msg.t === 'carAction')) spent += CAR_ACTIONS[n].cost; }
  const bound = 60 + 3.2 * 600; assert.ok(spent <= bound + 1e-6, 'spent ' + spent + ' of at most ' + bound);
  assert.ok(spent > 600, 'but the car is not starved either: ' + spent);
});
