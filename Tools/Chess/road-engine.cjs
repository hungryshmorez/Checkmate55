'use strict';
// CHECKMATE 55: OVERRIDE — Road Chess / Cyber Chess rules layer.
//
// chess.js remains the single authority for ordinary legality. This module only adds the
// explicit, temporary vehicle-dependent rules from the design document and never forks the
// board: every position is a plain FEN that chess.js can load. All state that is not part of
// a FEN (lane, speed, shields, EMP, hazards, cards, charges, one-shot rules) lives in `state`,
// which is plain JSON so it can cross the Unity/JS bridge, the network and save files.
const { Chess } = require('./chess.cjs');
const B = require('./board.cjs');

const VALUES = { p: 100, n: 320, b: 330, r: 500, q: 900, k: 0 };
const CARD_IDS = ['pawnBoost', 'glitchSwap', 'systemReboot', 'powerSurge'];
const RULE_IDS = ['shift', 'jump', 'charge', 'overclock', 'castle', 'ghost', 'reversal'];
const LANE_NAMES = ['LEFT', 'CENTER', 'RIGHT'];
const laneOfFile = f => (f <= 2 ? 0 : f <= 4 ? 1 : 2);
const tierOf = mph => (mph >= 110 ? 3 : mph >= 90 ? 2 : mph >= 70 ? 1 : 0);
const clampLane = n => Math.max(0, Math.min(2, n | 0));

const DEFAULT_CFG = {
  road: true, cyber: true, cards: true, carCyber: false, carCards: false,
  reshuffle: false, handMax: 3, startHand: 1, chargeCap: 6,
  costs: { firewall: 1, emp: 2, overdrive: 3 },
  cooldowns: { firewall: 0, emp: 2, overdrive: 3 },
  roadCooldowns: { jump: 0, charge: 0 },
  hazards: true, hazardMaxActive: 3, hazardRate: [0.25, 0.35, 0.5, 0.75],
  autoVehicle: false, highwayShieldTurns: 3, pressure: [], startCharges: 0,
};

function mulberry(state) {
  let t = (state.rng = (state.rng + 0x6D2B79F5) | 0);
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}
const rndInt = (state, n) => Math.floor(mulberry(state) * n);

function newState(opts) {
  opts = opts || {};
  const cfg = Object.assign({}, DEFAULT_CFG, opts.cfg || {});
  cfg.costs = Object.assign({}, DEFAULT_CFG.costs, (opts.cfg || {}).costs);
  cfg.cooldowns = Object.assign({}, DEFAULT_CFG.cooldowns, (opts.cfg || {}).cooldowns);
  cfg.roadCooldowns = Object.assign({}, DEFAULT_CFG.roadCooldowns, (opts.cfg || {}).roadCooldowns);
  const known = { shift: true, jump: true, charge: true, overclock: true, castle: false, ghost: false, reversal: false };
  Object.assign(known, opts.known || {});
  const side = () => ({
    charges: 0, cardPlayed: false, cyberUsed: false, hand: [], boost: '', overdrive: '',
    cd: { firewall: 0, emp: 0, overdrive: 0 }, roadCd: { jump: 0, charge: 0 },
  });
  const state = {
    v: 1, seed: opts.seed | 0, rng: (opts.seed | 0) ^ 0x5bd1e995, side: 'w', phase: 'pre', turnNo: 0,
    lane: clampLane(opts.lane === undefined ? 1 : opts.lane), speed: opts.speed === undefined ? 60 : opts.speed,
    cfg, known, w: side(), b: side(), deck: [], discard: [], shields: [], emp: [], hazards: [],
    used: { highwayCastle: false, ghostPawn: false, kingReversal: false },
    over: false, winner: '', reason: '',
  };
  // Seeded deck: three of each card. Order is deterministic per seed (and so are tests).
  const deck = [];
  for (let i = 0; i < 3; i++) for (const c of CARD_IDS) deck.push(c);
  for (let i = deck.length - 1; i > 0; i--) { const j = rndInt(state, i + 1); const t = deck[i]; deck[i] = deck[j]; deck[j] = t; }
  state.deck = deck;
  return state;
}

const sideOf = (state, c) => (c === 'w' ? state.w : state.b);
const keyOf = a => [a.kind, a.from, a.to, a.promotion || '', (a.steps || []).map(s => s.from + s.to + (s.promotion || '')).join('+')].join('|');
const shieldAt = (state, s) => state.shields.find(x => x.sq === s);
const empAt = (state, s) => state.emp.find(x => x.sq === s);
const hazardAt = (state, s) => state.hazards.find(x => x.sq === s);

function relocate(state, from, to) {
  for (const list of [state.shields, state.emp]) for (const e of list) if (e.sq === from) e.sq = to;
}
function dropAt(state, s) {
  state.shields = state.shields.filter(x => x.sq !== s);
  state.emp = state.emp.filter(x => x.sq !== s);
}
function cloneState(state) { return JSON.parse(JSON.stringify(state)); }

function ev(t, o) { return Object.assign({ t, side: '', a: '', b: '', piece: '', captured: '', n: 0, s: '' }, o || {}); }

// ---------------------------------------------------------------------------------------------
// Move generation
// ---------------------------------------------------------------------------------------------
function stdActionOf(m) {
  return {
    kind: m.flags.includes('k') || m.flags.includes('q') ? 'castle' : 'move', from: m.from, to: m.to,
    piece: m.piece, color: m.color, captured: m.captured || '', promotion: m.promotion || '', san: m.san,
    flags: m.flags, blocked: false, steps: [],
  };
}
function mk(kind, side, piece, from, to, extra) {
  return Object.assign({ kind, from, to, piece, color: side, captured: '', promotion: '', san: '', flags: kind, blocked: false, steps: [] }, extra || {});
}

function chessFor(board, side) {
  const b = B.clone(board);
  b.turn = side;
  if (b.ep !== '-') {
    // chess.js only accepts an en-passant square that belongs to the side to move.
    const r = b.ep[1];
    if ((side === 'w' && r !== '6') || (side === 'b' && r !== '3')) b.ep = '-';
  }
  B.sanitizeCastling(b);
  const c = new Chess();
  if (!c.load(B.toFen(b))) throw new Error('Engine produced an invalid position: ' + B.toFen(b));
  return c;
}

