'use strict';
// Fast standard-chess move generator used only inside AI search (chess.js is ~100x too slow for that).
// Never used for authority: the Road engine validates every chosen action with chess.js.
const B = require('./board.cjs');

const P = 1, N = 2, BI = 3, R = 4, Q = 5, K = 6;
const WK = 1, WQ = 2, BK = 4, BQ = 8;
const TYPE = { p: P, n: N, b: BI, r: R, q: Q, k: K };
const LETTER = [' ', 'p', 'n', 'b', 'r', 'q', 'k'];

const KNIGHT_T = [], KING_T = [], RAYS = [], PAWN_ATT = { 1: [], '-1': [] };
(function init() {
  for (let i = 0; i < 64; i++) {
    const f = i & 7, r = i >> 3;
    const kn = [], ki = [];
    for (const [df, dr] of B.KNIGHT) if (B.onBoard(f + df, r + dr)) kn.push((r + dr) * 8 + f + df);
    for (const [df, dr] of B.KING) if (B.onBoard(f + df, r + dr)) ki.push((r + dr) * 8 + f + df);
    KNIGHT_T.push(kn); KING_T.push(ki);
    const rays = [];
    for (const [df, dr] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]]) {
      const ray = []; let x = f + df, y = r + dr;
      while (B.onBoard(x, y)) { ray.push(y * 8 + x); x += df; y += dr; }
      rays.push(ray);
    }
    RAYS.push(rays);
    // squares from which a pawn of colour c attacks i
    PAWN_ATT[1].push([]); PAWN_ATT['-1'].push([]);
    for (const df of [-1, 1]) {
      if (B.onBoard(f + df, r - 1)) PAWN_ATT[1][i].push((r - 1) * 8 + f + df);
      if (B.onBoard(f + df, r + 1)) PAWN_ATT['-1'][i].push((r + 1) * 8 + f + df);
    }
  }
})();

function fromFen(fen) {
  const b = B.parse(fen);
  const pos = { b: new Int8Array(64), side: b.turn === 'w' ? 1 : -1, castle: 0, ep: -1, half: b.half, king: [-1, -1], hist: [], ply: 0 };
  for (let i = 0; i < 64; i++) {
    const ch = b.cells[i]; if (!ch) continue;
    const v = TYPE[ch.toLowerCase()]; pos.b[i] = ch === ch.toUpperCase() ? v : -v;
    if (v === K) pos.king[ch === ch.toUpperCase() ? 0 : 1] = i;
  }
  if (b.castling.includes('K')) pos.castle |= WK; if (b.castling.includes('Q')) pos.castle |= WQ;
  if (b.castling.includes('k')) pos.castle |= BK; if (b.castling.includes('q')) pos.castle |= BQ;
  if (b.ep !== '-') pos.ep = B.idx(b.ep);
  return pos;
}

function attacked(pos, sq, by) { // by = 1 (white) or -1 (black)
  const bd = pos.b;
  for (const j of PAWN_ATT[by][sq]) if (bd[j] === by * P) return true;
  for (const j of KNIGHT_T[sq]) if (bd[j] === by * N) return true;
  for (const j of KING_T[sq]) if (bd[j] === by * K) return true;
  const rays = RAYS[sq];
  for (let d = 0; d < 8; d++) {
    const ray = rays[d], diag = d >= 4;
    for (let k = 0; k < ray.length; k++) {
      const v = bd[ray[k]];
      if (v === 0) continue;
      if (v * by > 0) { const t = Math.abs(v); if (t === Q || (diag ? t === BI : t === R)) return true; }
      break;
    }
  }
  return false;
}
const inCheck = (pos, side) => attacked(pos, pos.king[side === 1 ? 0 : 1], -side);

// move: from | to<<6 | promo<<12 | flag<<15   (flag: 1 ep, 2 O-O, 4 O-O-O, 8 double push)
function pseudo(pos, out, capturesOnly) {
  const bd = pos.b, side = pos.side;
  for (let i = 0; i < 64; i++) {
    const v = bd[i]; if (v * side <= 0) continue;
    const t = Math.abs(v);
    if (t === P) {
      const f = i & 7, r = i >> 3, fwd = i + 8 * side, last = side === 1 ? 6 : 1, start = side === 1 ? 1 : 6;
      if (!capturesOnly || r === last) {
        if (bd[fwd] === 0) {
          if (r === last) for (const pr of [Q, R, BI, N]) out.push(i | (fwd << 6) | (pr << 12));
          else {
            out.push(i | (fwd << 6));
            if (r === start && bd[fwd + 8 * side] === 0) out.push(i | ((fwd + 8 * side) << 6) | (8 << 15));
          }
        }
      }
      for (const df of [-1, 1]) {
        const nf = f + df; if (nf < 0 || nf > 7) continue;
        const to = fwd + df, tv = bd[to];
        if (tv * side < 0) {
          if (r === last) for (const pr of [Q, R, BI, N]) out.push(i | (to << 6) | (pr << 12));
          else out.push(i | (to << 6));
        } else if (to === pos.ep && tv === 0) out.push(i | (to << 6) | (1 << 15));
      }
    } else if (t === N || t === K) {
      for (const to of (t === N ? KNIGHT_T : KING_T)[i]) { const tv = bd[to]; if (tv * side > 0) continue; if (capturesOnly && tv === 0) continue; out.push(i | (to << 6)); }
      if (t === K && !capturesOnly) {
        const home = side === 1 ? 4 : 60;
        if (i === home && !attacked(pos, i, -side)) {
          const ks = side === 1 ? WK : BK, qs = side === 1 ? WQ : BQ;
          if ((pos.castle & ks) && bd[home + 1] === 0 && bd[home + 2] === 0 && bd[home + 3] === side * R && !attacked(pos, home + 1, -side) && !attacked(pos, home + 2, -side)) out.push(i | ((home + 2) << 6) | (2 << 15));
          if ((pos.castle & qs) && bd[home - 1] === 0 && bd[home - 2] === 0 && bd[home - 3] === 0 && bd[home - 4] === side * R && !attacked(pos, home - 1, -side) && !attacked(pos, home - 2, -side)) out.push(i | ((home - 2) << 6) | (4 << 15));
        }
      }
    } else {
      const rays = RAYS[i];
      const from = t === R ? 0 : t === BI ? 4 : 0, to = t === R ? 4 : t === BI ? 8 : 8;
      for (let d = from; d < to; d++) {
        const ray = rays[d];
        for (let k = 0; k < ray.length; k++) {
          const sq = ray[k], tv = bd[sq];
          if (tv === 0) { if (!capturesOnly) out.push(i | (sq << 6)); continue; }
          if (tv * side < 0) out.push(i | (sq << 6));
          break;
        }
      }
    }
  }
}

