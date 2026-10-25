'use strict';
// CHECKMATE 55: OVERRIDE - authoritative room logic for Co-op Escape and Driverless Duel.
//
// This module is pure: it never touches a socket or a timer. A transport (server.cjs) feeds it text messages and delivers the
// "outbox" it returns, which makes every rule testable without a network.
//
// What the server owns (so that no client can cheat or desync it):
//   * rooms, slots, who is the host, who is the car
//   * the one-White-action-per-team-turn rule (CoopTurn) and the car's SYSTEM AUTHORITY budget and cooldowns
//   * the chess position: every action is applied to the SAME road engine the clients run, so legality is decided here,
//     the Black AI reply is computed here (time-budgeted search is not deterministic across machines), and clients
//     simply replay the exact op list the server broadcasts
// What clients own: the cabin (physics, hands, audio). They replicate puzzle progress through validated flags.
const E = require('../Chess/road-engine.cjs');
const { Chess } = require('../Chess/chess.cjs');

const PROTO = 1;
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const MAX_MESSAGE = 20000;
const COOP_MODES = ['shared', 'alternating', 'assist'];

// ---- System Authority (kept identical to Assets/Checkmate55/Scripts/Core/Roles.cs: see test/vectors.json)
const CAR_ACTIONS = {
  RadioHijack: { cost: 8, cd: 12 }, WindowClose: { cost: 8, cd: 10 }, TintUp: { cost: 12, cd: 18 }, ClimateCut: { cost: 12, cd: 20 },
  LockCycle: { cost: 14, cd: 15 }, SeatAdjust: { cost: 22, cd: 30 }, ForcedLaneChange: { cost: 24, cd: 22 }, HazardGenerate: { cost: 34, cd: 28 },
  RouteManipulate: { cost: 48, cd: 60 }, ExtremeAcceleration: { cost: 42, cd: 50 },
};
const AUTH_MAX = 100, AUTH_REGEN = 3.2, AUTH_START = 60;
class Authority {
  constructor(now) { this.value = AUTH_START; this.last = now || 0; this.readyAt = {}; }
  update(now) { const dt = Math.max(0, now - this.last); this.last = now; this.value = Math.min(AUTH_MAX, this.value + AUTH_REGEN * dt); }
  cooldownLeft(a, now) { return Math.max(0, (this.readyAt[a] || 0) - now); }
  why(a, now) {
    if (!Object.prototype.hasOwnProperty.call(CAR_ACTIONS, a)) return 'unknown-action';
    this.update(now);
    if (this.cooldownLeft(a, now) > 0) return 'cooldown';
    if (this.value < CAR_ACTIONS[a].cost) return 'authority';
    return '';
  }
  try(a, now) { const w = this.why(a, now); if (w) return w; this.value -= CAR_ACTIONS[a].cost; this.readyAt[a] = now + CAR_ACTIONS[a].cd; return ''; }
}

// ---- Co-op team turn (kept identical to Roles.cs CoopTurn)
class CoopTurn {
  constructor(mode, players) { this.mode = mode; this.players = Math.max(1, Math.min(2, players)); this.turn = 0; this.chessLocked = false; this.cardUsed = false; this.cyberUsed = false; this.lockedBy = -1; }
  get chessOperator() { if (this.players < 2) return 0; switch (this.mode) { case 'shared': return -1; case 'alternating': return this.turn % 2; default: return 0; } }
  get supportOperator() { if (this.players < 2) return 0; return this.mode === 'assist' ? 1 : -1; }
  canConfirmChess(p) { if (this.chessLocked || p < 0 || p >= this.players) return false; const op = this.chessOperator; return op === -1 || op === p; }
  tryConfirmChess(p) { if (!this.canConfirmChess(p)) return false; this.chessLocked = true; this.lockedBy = p; return true; }
  canSupport(p, kind) {
    if (p < 0 || p >= this.players) return false;
    if (kind === 'card' && this.cardUsed) return false; if (kind === 'cyber' && this.cyberUsed) return false;
    const op = this.supportOperator; return op === -1 || op === p;
  }
  trySupport(p, kind) {
    if (!this.canSupport(p, kind)) return false;
    if (this.mode === 'assist' && this.players > 1 && (this.cardUsed || this.cyberUsed)) return false;
    if (kind === 'card') this.cardUsed = true; else this.cyberUsed = true; return true;
  }
  nextTurn() { this.turn++; this.chessLocked = false; this.lockedBy = -1; this.cardUsed = false; this.cyberUsed = false; }
}