// Pieces of `side` that may use Road specials this turn.
function specialsUsable(state, side, sq) {
  if (side !== 'w' || !state.cfg.road) return false;
  return !empAt(state, sq);
}

function lanePasses(state, sq) {
  // Pieces must start in the active band; at 110+ (Overclock) pawns may also *enter* it.
  return laneOfFile(B.fileOf(B.idx(sq))) === state.lane;
}

// All single-step actions for the piece on square `from`. `std` is a prefetched chess.js move list.
function pieceActions(board, state, side, from, std) {
  const i = B.idx(from), ch = board.cells[i];
  const out = [];
  if (!ch || B.colorOf(ch) !== side) return out;
  const type = B.typeOf(ch);
  const mover = std || chessFor(board, side).moves({ verbose: true });
  for (const m of mover) {
    if (m.from !== from || m.captured === 'k') continue;
    out.push(stdActionOf(m));
  }
  if (!specialsUsable(state, side, from)) return out;
  const tier = tierOf(state.speed), known = state.known, me = sideOf(state, side);
  const f = B.fileOf(i), r = B.rankOf(i), dir = side === 'w' ? 1 : -1;
  const inBand = lanePasses(state, from);
  const safe = nb => !B.inCheck(nb, side);
  const land = (to, extra) => { // build the board after moving piece to `to` (no turn flip, no clocks)
    const nb = B.clone(board); nb.cells[B.idx(to)] = nb.cells[i]; nb.cells[i] = null; nb.turn = side; nb.ep = '-';
    B.sanitizeCastling(nb); return nb;
  };

  if (type === 'p') {
    // LANE SHIFT: one file sideways on the same rank, empty destination, never a capture.
    if (known.shift) {
      for (const df of [-1, 1]) {
        const nf = f + df; if (nf < 0 || nf > 7) continue;
        const to = B.sq(r * 8 + nf);
        if (board.cells[B.idx(to)]) continue;
        const enters = laneOfFile(nf) === state.lane;
        if (!(inBand || (tier >= 3 && enters))) continue;
        if (!safe(land(to))) continue;
        out.push(mk('laneShift', side, 'p', from, to, { san: 'LANE SHIFT' }));
      }
    }
    // GHOST PAWN: once per challenge, forward through ONE occupied square (not a king) onto an empty one.
    if (known.ghost && !state.used.ghostPawn && inBand) {
      const midR = r + dir, toR = r + 2 * dir;
      if (toR >= 0 && toR < 8) {
        const mid = board.cells[midR * 8 + f];
        const dst = B.sq(toR * 8 + f);
        if (mid && B.typeOf(mid) !== 'k' && !board.cells[toR * 8 + f]) {
          const nb = land(dst);
          if (safe(nb)) {
            const last = toR === (side === 'w' ? 7 : 0);
            for (const promo of last ? ['q', 'r', 'b', 'n'] : ['']) out.push(mk('ghostPawn', side, 'p', from, dst, { promotion: promo, san: 'GHOST PAWN' }));
          }
        }
      }
    }
    // PAWN BOOST (card): double step from any rank when both squares are empty.
    if (me.boost === from) {
      const startR = side === 'w' ? 1 : 6;
      const r1 = r + dir, r2 = r + 2 * dir;
      if (r !== startR && r2 >= 0 && r2 < 8 && !board.cells[r1 * 8 + f] && !board.cells[r2 * 8 + f]) {
        const dst = B.sq(r2 * 8 + f);
        if (safe(land(dst))) {
          const last = r2 === (side === 'w' ? 7 : 0);
          for (const promo of last ? ['q', 'r', 'b', 'n'] : ['']) out.push(mk('pawnBoost', side, 'p', from, dst, { promotion: promo, san: 'PAWN BOOST' }));
        }
      }
    }
  }

  if (type === 'n' && known.jump && tier >= 1 && inBand && !(state.cfg.roadCooldowns.jump && me.roadCd.jump > 0)) {
    // ROAD JUMP: an extended L of 3 files + 1 rank, or 1 file + 3 ranks.
    for (const [df, dr] of [[3, 1], [3, -1], [-3, 1], [-3, -1], [1, 3], [1, -3], [-1, 3], [-1, -3]]) {
      const nf = f + df, nr = r + dr; if (!B.onBoard(nf, nr)) continue;
      const to = B.sq(nr * 8 + nf), t = board.cells[nr * 8 + nf];
      if (t && (B.colorOf(t) === side || B.typeOf(t) === 'k')) continue;
      if (!safe(land(to))) continue;
      out.push(mk('roadJump', side, 'n', from, to, { captured: t ? B.typeOf(t) : '', san: 'ROAD JUMP' }));
    }
  }

  if (type === 'r' && known.charge && tier >= 2 && inBand && !(state.cfg.roadCooldowns.charge && me.roadCd.charge > 0)) {
    // LANE CHARGE: straight rook line that passes through exactly ONE friendly piece.
    for (const [df, dr] of B.ORTHO) {
      let x = f + df, y = r + dr, friendly = 0;
      while (B.onBoard(x, y)) {
        const t = board.cells[y * 8 + x];
        if (t) {
          if (B.colorOf(t) !== side) { // enemy: may only be the destination, and only after passing exactly one friend
            if (friendly === 1 && B.typeOf(t) !== 'k') {
              const to = B.sq(y * 8 + x);
              if (safe(land(to))) out.push(mk('laneCharge', side, 'r', from, to, { captured: B.typeOf(t), san: 'LANE CHARGE' }));
            }
            break;
          }
          friendly++;
          if (friendly > 1) break;
        } else if (friendly === 1) {
          const to = B.sq(y * 8 + x);
          if (safe(land(to))) out.push(mk('laneCharge', side, 'r', from, to, { san: 'LANE CHARGE' }));
        }
        x += df; y += dr;
      }
    }
  }
  return out;
}