function make(pos, m) {
  const bd = pos.b, from = m & 63, to = (m >> 6) & 63, promo = (m >> 12) & 7, flag = m >> 15, side = pos.side;
  const moved = bd[from];
  let captured = bd[to], capSq = to;
  const h = { m, captured: 0, capSq: -1, castle: pos.castle, ep: pos.ep, half: pos.half, moved };
  if (flag & 1) { capSq = to - 8 * side; captured = bd[capSq]; bd[capSq] = 0; }
  h.captured = captured; h.capSq = capSq;
  bd[to] = promo ? side * promo : moved; bd[from] = 0;
  if (Math.abs(moved) === K) {
    pos.king[side === 1 ? 0 : 1] = to;
    if (flag & 2) { bd[to - 1] = bd[to + 1]; bd[to + 1] = 0; }
    if (flag & 4) { bd[to + 1] = bd[to - 2]; bd[to - 2] = 0; }
    pos.castle &= side === 1 ? ~(WK | WQ) : ~(BK | BQ);
  }
  if (from === 0 || to === 0) pos.castle &= ~WQ; if (from === 7 || to === 7) pos.castle &= ~WK;
  if (from === 56 || to === 56) pos.castle &= ~BQ; if (from === 63 || to === 63) pos.castle &= ~BK;
  pos.ep = (flag & 8) ? from + 8 * side : -1;
  pos.half = (Math.abs(moved) === P || captured) ? 0 : pos.half + 1;
  pos.side = -side; pos.hist.push(h);
  return h;
}
function unmake(pos) {
  const h = pos.hist.pop(), m = h.m, bd = pos.b, from = m & 63, to = (m >> 6) & 63, flag = m >> 15;
  pos.side = -pos.side; const side = pos.side;
  bd[from] = h.moved; bd[to] = 0;
  if (flag & 1) bd[h.capSq] = h.captured; else bd[to] = h.captured;
  if (Math.abs(h.moved) === K) {
    pos.king[side === 1 ? 0 : 1] = from;
    if (flag & 2) { bd[to + 1] = bd[to - 1]; bd[to - 1] = 0; }
    if (flag & 4) { bd[to - 2] = bd[to + 1]; bd[to + 1] = 0; }
  }
  pos.castle = h.castle; pos.ep = h.ep; pos.half = h.half;
}

function legal(pos, capturesOnly) {
  const pm = []; pseudo(pos, pm, capturesOnly);
  const out = [], side = pos.side;
  for (const m of pm) { make(pos, m); if (!attacked(pos, pos.king[side === 1 ? 0 : 1], -side)) out.push(m); unmake(pos); }
  return out;
}
function perft(pos, depth) {
  if (depth === 0) return 1;
  let n = 0;
  for (const m of legal(pos)) { make(pos, m); n += perft(pos, depth - 1); unmake(pos); }
  return n;
}
const sqName = i => B.sq(i);
function toObj(m) { const pr = (m >> 12) & 7; return { from: sqName(m & 63), to: sqName((m >> 6) & 63), promotion: pr ? LETTER[pr] : '' }; }
function toFen(pos, fenOrig) { // only the placement/turn are needed by callers that round-trip through chess.js
  const cells = new Array(64).fill(null);
  for (let i = 0; i < 64; i++) { const v = pos.b[i]; if (v) cells[i] = B.make(LETTER[Math.abs(v)], v > 0 ? 'w' : 'b'); }
  let c = ''; if (pos.castle & WK) c += 'K'; if (pos.castle & WQ) c += 'Q'; if (pos.castle & BK) c += 'k'; if (pos.castle & BQ) c += 'q';
  return B.toFen({ cells, turn: pos.side === 1 ? 'w' : 'b', castling: c, ep: pos.ep >= 0 ? sqName(pos.ep) : '-', half: pos.half, full: 1 });
}

module.exports = { P, N, BI, R, Q, K, KNIGHT_T, KING_T, RAYS, PAWN_ATT, fromFen, attacked, inCheck, pseudo, make, unmake, legal, perft, toObj, toFen, sqName, LETTER };