// Flags a client may announce. Anything outside these families is refused, so a modified client cannot, for example, announce an ending.
const FLAG_FAMILIES = ['security.', 'rule.', 'puzzle.', 'clue.', 'item.', 'dock.', 'tested-', 'cal.'];
const FLAG_EXACT = ['glovebox-authenticated', 'glovebox-interest', 'authentication-known', 'trim-inspected', 'maintenance-mode', 'phone.powered', 'outside.contact', 'police.visible', 'lockdown', 'CHESS_CHALLENGE_COMPLETED', 'collision.started'];
function flagAllowed(f) { if (typeof f !== 'string' || f.length < 2 || f.length > 64 || !/^[A-Za-z0-9_.\-]+$/.test(f)) return false; return FLAG_EXACT.includes(f) || FLAG_FAMILIES.some(p => f.startsWith(p)); }
const THREAT_KINDS = ['RadioChange', 'VolumeUp', 'VolumeDown', 'SpeakerRoute', 'FavoriteRemoved', 'WindowClose', 'TintUp', 'ClimateShutdown', 'ClimateReset', 'SeatCreep', 'LockCycle', 'Accelerate', 'LaneChange', 'WarningDisplay', 'NavChange', 'SunshadeClose', 'BeltTighten', 'PuzzleInterference', 'JamAudio'];

function sanitizeVehicle(req) {
  const v = {};
  if (typeof req.lane === 'number' && req.lane >= 0) v.lane = Math.min(2, req.lane | 0);
  if (typeof req.speedMph === 'number' && req.speedMph >= 0) v.speedMph = Math.min(200, req.speedMph);
  if (typeof req.knownMask === 'number' && req.knownMask >= 0) v.knownMask = req.knownMask | 0;
  if (typeof req.overclockLane === 'number') v.overclockLane = req.overclockLane | 0;
  if (typeof req.roadEnabled === 'boolean') v.roadEnabled = req.roadEnabled;
  if (typeof req.jumpKnown === 'boolean') v.jumpKnown = req.jumpKnown;
  if (typeof req.chargeKnown === 'boolean') v.chargeKnown = req.chargeKnown;
  return v;
}
function validFen(f) { if (typeof f !== 'string' || f.length > 100) return false; try { return new Chess().load(f); } catch (e) { return false; } }

class Room {
  constructor(code, opts, clock, rng) {
    this.code = code; this.clock = clock; this.rng = rng;
    this.mode = opts.mode; this.coopMode = COOP_MODES.includes(opts.coop) ? opts.coop : 'assist'; this.skill = ['casual', 'smart', 'expert', 'relentless'].includes(opts.skill) ? opts.skill : 'smart';
    this.members = []; // {id,name,slot,left}
    this.hostId = null; this.started = false; this.closed = false; this.seed = 0;
    this.flags = new Set(); this.touched = clock();
    this.coop = null; this.authority = null; this.aiCar = false;
    this.chess = null; // {fen,stateJson,turn,over,winner,reason,seq,veh,challengeId,blackSince}
    this.ended = null;
  }
  get slotsFor() { return this.mode === 'coop' ? ['A', 'B'] : ['P', 'C']; }
  member(id) { return this.members.find(m => m.id === id && !m.left); }
  bySlot(slot) { return this.members.find(m => m.slot === slot && !m.left); }
  live() { return this.members.filter(m => !m.left); }
  roster() { return this.live().map(m => ({ slot: m.slot, name: m.name, host: m.id === this.hostId })); }
  playerIndex(m) { return m.slot === 'A' ? 0 : m.slot === 'B' ? 1 : -1; }
}