// KING REVERSAL: the king swaps with one friendly rook. Whole move; never inside Overdrive.
function kingReversals(board, state, side) {
  const out = [];
  if (side !== 'w' || !state.cfg.road || !state.known.reversal || state.used.kingReversal || sideOf(state, side).overdrive) return out; // never combined with Overdrive
  const k = B.findKing(board, side); if (k < 0) return out;
  if (empAt(state, B.sq(k))) return out;
  for (let i = 0; i < 64; i++) {
    if (board.cells[i] !== B.make('r', side)) continue;
    if (empAt(state, B.sq(i))) continue;
    const nb = B.clone(board); nb.cells[k] = nb.cells[i]; nb.cells[i] = B.make('k', side); nb.turn = side; nb.ep = '-'; B.sanitizeCastling(nb);
    if (B.inCheck(nb, side)) continue; // resulting king square may not be under attack
    out.push(mk('kingReversal', side, 'k', B.sq(k), B.sq(i), { san: 'KING REVERSAL' }));
  }
  return out;
}

function captureSquare(board, a) {
  // Where an enemy piece would actually be removed by this single-step action ('' if none).
  const ch = board.cells[B.idx(a.from)];
  if (!ch) return '';
  if (a.kind === 'move' && B.typeOf(ch) === 'p' && a.flags.includes('e')) return a.to[0] + a.from[1];
  return a.captured ? a.to : '';
}

function applyShieldRule(board, state, side, acts) {
  const inCheck = B.inCheck(board, side);
  const out = [];
  for (const a of acts) {
    const cs = captureSquare(board, a);
    if (cs && shieldAt(state, cs)) {
      if (inCheck) continue; // a blocked capture cannot answer a check
      a.blocked = true;
    }
    out.push(a);
  }
  return out;
}

function overdriveSequences(board, state, side, from) {
  // Same piece, up to TWO legal steps, at most one capture, both resulting positions legal.
  const out = [];
  const ch = board.cells[B.idx(from)];
  if (!ch || B.typeOf(ch) === 'k') return out;
  const first = applyShieldRule(board, state, side, pieceActions(board, state, side, from));
  for (const a1 of first) {
    if (a1.blocked || a1.kind === 'castle' || a1.kind === 'kingReversal' || a1.captured === 'k') continue;
    const r1 = applyStep(board, state, side, a1);
    if (!r1) continue;
    const b1 = r1.board; b1.turn = side; b1.ep = '-'; B.sanitizeCastling(b1);
    // shields / EMP follow the moving piece (and a captured piece's marks vanish) for the second step
    const sub = { shields: state.shields.map(x => Object.assign({}, x)), emp: state.emp.map(x => Object.assign({}, x)) };
    const view = Object.assign({}, state, sub);
    if (r1.captured) { view.shields = view.shields.filter(x => x.sq !== r1.capSq); view.emp = view.emp.filter(x => x.sq !== r1.capSq); }
    for (const list of [view.shields, view.emp]) for (const e of list) if (e.sq === a1.from) e.sq = a1.to;
    for (const a2 of applyShieldRule(b1, view, side, pieceActions(b1, view, side, a1.to))) {
      if (a2.kind === 'castle' || a2.kind === 'kingReversal' || a2.captured === 'k') continue;
      if (a1.captured && a2.captured) continue; // maximum one capture across both steps
      if (a2.blocked && a1.captured) continue;
      out.push(mk('overdrive', side, B.typeOf(ch), from, a2.to, {
        captured: a1.captured || a2.captured, san: 'OVERDRIVE',
        steps: [{ from: a1.from, to: a1.to, promotion: a1.promotion || '' }, { from: a2.from, to: a2.to, promotion: a2.promotion || '' }],
      }));
    }
  }
  return out;
}

// Full list of main-phase actions for the side to move.
function listActions(fen, state) {
  const board = B.parse(fen), side = board.turn;
  const c = chessFor(board, side);
  const std = c.moves({ verbose: true });
  let acts = [];
  for (const m of std) { if (m.captured === 'k') continue; acts.push(stdActionOf(m)); }
  if (state.cfg.road && side === 'w') {
    const seen = new Set();
    for (let i = 0; i < 64; i++) {
      const ch = board.cells[i]; if (!ch || B.colorOf(ch) !== side) continue;
      const s = B.sq(i);
      for (const a of pieceActions(board, state, side, s, std)) { if (a.kind === 'move' || a.kind === 'castle') continue; acts.push(a); }
    }
    acts = acts.concat(kingReversals(board, state, side));
  }
  const me = sideOf(state, side);
  if (me.overdrive) acts = acts.concat(overdriveSequences(board, state, side, me.overdrive));
  acts = applyShieldRule(board, state, side, acts);
  const dedup = new Map();
  for (const a of acts) dedup.set(keyOf(a), a);
  return Array.from(dedup.values());
}

// ---------------------------------------------------------------------------------------------
// Applying a single step to a board (pure; bookkeeping of shields/hazards is done by the caller)
// ---------------------------------------------------------------------------------------------
function advanceClocks(nb, side, resetClock) {
  nb.turn = B.other(side); nb.ep = '-';
  nb.half = resetClock ? 0 : nb.half + 1;
  if (side === 'b') nb.full += 1;
}

function applyStep(board, state, side, a) {
  const i = B.idx(a.from), ch = board.cells[i];
  if (!ch) return null;
  const nb = B.clone(board); nb.turn = side;
  const type = B.typeOf(ch);
  if (a.kind === 'move' || a.kind === 'castle') {
    const c = chessFor(board, side);
    const m = c.move({ from: a.from, to: a.to, promotion: a.promotion || 'q' });
    if (!m) return null;
    const out = B.parse(c.fen());
    const capSq = m.captured ? (m.flags.includes('e') ? a.to[0] + a.from[1] : a.to) : '';
    return { board: out, captured: m.captured || '', capSq, san: m.san, castleRook: a.kind === 'castle' ? castleRook(side, a) : null, flags: m.flags };
  }
  const ti = B.idx(a.to);
  const target = board.cells[ti];
  let capSq = '', captured = '';
  if (a.kind === 'kingReversal') {
    nb.cells[i] = target; nb.cells[ti] = ch;
    B.sanitizeCastling(nb); advanceClocks(nb, side, false);
    return { board: nb, captured: '', capSq: '', san: 'KING REVERSAL', swap: true };
  }
  if (target) { if (B.colorOf(target) === side || B.typeOf(target) === 'k') return null; capSq = a.to; captured = B.typeOf(target); }
  nb.cells[ti] = a.promotion ? B.make(a.promotion, side) : ch; nb.cells[i] = null;
  B.sanitizeCastling(nb);
  advanceClocks(nb, side, type === 'p' || !!captured);
  return { board: nb, captured, capSq, san: a.san };
}

function castleRook(side, a) {
  const r = side === 'w' ? '1' : '8';
  return a.to[0] === 'g' ? { from: 'h' + r, to: 'f' + r } : { from: 'a' + r, to: 'd' + r };
}

// ---------------------------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------------------------
function pieceList(board, state) {
  const out = [];
  for (let i = 0; i < 64; i++) {
    const ch = board.cells[i]; if (!ch) continue;
    const s = B.sq(i), h = hazardAt(state, s);
    out.push({ square: s, type: B.typeOf(ch), color: B.colorOf(ch), shield: !!shieldAt(state, s), emp: !!empAt(state, s), hazard: h ? h.phase : '' });
  }
  return out;
}

function terminalCheck(fen, state) {
  const board = B.parse(fen), side = board.turn;
  const acts = listActions(fen, state);
  const check = B.inCheck(board, side);
  if (acts.length === 0) return check ? { over: true, winner: B.other(side), reason: 'checkmate' } : { over: true, winner: '', reason: 'stalemate' };
  const pcs = board.cells.filter(Boolean).map(B.typeOf).filter(t => t !== 'k');
  if (pcs.length === 0 || (pcs.length === 1 && (pcs[0] === 'b' || pcs[0] === 'n'))) return { over: true, winner: '', reason: 'insufficient' };
  if (board.half >= 100) return { over: true, winner: '', reason: 'fifty' };
  return { over: false, winner: '', reason: '' };
}

function startTurn(fen, state, events) {
  const side = state.side, me = sideOf(state, side);
  if (state.over) throw new Error('game over');
  if (state.phase !== 'pre') throw new Error('turn already started');
  if (side === 'w') state.turnNo++;
  me.cardPlayed = false; me.cyberUsed = false; me.boost = ''; me.overdrive = '';
  const tier = tierOf(state.speed);
  for (const k of Object.keys(me.cd)) if (me.cd[k] > 0) me.cd[k]--;
  for (const k of Object.keys(me.roadCd)) { if (me.roadCd[k] > 0) me.roadCd[k] -= (tier >= 3 ? 2 : 1); if (me.roadCd[k] < 0) me.roadCd[k] = 0; }
  const cyberOn = state.cfg.cyber && (side === 'w' || state.cfg.carCyber);
  if (cyberOn) { me.charges = Math.min(state.cfg.chargeCap, me.charges + 1); events.push(ev('charge', { side, n: me.charges })); }
  if (state.cfg.cards && (side === 'w' || state.cfg.carCards)) {
    if (me.hand.length < state.cfg.handMax) {
      if (state.deck.length === 0 && state.cfg.reshuffle && state.discard.length) {
        state.deck = state.discard.splice(0); for (let i = state.deck.length - 1; i > 0; i--) { const j = rndInt(state, i + 1); const t = state.deck[i]; state.deck[i] = state.deck[j]; state.deck[j] = t; }
        events.push(ev('reshuffle', { side }));
      }
      if (state.deck.length) { const c = state.deck.shift(); me.hand.push(c); events.push(ev('draw', { side, s: c })); }
    }
  }
  state.phase = 'main';
  events.push(ev('turnStart', { side, n: state.turnNo, a: LANE_NAMES[state.lane], b: String(Math.round(state.speed)) }));
}

function requireMain(state, side) {
  if (state.over) throw new Error('game over');
  if (state.side !== side || state.phase !== 'main') throw new Error('not ' + side + ' main phase');
}

function playCard(fen, state, req, events) {
  const side = state.side; requireMain(state, side);
  const me = sideOf(state, side);
  if (!state.cfg.cards || (side === 'b' && !state.cfg.carCards)) throw new Error('cards disabled');
  if (me.cardPlayed) throw new Error('one card per turn');
  const at = me.hand.indexOf(req.card);
  if (at < 0) throw new Error('card not in hand');
  let board = B.parse(fen);
  switch (req.card) {
    case 'pawnBoost': {
      const ch = board.cells[B.idx(req.target || 'a1')];
      if (ch !== B.make('p', side)) throw new Error('target must be a friendly pawn');
      me.boost = req.target; break;
    }
    case 'glitchSwap': {
      const a = req.target, b = req.target2;
      if (!a || !b || a === b) throw new Error('two pieces required');
      const ca = board.cells[B.idx(a)], cb = board.cells[B.idx(b)];
      if (!ca || !cb || B.colorOf(ca) !== side || B.colorOf(cb) !== side || B.typeOf(ca) === 'k' || B.typeOf(cb) === 'k') throw new Error('swap two friendly non-king pieces');
      const nb = B.clone(board); nb.cells[B.idx(a)] = cb; nb.cells[B.idx(b)] = ca; nb.ep = '-';
      for (const [ch, to] of [[ca, b], [cb, a]]) if (B.typeOf(ch) === 'p' && (B.rankOf(B.idx(to)) === 0 || B.rankOf(B.idx(to)) === 7)) throw new Error('pawn cannot land on the back rank');
      B.sanitizeCastling(nb);
      if (B.inCheck(nb, side)) throw new Error('swap would leave your king in check');
      if (B.inCheck(nb, B.other(side))) throw new Error('swap would create an illegal position (enemy king in check on your turn)');
      for (const list of [state.shields, state.emp]) for (const e of list) { if (e.sq === a) e.sq = b; else if (e.sq === b) e.sq = a; }
      board = nb; fen = B.toFen(board); events.push(ev('swap', { side, a, b })); break;
    }
    case 'systemReboot': {
      const n = state.hazards.length; state.hazards = []; events.push(ev('reboot', { side, n })); break;
    }
    case 'powerSurge': {
      me.charges = Math.min(state.cfg.chargeCap, me.charges + 2);
      for (const k of Object.keys(me.cd)) if (me.cd[k] > 0) me.cd[k]--;
      for (const k of Object.keys(me.roadCd)) if (me.roadCd[k] > 0) me.roadCd[k]--;
      events.push(ev('charge', { side, n: me.charges })); break;
    }
    default: throw new Error('unknown card');
  }
  me.hand.splice(at, 1); state.discard.push(req.card); me.cardPlayed = true;
  events.push(ev('card', { side, s: req.card, a: req.target || '', b: req.target2 || '' }));
  return fen;
}