class Hub {
  constructor(opts) {
    opts = opts || {};
    this.clock = opts.clock || (() => Date.now() / 1000);
    this.seedFn = opts.seed || (() => (Math.random() * 0x7fffffff) | 0);
    this.codeFn = opts.codes || (() => { let s = ''; for (let i = 0; i < 5; i++) s += ALPHABET[(Math.random() * ALPHABET.length) | 0]; return s; });
    this.rooms = new Map(); this.conns = new Map(); this.maxRooms = opts.maxRooms || 500;
    this.aiBudgetMs = opts.aiBudgetMs || 200; this.carTurnLimit = opts.carTurnLimit || 90;
  }
  // ---- connection lifecycle
  connect(id) { this.conns.set(id, { id, name: 'Passenger', room: null, tokens: 60, refill: this.clock(), seen: this.clock() }); return [{ to: id, msg: { t: 'welcome', id, proto: PROTO } }]; }
  disconnect(id) {
    const c = this.conns.get(id); if (!c) return []; this.conns.delete(id);
    return c.room ? this.leaveRoom(c, 'disconnect') : [];
  }
  size() { return { conns: this.conns.size, rooms: this.rooms.size }; }
  // ---- message entry point
  message(id, text) {
    const c = this.conns.get(id); if (!c) return [];
    const now = this.clock(); c.seen = now;
    c.tokens = Math.min(60, c.tokens + (now - c.refill) * 30); c.refill = now;
    if (c.tokens < 1) return [this.deny(id, 'rate-limited')]; c.tokens -= 1;
    if (typeof text !== 'string' || text.length > MAX_MESSAGE) return [this.deny(id, 'too-large')];
    let m; try { m = JSON.parse(text); } catch (e) { return [this.deny(id, 'bad-json')]; }
    if (!m || typeof m !== 'object' || typeof m.t !== 'string') return [this.deny(id, 'bad-message')];
    try { return this.dispatch(c, m, now) || []; } catch (e) { return [this.deny(id, 'server-error', m.t)]; }
  }
  deny(to, reason, ref, extra) { return { to, msg: Object.assign({ t: 'deny', reason, ref: ref || '' }, extra || {}) }; }
  all(room, msg, except) { return room.live().filter(m => m.id !== except).map(m => ({ to: m.id, msg })); }
  dispatch(c, m, now) {
    switch (m.t) {
      case 'hello': c.name = String(m.name || 'Passenger').replace(/[^\w \-.]/g, '').slice(0, 16) || 'Passenger'; return [];
      case 'ping': return [{ to: c.id, msg: { t: 'pong', at: m.at || 0 } }];
      case 'create': return this.create(c, m);
      case 'join': return this.join(c, m);
      case 'leave': return c.room ? this.leaveRoom(c, 'left') : [];
    }
    const room = c.room; if (!room) return [this.deny(c.id, 'not-in-room', m.t)];
    room.touched = now;
    switch (m.t) {
      case 'start': return this.start(c, room, m);
      case 'flag': return this.flag(c, room, m);
      case 'threat': return this.threat(c, room, m);
      case 'ctl': return this.all(room, { t: 'ctl', id: String(m.id || '').slice(0, 40), by: room.member(c.id).slot }, c.id);
      case 'carAction': return this.carAction(c, room, m, now);
      case 'chessBegin': return this.chessBegin(c, room, m);
      case 'chess': return this.chessIntent(c, room, m, now);
      case 'chessEnd': return this.chessEnd(c, room);
      case 'resync': return this.resync(c, room);
      case 'ending': return this.ending(c, room, m);
    }
    return [this.deny(c.id, 'unknown-type', m.t)];
  }
  // ---- rooms
  newCode() { for (let i = 0; i < 20; i++) { const code = this.codeFn(); if (!this.rooms.has(code)) return code; } return null; }
  create(c, m) {
    if (c.room) return [this.deny(c.id, 'already-in-room', 'create')];
    if (m.mode !== 'coop' && m.mode !== 'duel') return [this.deny(c.id, 'bad-mode', 'create')];
    if (this.rooms.size >= this.maxRooms) return [this.deny(c.id, 'server-full', 'create')];
    const code = this.newCode(); if (!code) return [this.deny(c.id, 'server-full', 'create')];
    const room = new Room(code, m, this.clock, this.seedFn); this.rooms.set(code, room);
    const slot = room.mode === 'coop' ? 'A' : (m.role === 'C' ? 'C' : 'P');
    room.members.push({ id: c.id, name: c.name, slot, left: false }); room.hostId = c.id; c.room = room;
    return [{ to: c.id, msg: { t: 'room', code, slot, mode: room.mode, coop: room.coopMode, skill: room.skill, roster: room.roster() } }];
  }
  join(c, m) {
    if (c.room) return [this.deny(c.id, 'already-in-room', 'join')];
    const code = String(m.code || '').toUpperCase().trim(); const room = this.rooms.get(code);
    if (!room || room.closed) return [this.deny(c.id, 'no-room', 'join')];
    if (room.started) return [this.deny(c.id, 'already-started', 'join')];
    const free = room.slotsFor.filter(s => !room.bySlot(s));
    if (!free.length) return [this.deny(c.id, 'room-full', 'join')];
    const slot = free[0]; room.members.push({ id: c.id, name: c.name, slot, left: false }); c.room = room;
    const out = [{ to: c.id, msg: { t: 'room', code, slot, mode: room.mode, coop: room.coopMode, skill: room.skill, roster: room.roster() } }];
    return out.concat(this.all(room, { t: 'roster', roster: room.roster() }, c.id));
  }
  leaveRoom(c, why) {
    const room = c.room; c.room = null; if (!room) return [];
    const mem = room.members.find(x => x.id === c.id && !x.left); if (!mem) return [];
    mem.left = true; const out = [];
    const live = room.live();
    if (!live.length) { this.rooms.delete(room.code); return out; }
    if (room.hostId === c.id) room.hostId = live[0].id;
    if (!room.started) return out.concat(this.all(room, { t: 'roster', roster: room.roster() }));
    // A started match: the survivor continues. A missing teammate / car is replaced by AI so the ride is never stuck.
    let note = { t: 'left', slot: mem.slot, why, host: room.bySlot(live[0].slot).id === room.hostId };
    if (room.mode === 'coop') { room.coop.players = 1; note.takeover = 'partner'; }
    else if (mem.slot === 'C') { room.aiCar = true; note.takeover = 'car'; }
    else { room.closed = true; note.takeover = 'none'; note.closed = true; }
    out.push(...this.all(room, note));
    if (room.aiCar && room.chess && !room.chess.over && room.chess.turn === 'b') out.push(...this.blackAi(room));
    if (room.closed) this.rooms.delete(room.code);
    return out;
  }
  start(c, room, m) {
    if (room.hostId !== c.id) return [this.deny(c.id, 'host-only', 'start')];
    if (room.started) return [this.deny(c.id, 'already-started', 'start')];
    if (room.live().length < 2) return [this.deny(c.id, 'need-two', 'start')];
    room.started = true; room.seed = (typeof m.seed === 'number' ? m.seed : this.seedFn()) | 0;
    room.coop = room.mode === 'coop' ? new CoopTurn(room.coopMode, 2) : null;
    room.authority = new Authority(this.clock());
    return room.live().map(mem => ({ to: mem.id, msg: { t: 'start', seed: room.seed, mode: room.mode, coop: room.coopMode, skill: room.skill, slot: mem.slot, host: mem.id === room.hostId, roster: room.roster(), authority: room.authority.value } }));
  }
  flag(c, room, m) {
    if (!room.started) return [this.deny(c.id, 'not-started', 'flag')];
    const mem = room.member(c.id);
    if (mem.slot === 'C') return [this.deny(c.id, 'car-cannot-set-flags', 'flag')];
    if (!flagAllowed(m.flag)) return [this.deny(c.id, 'flag-refused', 'flag', { flag: String(m.flag).slice(0, 40) })];
    if (room.flags.has(m.flag)) return [];
    room.flags.add(m.flag); return this.all(room, { t: 'flag', flag: m.flag, by: mem.slot }, c.id);
  }
  threat(c, room, m) {
    if (!room.started || room.mode !== 'coop' || c.id !== room.hostId) return [this.deny(c.id, 'host-only', 'threat')];
    const a = m.threat || {}; if (!THREAT_KINDS.includes(a.kind)) return [this.deny(c.id, 'bad-threat', 'threat')];
    return this.all(room, { t: 'threat', threat: { kind: a.kind, arg: String(a.arg || '').slice(0, 24), strength: Math.max(0, Math.min(3, +a.strength || 1)), line: String(a.line || '').slice(0, 140) } }, c.id);
  }
  carAction(c, room, m, now) {
    const mem = room.member(c.id);
    if (!room.started || room.mode !== 'duel' || mem.slot !== 'C') return [this.deny(c.id, 'car-only', 'carAction')];
    const name = String(m.action || '');
    if (!Object.prototype.hasOwnProperty.call(CAR_ACTIONS, name)) return [this.deny(c.id, 'forbidden-action', 'carAction', { action: name.slice(0, 40) })];
    if (name === 'HazardGenerate') {
      const ch = room.chess; if (!ch || ch.over || ch.turn !== 'w' || !ch.hazards) return [this.deny(c.id, 'no-board', 'carAction')];
    }
    const why = room.authority.try(name, now);
    if (why) return [this.deny(c.id, why, 'carAction', { action: name, authority: Math.round(room.authority.value * 10) / 10, wait: Math.ceil(room.authority.cooldownLeft(name, now)) })];
    const out = this.all(room, { t: 'carAction', action: name, authority: Math.round(room.authority.value * 10) / 10 });
    if (name === 'HazardGenerate') out.push(...this.hazardOps(room));
    return out;
  }
  ending(c, room, m) {
    if (c.id !== room.hostId || !room.started) return [this.deny(c.id, 'host-only', 'ending')];
    const e = String(m.ending || ''); if (!['Checkmate', 'Cheater', 'PreviousPassenger', 'SystemEscape', 'Destination'].includes(e)) return [this.deny(c.id, 'bad-ending', 'ending')];
    if (room.ended) return []; room.ended = e;
    return this.all(room, { t: 'ending', ending: e }, c.id);
  }
  // ---- chess
  vehicleOf(room, req) { const v = Object.assign({}, room.chess ? room.chess.veh : {}, sanitizeVehicle(req || {})); if (room.chess) room.chess.veh = v; return v; }
  run(room, op, fields) {
    const ch = room.chess;
    const q = Object.assign({}, fields, ch.veh, { op, fen: ch.fen, stateJson: ch.stateJson });
    const r = E.handle(q);
    return { q, r };
  }
  absorb(room, q, r, ops) {
    const ch = room.chess; ch.stateJson = r.stateJson || ch.stateJson;
    if (r.fen) ch.fen = r.fen;
    if (typeof r.turn === 'string') ch.turn = r.turn;
    const o = Object.assign({}, q); delete o.fen; delete o.stateJson; ops.push(o);
    if (r.over) { if (room.saved && ch.challengeId) delete room.saved[ch.challengeId]; ch.over = true; ch.active = false; ch.winner = r.winner || ''; ch.reason = r.reason || ''; }
  }
  chessBegin(c, room, m) {
    const mem = room.member(c.id);
    if (!room.started) return [this.deny(c.id, 'not-started', 'chessBegin')];
    if (mem.slot === 'C') return [this.deny(c.id, 'car-cannot-deploy', 'chessBegin')];
    if (room.chess && !room.chess.over && room.chess.active) return [this.deny(c.id, 'chess-active', 'chessBegin')];
    const persistId = m.persist ? String(m.challengeId || '').slice(0, 24) : '';
    // a persistent board (the RIDE GAME) resumes exactly where it was retracted
    if (persistId && room.saved && room.saved[persistId]) {
      const sv = room.saved[persistId]; room.chess = Object.assign({}, sv, { active: true, over: false });
      if (room.coop) room.coop = new CoopTurn(room.coopMode, room.live().length > 1 ? 2 : 1);
      return this.broadcastOps(room, [], 'begin', { challengeId: persistId, resume: true, stateJson: room.chess.stateJson });
    }
    const req = m.req || {}; if (!validFen(req.fen)) return [this.deny(c.id, 'bad-fen', 'chessBegin')];
    let cfg; try { cfg = req.cfgJson ? JSON.parse(String(req.cfgJson).slice(0, 4000)) : {}; } catch (e) { return [this.deny(c.id, 'bad-config', 'chessBegin')]; }
    const veh = sanitizeVehicle(req); const ops = [];
    room.chess = { fen: req.fen, stateJson: '', turn: 'w', over: false, winner: '', reason: '', seq: 0, veh, challengeId: String(m.challengeId || '').slice(0, 24), active: true, hazards: !!cfg.hazards, black: null, blackSince: 0, persist: !!persistId };
    const begin = E.handle(Object.assign({ seed: (req.seed | 0) }, veh, { op: 'begin', fen: req.fen, cfgJson: JSON.stringify(cfg), knownMask: veh.knownMask === undefined ? -1 : veh.knownMask }));
    if (!begin.ok) { room.chess = null; return [this.deny(c.id, 'begin-failed', 'chessBegin')]; }
    const bq = { op: 'begin', fen: req.fen, seed: req.seed | 0, cfgJson: JSON.stringify(cfg) }; Object.assign(bq, veh);
    room.chess.stateJson = begin.stateJson; room.chess.fen = begin.fen; room.chess.turn = begin.turn || 'w'; ops.push(bq);
    const st = this.run(room, 'startTurn', {}); this.absorb(room, st.q, st.r, ops);
    if (typeof req.squares === 'string' && /^[a-h1-8,]{0,40}$/.test(req.squares) && req.squares) {
      const h = this.run(room, 'roadEvent', { squares: req.squares }); if (h.r.ok) this.absorb(room, h.q, h.r, ops);
    }
    if (room.coop) room.coop = new CoopTurn(room.coopMode, room.live().length > 1 ? 2 : 1);
    return this.broadcastOps(room, ops, 'begin', { challengeId: room.chess.challengeId });
  }
  broadcastOps(room, ops, kind, extra) {
    const ch = room.chess; ch.seq++;
    const c = room.coop; const team = c ? { turn: c.turn, operator: c.chessOperator, support: c.supportOperator, locked: c.chessLocked, card: c.cardUsed, cyber: c.cyberUsed, players: c.players, mode: c.mode } : null;
    return this.all(room, Object.assign({ t: 'ops', kind, seq: ch.seq, who: extra && extra.who || '', ops, fen: ch.fen, turn: ch.turn, over: !!ch.over, winner: ch.winner, reason: ch.reason }, team ? { team } : {}, extra || {}));
  }
  chessIntent(c, room, m, now) {
    const mem = room.member(c.id); const ch = room.chess;
    if (!room.started) return [this.deny(c.id, 'not-started', 'chess')];
    if (!ch || ch.over || !ch.active) return [this.deny(c.id, 'no-board', 'chess')];
    const req = m.req || {}; const op = req.op;
    if (!['act', 'card', 'cyber'].includes(op)) return [this.deny(c.id, 'bad-op', 'chess')];
    const side = ch.turn; const pidx = room.playerIndex(mem);
    // ---- permission
    if (room.mode === 'coop') {
      if (side !== 'w') return [this.deny(c.id, 'not-white-turn', 'chess')];
      if (op === 'act' && !room.coop.canConfirmChess(pidx)) return [this.deny(c.id, room.coop.chessLocked ? 'turn-locked' : 'not-your-move', 'chess', { operator: room.coop.chessOperator })];
      if (op !== 'act' && !room.coop.canSupport(pidx, op === 'card' ? 'card' : 'cyber')) return [this.deny(c.id, 'support-unavailable', 'chess', { operator: room.coop.supportOperator })];
      if (op !== 'act' && room.coop.mode === 'assist' && room.coop.players > 1 && (room.coop.cardUsed || room.coop.cyberUsed)) return [this.deny(c.id, 'support-unavailable', 'chess')];
    } else {
      if (side === 'w' && mem.slot !== 'P') return [this.deny(c.id, 'not-your-side', 'chess')];
      if (side === 'b' && mem.slot !== 'C') return [this.deny(c.id, 'not-your-side', 'chess')];
      if (side === 'b' && op === 'card') return [this.deny(c.id, 'cards-not-allowed', 'chess')];
    }
    this.vehicleOf(room, req);
    const fields = {}; for (const k of ['kind', 'from', 'to', 'promotion', 'steps', 'card', 'target', 'target2', 'ability']) if (req[k] !== undefined) fields[k] = req[k];
    const ops = []; const first = this.run(room, op, fields);
    if (!first.r.ok || (op === 'act' && !first.r.accepted)) return [this.deny(c.id, 'illegal', 'chess', { error: String(first.r.error || first.r.locked || '').slice(0, 60) })];
    this.absorb(room, first.q, first.r, ops);
    if (op === 'card') room.coop && room.coop.trySupport(pidx, 'card');
    if (op === 'cyber') room.coop && room.coop.trySupport(pidx, 'cyber');
    if (op !== 'act') return this.broadcastOps(room, ops, 'support', { who: side });
    if (room.coop && side === 'w') room.coop.tryConfirmChess(pidx);
    if (ch.over) return this.broadcastOps(room, ops, 'act', { who: side });
    if (side === 'w') {
      if (room.mode === 'coop' || room.aiCar) { this.blackAiOps(room, ops); }
      else { const st = this.run(room, 'startTurn', {}); this.absorb(room, st.q, st.r, ops); ch.blackSince = now; }
    } else {
      const st = this.run(room, 'startTurn', {}); this.absorb(room, st.q, st.r, ops); room.coop && room.coop.nextTurn();
    }
    return this.broadcastOps(room, ops, 'act', { who: side });
  }
  // The server plays Black for the car AI: startTurn, optional card / cyber, one action, then White's startTurn.
  blackAiOps(room, ops) {
    const ch = room.chess;
    const bt = this.run(room, 'startTurn', {}); this.absorb(room, bt.q, bt.r, ops);
    if (!ch.over) {
      const p = this.run(room, 'ai', { difficulty: this.aiLevel(room), budgetMs: this.aiBudgetMs });
      const plan = p.r && p.r.plan;
      if (p.r.ok && plan && plan.action && plan.action.from) {
        if (plan.card && plan.card.card) { const c1 = this.run(room, 'card', { card: plan.card.card, target: plan.card.target || '', target2: plan.card.target2 || '' }); if (c1.r.ok) this.absorb(room, c1.q, c1.r, ops); }
        if (plan.cyber && plan.cyber.ability) { const c2 = this.run(room, 'cyber', { ability: plan.cyber.ability, target: plan.cyber.target || '' }); if (c2.r.ok) this.absorb(room, c2.q, c2.r, ops); }
        const a = this.run(room, 'act', { kind: plan.action.kind, from: plan.action.from, to: plan.action.to, promotion: plan.action.promotion || '', steps: plan.action.steps || [] });
        if (a.r.ok && a.r.accepted) this.absorb(room, a.q, a.r, ops); else this.fallbackAct(room, ops);
      } else this.fallbackAct(room, ops);
    }
    if (!ch.over) { const wt = this.run(room, 'startTurn', {}); this.absorb(room, wt.q, wt.r, ops); room.coop && room.coop.nextTurn(); }
  }
  fallbackAct(room, ops) {
    const q = this.run(room, 'query', {}); const m = (q.r.moves || []).find(x => x.kind === 'move' || x.kind === 'castle'); if (!m) return;
    const a = this.run(room, 'act', { kind: m.kind, from: m.from, to: m.to, promotion: m.promotion || '' }); if (a.r.ok && a.r.accepted) this.absorb(room, a.q, a.r, ops);
  }
  aiLevel(room) { return room.mode === 'duel' ? 'casual' : ({ casual: 'easy', smart: 'normal', expert: 'hard', relentless: 'brutal' }[room.skill] || 'normal'); }
  // Used when the car disconnects after White has moved: Black's turn was already started, so just play it.
  blackAi(room) {
    const ops = []; const ch = room.chess;
    const p = this.run(room, 'ai', { difficulty: 'casual', budgetMs: this.aiBudgetMs }); const plan = p.r && p.r.plan;
    if (p.r.ok && plan && plan.action && plan.action.from) { const a = this.run(room, 'act', { kind: plan.action.kind, from: plan.action.from, to: plan.action.to, promotion: plan.action.promotion || '', steps: plan.action.steps || [] }); if (a.r.ok && a.r.accepted) this.absorb(room, a.q, a.r, ops); else this.fallbackAct(room, ops); } else this.fallbackAct(room, ops);
    if (!ch.over) { const wt = this.run(room, 'startTurn', {}); this.absorb(room, wt.q, wt.r, ops); }
    return ops.length ? this.broadcastOps(room, ops, 'act', { who: 'b' }) : [];
  }
  hazardOps(room) {
    const ops = []; const h = this.run(room, 'roadEvent', { count: 1, label: 'car' });
    if (h.r.ok) this.absorb(room, h.q, h.r, ops); return ops.length ? this.broadcastOps(room, ops, 'hazard', { who: 'b' }) : [];
  }
  chessEnd(c, room) {
    const mem = room.member(c.id); if (mem.slot === 'C') return [this.deny(c.id, 'car-cannot-retract', 'chessEnd')];
    if (!room.chess || !room.chess.active) return [];
    const ch = room.chess; ch.active = false;
    if (ch.persist && !ch.over) { room.saved = room.saved || {}; room.saved[ch.challengeId] = Object.assign({}, ch, { active: false }); }
    return this.all(room, { t: 'chessEnd', by: mem.slot });
  }
  resync(c, room) {
    const ch = room.chess; if (!ch) return [];
    return [{ to: c.id, msg: { t: 'resync', fen: ch.fen, stateJson: ch.stateJson, turn: ch.turn, seq: ch.seq, over: ch.over, winner: ch.winner } }];
  }
  // ---- periodic maintenance (server.cjs calls this every second)
  sweep() {
    const now = this.clock(); const out = [];
    for (const [id, c] of Array.from(this.conns)) if (now - c.seen > 60) { out.push({ to: id, msg: { t: 'deny', reason: 'idle-timeout' } }); out.push({ close: id }); out.push(...this.disconnect(id)); }
    for (const room of Array.from(this.rooms.values())) {
      if (!room.live().length || now - room.touched > 1800) { this.rooms.delete(room.code); continue; }
      const ch = room.chess;
      // an absent car never stalls the ride: after the limit the server plays a casual Black move for it
      if (room.started && room.mode === 'duel' && ch && !ch.over && ch.turn === 'b' && ch.blackSince && now - ch.blackSince > this.carTurnLimit) {
        ch.blackSince = 0; const ops = []; const p = this.run(room, 'ai', { difficulty: 'casual', budgetMs: 120 }); const plan = p.r && p.r.plan;
        if (p.r.ok && plan && plan.action && plan.action.from) { const a = this.run(room, 'act', { kind: plan.action.kind, from: plan.action.from, to: plan.action.to, promotion: plan.action.promotion || '', steps: plan.action.steps || [] }); if (a.r.ok && a.r.accepted) this.absorb(room, a.q, a.r, ops); else this.fallbackAct(room, ops); } else this.fallbackAct(room, ops);
        if (!ch.over) { const wt = this.run(room, 'startTurn', {}); this.absorb(room, wt.q, wt.r, ops); }
        if (ops.length) out.push(...this.broadcastOps(room, ops, 'act', { who: 'b', timeout: true }));
      }
    }
    return out;
  }
}

module.exports = { Hub, Room, Authority, CoopTurn, CAR_ACTIONS, PROTO, flagAllowed, THREAT_KINDS, MAX_MESSAGE };