function useCyber(fen, state, req, events) {
  const side = state.side; requireMain(state, side);
  const me = sideOf(state, side), cfg = state.cfg;
  if (!cfg.cyber || (side === 'b' && !cfg.carCyber)) throw new Error('cyber disabled');
  if (me.cyberUsed) throw new Error('one cyber ability per turn');
  const ab = req.ability, cost = cfg.costs[ab];
  if (cost === undefined) throw new Error('unknown ability');
  if (me.charges < cost) throw new Error('not enough charge');
  if (me.cd[ab] > 0) throw new Error('ability cooling down');
  const board = B.parse(fen), ti = B.idx(req.target || 'a1'), tc = board.cells[ti];
  if (!tc) throw new Error('empty target');
  if (B.typeOf(tc) === 'k') throw new Error('kings cannot be targeted');
  if (ab === 'firewall') {
    if (B.colorOf(tc) !== side) throw new Error('shield a friendly piece');
    if (empAt(state, req.target)) throw new Error('piece is disabled by EMP');
    if (shieldAt(state, req.target)) throw new Error('already shielded');
    state.shields.push({ sq: req.target, ttl: -1 });
  } else if (ab === 'emp') {
    if (B.colorOf(tc) === side) throw new Error('shock an enemy piece');
    let adjacent = false;
    for (const [df, dr] of B.KING) { const nf = B.fileOf(ti) + df, nr = B.rankOf(ti) + dr; if (B.onBoard(nf, nr) && board.cells[nr * 8 + nf] && B.colorOf(board.cells[nr * 8 + nf]) === side) adjacent = true; }
    if (!adjacent) throw new Error('target must be adjacent to one of your pieces');
    state.shields = state.shields.filter(s => s.sq !== req.target);
    if (!empAt(state, req.target)) state.emp.push({ sq: req.target, owner: B.colorOf(tc) });
  } else if (ab === 'overdrive') {
    if (B.colorOf(tc) !== side) throw new Error('overdrive a friendly piece');
    if (empAt(state, req.target)) throw new Error('piece is disabled by EMP');
    me.overdrive = req.target;
  }
  me.charges -= cost; me.cd[ab] = cfg.cooldowns[ab] || 0; me.cyberUsed = true;
  events.push(ev('cyber', { side, s: ab, a: req.target }));
}

function resolveHazards(board, state, side, events) {
  // Called at the END of a White turn. WARNING -> ACTIVE -> resolve -> CLEAR.
  const due = state.hazards.filter(h => h.resolveTurn <= state.turnNo);
  if (!due.length) return board;
  state.hazards = state.hazards.filter(h => h.resolveTurn > state.turnNo);
  let changed = false;
  for (const h of due) {
    events.push(ev('hazardActive', { a: h.sq }));
    const i = B.idx(h.sq), ch = board.cells[i];
    if (ch && B.typeOf(ch) !== 'k') {
      const sh = shieldAt(state, h.sq);
      if (sh) { state.shields = state.shields.filter(s => s !== sh); events.push(ev('hazardBlocked', { a: h.sq, piece: B.typeOf(ch), side: B.colorOf(ch) })); }
      else {
        const nb = B.clone(board); nb.cells[i] = null;
        // Hazards never expose a king to capture: a piece whose removal would leave the side that just moved in check
        // (its own pinned piece, or an enemy piece shielding a hidden attacker) is spared.
        if (B.inCheck(nb, side)) events.push(ev('hazardSpared', { a: h.sq, piece: B.typeOf(ch), side: B.colorOf(ch) }));
        else { board = nb; changed = true; dropAt(state, h.sq); events.push(ev('hazardDestroyed', { a: h.sq, piece: B.typeOf(ch), side: B.colorOf(ch) })); }
      }
    }
    events.push(ev('hazardClear', { a: h.sq }));
  }
  if (changed) { board.ep = '-'; B.sanitizeCastling(board); }
  return board;
}

function addHazards(fen, state, count, events, label) {
  if (!state.cfg.hazards || count <= 0) return;
  const board = B.parse(fen);
  const room = state.cfg.hazardMaxActive - state.hazards.length;
  count = Math.min(count, room);
  for (let n = 0; n < count; n++) {
    const cand = [];
    for (let i = 0; i < 64; i++) {
      const s = B.sq(i), ch = board.cells[i];
      if (ch && B.typeOf(ch) === 'k') continue;
      if (hazardAt(state, s)) continue;
      let w = laneOfFile(B.fileOf(i)) === state.lane ? 3 : 1;
      if (ch) w *= B.colorOf(ch) === 'w' ? 1.3 : 0.35; // the road is hostile to the passenger
      cand.push([s, w]);
    }
    if (!cand.length) return;
    const total = cand.reduce((a, c) => a + c[1], 0);
    let pick = mulberry(state) * total, chosen = cand[cand.length - 1][0];
    for (const [s, w] of cand) { pick -= w; if (pick <= 0) { chosen = s; break; } }
    state.hazards.push({ sq: chosen, phase: 'WARNING', resolveTurn: state.turnNo + 1 });
    events.push(ev('hazardWarning', { a: chosen, s: label || 'road' }));
  }
}

function hardBrakeSpeed(mph) {
  const t = tierOf(mph);
  const floor = [0, 70, 90, 110][t];
  return Math.min(mph - 20, floor - 1);
}

function vehicleConsequences(state, side, step, events) {
  // Step 9: the vehicle's reaction to the move. HARD BRAKE is a vehicle consequence, not an extra move.
  const cfg = state.cfg;
  if (side === 'w' && step.givesCheck && cfg.road && state.speed >= 90) {
    const from = state.speed; state.speed = Math.max(30, hardBrakeSpeed(state.speed));
    events.push(ev('hardBrake', { n: Math.round(state.speed), s: String(Math.round(from)) }));
  }
  // ROUTE PRESSURE: authored challenge effects attached to chess events (never universal rules).
  for (const p of cfg.pressure || []) {
    let hit = false;
    if (p.on === 'check' && side === 'w' && step.givesCheck) hit = true;
    if (p.on === 'sacrifice' && side === 'b' && step.captured === p.piece) hit = true; // the car takes a passenger piece
    if (!hit) continue;
    if (p.do === 'slow') state.speed = Math.max(30, state.speed - (p.mph || 15));
    else if (p.do === 'brake') state.speed = Math.max(25, state.speed - (p.mph || 30));
    else if (p.do === 'laneChange') state.lane = state.lane === 1 ? (rndInt(state, 2) ? 2 : 0) : 1;
    events.push(ev('pressure', { s: p.do, piece: p.piece || '', n: Math.round(state.speed), a: LANE_NAMES[state.lane] }));
  }
}

function vehicleDrive(fen, state, events) {
  // Black's turn, steps 6-8: the vehicle swerves/brakes/accelerates, then road events create hazard warnings.
  const cfg = state.cfg;
  if (cfg.autoVehicle) {
    const r = mulberry(state);
    if (r < 0.22) { const old = state.lane; state.lane = clampLane(old + (old === 1 ? (rndInt(state, 2) ? 1 : -1) : (old === 0 ? 1 : -1))); if (old !== state.lane) events.push(ev('laneChange', { a: LANE_NAMES[state.lane] })); }
    const ds = (mulberry(state) - 0.45) * 30;
    const prev = state.speed; state.speed = Math.max(40, Math.min(135, state.speed + ds));
    if (tierOf(prev) !== tierOf(state.speed)) events.push(ev('speedTier', { n: tierOf(state.speed), s: String(Math.round(state.speed)) }));
  }
  const tier = tierOf(state.speed);
  if (cfg.hazards && mulberry(state) < cfg.hazardRate[tier]) {
    const major = tier >= 2 && mulberry(state) < (tier >= 3 ? 0.6 : 0.4);
    let count = major ? 2 : 1;
    if (tier >= 3 && major) count += 1; // Road Overclock: one additional warning square from major events
    addHazards(fen, state, count, events, major ? 'swerve' : 'bump');
  }
}

function endOfTurnUpkeep(state, side) {
  state.emp = state.emp.filter(e => e.owner !== side);
  const keep = [];
  for (const s of state.shields) {
    if (s.ttl > 0 && s.owner === side) { s.ttl--; if (s.ttl === 0) continue; }
    keep.push(s);
  }
  state.shields = keep;
}

// Applies the steps of an action to a board, mutating shield / EMP bookkeeping on `state`. Shared by act() and the AI preview.
function runSteps(board, state, side, a, events) {
  const mover = board.cells[B.idx(a.from)];
  const steps = a.kind === 'overdrive' ? a.steps : [a];
  let cur = board, capturedTotal = '', blocked = false, lastSan = a.san, castleRookMove = null;
  for (let n = 0; n < steps.length; n++) {
    let act1 = a;
    if (a.kind === 'overdrive') {
      const st = steps[n];
      act1 = applyShieldRule(cur, state, side, pieceActions(cur, state, side, st.from)).find(o => o.from === st.from && o.to === st.to && (o.promotion || '') === (st.promotion || ''));
      if (!act1) throw new Error('overdrive step became illegal');
    }
    const cs = captureSquare(cur, act1);
    if (cs && shieldAt(state, cs)) { // CAPTURE BLOCKED by Firewall: the attacker bounces, the shield is spent, the turn is used.
      state.shields = state.shields.filter(x => x.sq !== cs);
      events.push(ev('captureBlocked', { side, a: act1.from, b: cs, piece: B.typeOf(cur.cells[B.idx(act1.from)]) }));
      const nb = B.clone(cur); nb.turn = side; advanceClocks(nb, side, false); cur = nb; blocked = true; break;
    }
    const r = applyStep(cur, state, side, act1);
    if (!r) throw new Error('step failed');
    if (r.captured) { capturedTotal = r.captured; dropAt(state, r.capSq); events.push(ev('capture', { side, a: act1.from, b: r.capSq, piece: B.typeOf(cur.cells[B.idx(act1.from)]), captured: r.captured })); }
    relocate(state, act1.from, act1.to);
    if (r.castleRook) { castleRookMove = r.castleRook; relocate(state, r.castleRook.from, r.castleRook.to); }
    if (r.swap) relocate(state, act1.to, act1.from);
    lastSan = r.san || lastSan;
    events.push(ev('move', { side, a: act1.from, b: act1.to, piece: B.typeOf(mover), s: a.kind === 'overdrive' ? 'overdrive' : a.kind }));
    cur = r.board;
    if (n < steps.length - 1) { cur = B.clone(cur); cur.turn = side; cur.ep = '-'; B.sanitizeCastling(cur); }
  }
  return { cur, capturedTotal, blocked, lastSan, castleRookMove };
}

// Side-effect free preview used by AI controllers: resulting FEN + the state with shield bookkeeping applied.
function previewAction(fen, state, a) {
  const st = cloneState(state), side = B.parse(fen).turn;
  const r = runSteps(B.parse(fen), st, side, a, []);
  B.sanitizeCastling(r.cur);
  return { fen: B.toFen(r.cur), blocked: r.blocked, captured: r.capturedTotal, state: st };
}

// Resolve one main-phase action for the side to move: steps 7-11 (white) / 5-9 (black).
function act(fen, state, req, events) {
  const side = state.side; requireMain(state, side);
  const board = B.parse(fen);
  const acts = listActions(fen, state);
  let a = null;
  if (req.kind) {
    const want = keyOf({ kind: req.kind, from: req.from, to: req.to, promotion: req.promotion || '', steps: req.steps || [] });
    a = acts.find(x => keyOf(x) === want);
  } else {
    const want = req.promotion || '';
    a = acts.find(x => x.from === req.from && x.to === req.to && (x.kind === 'move' || x.kind === 'castle') && (x.promotion === want || (!want && (x.promotion === 'q' || !x.promotion))));
    if (!a) a = acts.find(x => x.from === req.from && x.to === req.to && x.kind !== 'overdrive');
  }
  if (!a) { const err = new Error('illegal action'); err.code = classifyLocked(fen, state, req); throw err; }
  const run = runSteps(board, state, side, a, events);
  let cur = run.cur;
  const blocked = run.blocked, capturedTotal = run.capturedTotal, lastSan = run.lastSan, highwayRook = run.castleRookMove;
  const me = sideOf(state, side);
  if (!blocked) {
    if (a.kind === 'ghostPawn') state.used.ghostPawn = true;
    if (a.kind === 'kingReversal') state.used.kingReversal = true;
    if (a.kind === 'roadJump' && state.cfg.roadCooldowns.jump) me.roadCd.jump = state.cfg.roadCooldowns.jump;
    if (a.kind === 'laneCharge' && state.cfg.roadCooldowns.charge) me.roadCd.charge = state.cfg.roadCooldowns.charge;
    // HIGHWAY CASTLE: the castling rook gets a temporary Firewall if it started inside the active lane.
    if (a.kind === 'castle' && side === 'w' && state.cfg.road && state.known.castle && !state.used.highwayCastle && highwayRook) {
      if (laneOfFile(B.fileOf(B.idx(highwayRook.from))) === state.lane) {
        state.used.highwayCastle = true; state.shields.push({ sq: highwayRook.to, ttl: state.cfg.highwayShieldTurns, owner: side });
        events.push(ev('highwayCastle', { side, a: highwayRook.to }));
      }
    }
  }
  me.boost = ''; me.overdrive = '';
  B.sanitizeCastling(cur);
  const oppInCheck = B.inCheck(cur, B.other(side));
  vehicleConsequences(state, side, { givesCheck: oppInCheck && !blocked, captured: blocked ? '' : capturedTotal }, events);
  if (side === 'w') cur = resolveHazards(cur, state, side, events);
  else vehicleDrive(B.toFen(cur), state, events);
  endOfTurnUpkeep(state, side);
  const fenAfter = B.toFen(cur);
  const nowCheck = B.inCheck(cur, B.other(side));
  if (nowCheck) events.push(ev('check', { side: B.other(side) }));
  state.side = B.other(side); state.phase = 'pre';
  const term = terminalCheck(fenAfter, state);
  if (term.over) { state.over = true; state.winner = term.winner; state.reason = term.reason; events.push(ev('gameOver', { s: term.reason, side: term.winner })); }
  return { fen: fenAfter, action: a, san: lastSan, blocked, check: nowCheck };
}

// Which hidden/locked rule (if any) the attempted action belongs to.
function classifyLocked(fen, state, req) {
  const probe = cloneState(state);
  for (const k of RULE_IDS) probe.known[k] = true;
  probe.speed = 120; probe.lane = state.lane;
  probe.used = { highwayCastle: false, ghostPawn: false, kingReversal: false };
  let all;
  try { all = listActions(fen, probe); } catch (e) { return ''; }
  const hit = all.find(x => x.from === req.from && x.to === req.to && x.kind !== 'move' && x.kind !== 'castle');
  if (!hit) return '';
  if (hit.kind === 'kingReversal') return 'locked:reversal';
  if (hit.kind === 'ghostPawn') return 'locked:ghost';
  if (hit.kind === 'laneShift') return state.known.shift ? 'lane' : 'locked:shift';
  if (hit.kind === 'roadJump') return state.known.jump ? (tierOf(state.speed) < 1 ? 'speed:70' : 'lane') : 'locked:jump';
  if (hit.kind === 'laneCharge') return state.known.charge ? (tierOf(state.speed) < 2 ? 'speed:90' : 'lane') : 'locked:charge';
  return '';
}

// ---------------------------------------------------------------------------------------------
// Request handler (JSON in / JSON out). Stateless: fen + stateJson in, fen + stateJson out.
// ---------------------------------------------------------------------------------------------
function applyOverrides(state, q) {
  if (q.lane !== undefined && q.lane >= 0) state.lane = clampLane(q.lane);
  if (q.speedMph !== undefined && q.speedMph >= 0) state.speed = q.speedMph;
  if (q.knownMask !== undefined && q.knownMask >= 0) RULE_IDS.forEach((id, n) => { state.known[id] = !!(q.knownMask & (1 << n)); });
  if (q.cfgJson) Object.assign(state.cfg, JSON.parse(q.cfgJson));
}

function snapshot(fen, state, extra) {
  const board = B.parse(fen);
  let moves = [];
  if (!state.over && state.phase === 'main') moves = listActions(fen, state);
  else if (!state.over && state.phase === 'pre') { const s2 = cloneState(state); s2.phase = 'main'; moves = listActions(fen, s2); }
  const side = board.turn;
  return Object.assign({
    ok: true, error: '', locked: '', fen, turn: side, check: B.inCheck(board, side), mate: state.over && state.reason === 'checkmate',
    draw: state.over && ['stalemate', 'insufficient', 'fifty'].includes(state.reason), stalemate: state.over && state.reason === 'stalemate',
    over: state.over, winner: state.winner, reason: state.reason, accepted: false, move: null,
    moves, pieces: pieceList(board, state), events: [], tier: tierOf(state.speed), lane: state.lane, speed: state.speed,
    state, stateJson: JSON.stringify(state),
  }, extra || {});
}

function begin(q) {
  const state = newState({ seed: q.seed, lane: q.lane, speed: q.speedMph, cfg: q.cfgJson ? JSON.parse(q.cfgJson) : {}, known: q.knownMask >= 0 && q.knownMask !== undefined ? maskToKnown(q.knownMask) : {} });
  const events = [];
  const me = state.w;
  for (let i = 0; i < state.cfg.startHand && state.cfg.cards; i++) { const c = state.deck.shift(); if (c) { me.hand.push(c); events.push(ev('draw', { side: 'w', s: c })); } }
  const side = B.parse(q.fen).turn; state.side = side;
  if (state.cfg.cyber && state.cfg.startCharges) state.w.charges = Math.min(state.cfg.chargeCap, state.cfg.startCharges);
  return snapshot(q.fen, state, { events });
}
function maskToKnown(mask) { const k = {}; RULE_IDS.forEach((id, n) => { k[id] = !!(mask & (1 << n)); }); return k; }

function handle(q) {
  try {
    const op = q.op || 'legacy';
    if (op === 'legacy') return legacy(q);
    if (op === 'begin') return begin(q);
    const state = q.stateJson ? JSON.parse(q.stateJson) : newState({});
    applyOverrides(state, q);
    let fen = q.fen; const events = [];
    switch (op) {
      case 'query': return snapshot(fen, state);
      case 'startTurn': startTurn(fen, state, events); break;
      case 'card': fen = playCard(fen, state, q, events); break;
      case 'cyber': useCyber(fen, state, q, events); break;
      case 'setVehicle': break;
      case 'probe': { // classify an attempted action without playing it
        if (state.phase !== 'main') { const s2 = cloneState(state); s2.phase = 'main'; Object.assign(state, s2); }
        const acts = listActions(fen, state);
        const hit = acts.find(x => x.from === q.from && x.to === q.to);
        return snapshot(fen, state, { locked: hit ? 'legal' : classifyLocked(fen, state, q) });
      }
      case 'roadEvent': {
        if (q.squares) { // authored hazards (tutorial boards): explicit squares, always WARNING first
          for (const sqr of String(q.squares).split(',').filter(Boolean)) { if (!hazardAt(state, sqr) && state.hazards.length < 8) { state.hazards.push({ sq: sqr, phase: 'WARNING', resolveTurn: state.turnNo + 1 }); events.push(ev('hazardWarning', { a: sqr, s: 'authored' })); } }
        } else addHazards(fen, state, q.count | 0, events, q.label || 'road');
        break;
      }
      case 'clearHazards': state.hazards = []; break;
      case 'act': {
        try {
          const r = act(fen, state, q, events);
          const snap = snapshot(r.fen, state, { events, accepted: true, move: { color: B.other(state.side), from: r.action.from, to: r.action.to, piece: r.action.piece, captured: r.action.captured, promotion: r.action.promotion, san: r.san, flags: r.action.kind, kind: r.action.kind } });
          return snap;
        } catch (e) {
          if (e.code !== undefined) return snapshot(fen, state, { ok: true, accepted: false, error: 'illegal', locked: e.code });
          throw e;
        }
      }
      case 'ai': return require('./ai.cjs').plan(handleApi, q, state);
      default: throw new Error('unknown op ' + op);
    }
    return snapshot(fen, state, { events });
  } catch (e) {
    return { ok: false, error: String(e && e.message || e), locked: '', fen: q.fen || '', moves: [], pieces: [], events: [], stateJson: q.stateJson || '', state: null };
  }
}

// Original (pre-OVERRIDE) request shape, kept so older callers and saves keep working.
function legacy(q) {
  const lane = q.lane === undefined ? 1 : q.lane;
  const state = newState({ lane, speed: q.speedMph === undefined ? 60 : q.speedMph, cfg: { cyber: false, cards: false, hazards: false }, known: { shift: false, jump: !!q.jumpKnown, charge: !!q.chargeKnown } });
  state.cfg.road = !!q.roadEnabled;
  state.side = B.parse(q.fen).turn; state.phase = 'main'; state.turnNo = 1;
  const road = !!q.roadEnabled && state.side === 'w';
  let acts = listActions(q.fen, state);
  if (road) {
    // Original rule set: in a Road challenge only the active lane's pieces move (plus the king).
    acts = acts.filter(a => a.piece === 'k' || laneOfFile(B.fileOf(B.idx(a.from))) === lane);
  }
  const reply = snapshot(q.fen, state, { moves: acts });
  reply.moves = acts.map(a => Object.assign({}, a, a.kind === 'roadJump' || a.kind === 'laneCharge' ? { flags: 'road' } : {}));
  if (q.from) {
    const m = reply.moves.find(x => x.from === q.from && x.to === q.to && (!x.promotion || x.promotion === (q.promotion || 'q')));
    if (m) {
      const events = [];
      state.phase = 'main';
      const r = act(q.fen, state, { kind: m.kind, from: m.from, to: m.to, promotion: m.promotion }, events);
      const board = B.parse(r.fen);
      reply.fen = r.fen; reply.turn = board.turn; reply.accepted = true;
      reply.move = { color: 'w', from: m.from, to: m.to, piece: m.piece, captured: m.captured, flags: m.kind === 'move' ? m.flags : 'road', san: m.san };
      reply.check = B.inCheck(board, board.turn); reply.pieces = pieceList(board, state);
      const c = new Chess(); c.load(r.fen); reply.moves = c.moves({ verbose: true }); reply.mate = c.in_checkmate(); reply.draw = c.in_draw();
    }
  }
  return reply;
}

const handleApi = {
  handle, newState, listActions, begin, startTurn, playCard, useCyber, act, snapshot, tierOf, laneOfFile, cloneState,
  pieceActions, applyStep, previewAction, chessFor, hazardAt, shieldAt, empAt, keyOf, VALUES, CARD_IDS, RULE_IDS, hardBrakeSpeed, addHazards, vehicleDrive,
  resolveHazards, terminalCheck, mulberry, rndInt, relocate,
};
module.exports = handleApi;
