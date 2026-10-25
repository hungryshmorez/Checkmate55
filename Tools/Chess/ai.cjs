'use strict';
// Chess AI shared by the car (Black), the AI passenger and hint requests.
// It never touches hidden state: it plans from exactly the position, shields, hazards, hand and resources
// that a human controller could see, and every move it returns is executed through the same engine API.
const G = require('./movegen.cjs');
const B = require('./board.cjs');

const VAL = [0, 100, 320, 330, 500, 900, 0];
const MATE = 30000;
const PST = {
  1: [0, 0, 0, 0, 0, 0, 0, 0, 50, 50, 50, 50, 50, 50, 50, 50, 10, 10, 20, 30, 30, 20, 10, 10, 5, 5, 10, 25, 25, 10, 5, 5, 0, 0, 0, 20, 20, 0, 0, 0, 5, -5, -10, 0, 0, -10, -5, 5, 5, 10, 10, -20, -20, 10, 10, 5, 0, 0, 0, 0, 0, 0, 0, 0],
  2: [-50, -40, -30, -30, -30, -30, -40, -50, -40, -20, 0, 0, 0, 0, -20, -40, -30, 0, 10, 15, 15, 10, 0, -30, -30, 5, 15, 20, 20, 15, 5, -30, -30, 0, 15, 20, 20, 15, 0, -30, -30, 5, 10, 15, 15, 10, 5, -30, -40, -20, 0, 5, 5, 0, -20, -40, -50, -40, -30, -30, -30, -30, -40, -50],
  3: [-20, -10, -10, -10, -10, -10, -10, -20, -10, 0, 0, 0, 0, 0, 0, -10, -10, 0, 5, 10, 10, 5, 0, -10, -10, 5, 5, 10, 10, 5, 5, -10, -10, 0, 10, 10, 10, 10, 0, -10, -10, 10, 10, 10, 10, 10, 10, -10, -10, 5, 0, 0, 0, 0, 5, -10, -20, -10, -10, -10, -10, -10, -10, -20],
  4: [0, 0, 0, 0, 0, 0, 0, 0, 5, 10, 10, 10, 10, 10, 10, 5, -5, 0, 0, 0, 0, 0, 0, -5, -5, 0, 0, 0, 0, 0, 0, -5, -5, 0, 0, 0, 0, 0, 0, -5, -5, 0, 0, 0, 0, 0, 0, -5, -5, 0, 0, 0, 0, 0, 0, -5, 0, 0, 0, 5, 5, 0, 0, 0],
  5: [-20, -10, -10, -5, -5, -10, -10, -20, -10, 0, 0, 0, 0, 0, 0, -10, -10, 0, 5, 5, 5, 5, 0, -10, -5, 0, 5, 5, 5, 5, 0, -5, 0, 0, 5, 5, 5, 5, 0, -5, -10, 5, 5, 5, 5, 5, 0, -10, -10, 0, 5, 0, 0, 0, 0, -10, -20, -10, -10, -5, -5, -10, -10, -20],
  6: [-30, -40, -40, -50, -50, -40, -40, -30, -30, -40, -40, -50, -50, -40, -40, -30, -30, -40, -40, -50, -50, -40, -40, -30, -30, -40, -40, -50, -50, -40, -40, -30, -20, -30, -30, -40, -40, -30, -30, -20, -10, -20, -20, -20, -20, -20, -20, -10, 20, 20, 0, 0, 0, 0, 20, 20, 20, 30, 10, 0, 0, 10, 30, 20],
};
const KING_END = [-50, -40, -30, -20, -20, -30, -40, -50, -30, -20, -10, 0, 0, -10, -20, -30, -30, -10, 20, 30, 30, 20, -10, -30, -30, -10, 30, 40, 40, 30, -10, -30, -30, -10, 30, 40, 40, 30, -10, -30, -30, -10, 20, 30, 30, 20, -10, -30, -30, -30, 0, 0, 0, 0, -30, -30, -50, -30, -30, -30, -30, -30, -30, -50];
const PASSED = [0, 5, 10, 20, 35, 60, 100, 0];

const LEVELS = {
  easy: { depth: 1, margin: 140, abilities: false, name: 'EASY' },
  casual: { depth: 1, margin: 140, abilities: false, name: 'CASUAL' },
  normal: { depth: 2, margin: 60, abilities: true, name: 'NORMAL' },
  smart: { depth: 2, margin: 60, abilities: true, name: 'SMART' },
  hard: { depth: 3, margin: 12, abilities: true, name: 'HARD' },
  expert: { depth: 3, margin: 12, abilities: true, name: 'EXPERT' },
  brutal: { depth: 4, margin: 0, abilities: true, name: 'BRUTAL' },
  relentless: { depth: 4, margin: 0, abilities: true, name: 'RELENTLESS' },
};

// Static evaluation from the point of view of the side to move.
function evaluate(pos) {
  const bd = pos.b;
  let score = 0, np = 0;
  const pf = [new Array(8).fill(0), new Array(8).fill(0)], bishops = [0, 0];
  for (let i = 0; i < 64; i++) { const v = bd[i]; if (!v) continue; const t = Math.abs(v); if (t !== 1 && t !== 6) np += VAL[t]; if (t === 1) pf[v > 0 ? 0 : 1][i & 7]++; if (t === 3) bishops[v > 0 ? 0 : 1]++; }
  const endgame = np <= 2600;
  for (let i = 0; i < 64; i++) {
    const v = bd[i]; if (!v) continue;
    const t = Math.abs(v), white = v > 0;
    const idx = white ? ((7 - (i >> 3)) * 8 + (i & 7)) : ((i >> 3) * 8 + (i & 7));
    let s = VAL[t] + (t === 6 && endgame ? KING_END[idx] : PST[t][idx]);
    if (t === 1) {
      const f = i & 7, r = i >> 3, rel = white ? r : 7 - r, me = pf[white ? 0 : 1], op = pf[white ? 1 : 0];
      if (me[f] > 1) s -= 10;
      if ((f === 0 || !me[f - 1]) && (f === 7 || !me[f + 1])) s -= 12;
      let passed = true;
      for (let df = -1; df <= 1 && passed; df++) { const ff = f + df; if (ff < 0 || ff > 7) continue; if (op[ff]) { // an enemy pawn on that file might still be behind us
        for (let rr = r + (white ? 1 : -1); rr >= 0 && rr < 8; rr += white ? 1 : -1) if (bd[rr * 8 + ff] === (white ? -1 : 1)) { passed = false; break; } } }
      if (passed) s += PASSED[rel];
    }
    if (t === 6 && !endgame) { // pawn shield
      const f = i & 7, r = i >> 3, dir = white ? 1 : -1; let sh = 0;
      for (let df = -1; df <= 1; df++) { const ff = f + df, rr = r + dir; if (ff >= 0 && ff < 8 && rr >= 0 && rr < 8 && bd[rr * 8 + ff] === (white ? 1 : -1)) sh++; }
      s += sh * 10;
    }
    score += white ? s : -s;
  }
  if (bishops[0] >= 2) score += 30; if (bishops[1] >= 2) score -= 30;
  return pos.side === 1 ? score : -score;
}

function orderScore(pos, m, ctx, ply) {
  const to = (m >> 6) & 63, from = m & 63, promo = (m >> 12) & 7;
  const cap = pos.b[to], flag = m >> 15;
  let s = 0;
  if (cap || (flag & 1)) s = 10000 + 10 * VAL[Math.abs(cap || 1)] - VAL[Math.abs(pos.b[from])] / 10;
  if (promo) s += 9000 + VAL[promo];
  if (!s) { const k = ctx.killers[ply]; if (k && (k[0] === m || k[1] === m)) s = 5000; else s = ctx.history[from * 64 + to] || 0; }
  return s;
}

function quiesce(pos, alpha, beta, ply, ctx) {
  ctx.nodes++;
  if ((ctx.nodes & 1023) === 0 && ctx.deadline && Date.now() > ctx.deadline) ctx.stop = true;
  if (ctx.stop) return 0;
  const stand = evaluate(pos);
  if (stand >= beta) return beta;
  if (stand > alpha) alpha = stand;
  if (ply > 12) return alpha;
  const moves = G.legal(pos, true);
  moves.sort((a, b) => orderScore(pos, b, ctx, ply) - orderScore(pos, a, ctx, ply));
  for (const m of moves) {
    G.make(pos, m);
    const sc = -quiesce(pos, -beta, -alpha, ply + 1, ctx);
    G.unmake(pos);
    if (ctx.stop) return 0;
    if (sc >= beta) return beta;
    if (sc > alpha) alpha = sc;
  }
  return alpha;
}

function search(pos, depth, alpha, beta, ply, ctx) {
  ctx.nodes++;
  if ((ctx.nodes & 1023) === 0 && ctx.deadline && Date.now() > ctx.deadline) ctx.stop = true;
  if (ctx.stop) return 0;
  if (pos.half >= 100) return 0;
  const chk = G.inCheck(pos, pos.side);
  if (chk && ply < 24) depth++;
  if (depth <= 0) return quiesce(pos, alpha, beta, ply, ctx);
  const moves = G.legal(pos);
  if (!moves.length) return chk ? -MATE + ply : 0;
  moves.sort((a, b) => orderScore(pos, b, ctx, ply) - orderScore(pos, a, ctx, ply));
  let best = -Infinity;
  for (const m of moves) {
    G.make(pos, m);
    const sc = -search(pos, depth - 1, -beta, -alpha, ply + 1, ctx);
    G.unmake(pos);
    if (ctx.stop) return 0;
    if (sc > best) best = sc;
    if (sc > alpha) alpha = sc;
    if (alpha >= beta) {
      if (!pos.b[(m >> 6) & 63]) { const k = ctx.killers[ply] || (ctx.killers[ply] = [0, 0]); if (k[0] !== m) { k[1] = k[0]; k[0] = m; } ctx.history[(m & 63) * 64 + ((m >> 6) & 63)] = (ctx.history[(m & 63) * 64 + ((m >> 6) & 63)] || 0) + depth * depth; }
      break;
    }
  }
  return best;
}

// ---------------------------------------------------------------------------------------------
function lcg(seed) { let s = seed >>> 0; return () => (s = (Math.imul(s, 1664525) + 1013904223) >>> 0) / 4294967296; }

// Material swing from pending hazards, judged on the position after the decision being scored.
function hazardDelta(api, fen, state, side) {
  if (!state.hazards.length) return 0;
  const b = B.parse(fen); let d = 0;
  for (const h of state.hazards) {
    const ch = b.cells[B.idx(h.sq)]; if (!ch || B.typeOf(ch) === 'k') continue;
    if (api.shieldAt(state, h.sq)) continue;
    const v = VAL[{ p: 1, n: 2, b: 3, r: 4, q: 5 }[B.typeOf(ch)]];
    const mine = B.colorOf(ch) === side;
    let wMine, wTheirs;
    if (side === 'w') { const due = h.resolveTurn <= state.turnNo; wMine = due ? 0.95 : 0.25; wTheirs = due ? 0.95 : 0.2; } // White's move is followed immediately by resolution
    else { wMine = 0.9; wTheirs = 0.2; }                                                                                  // Black cannot move again before White's turn ends
    d += mine ? -v * wMine : v * wTheirs;
  }
  return d;
}

function evalActions(api, fen, state, depth, opts) {
  const side = B.parse(fen).turn;
  const acts = api.listActions(fen, state);
  const ctx = { nodes: 0, killers: [], history: {}, deadline: opts.deadline || 0, stop: false };
  const scored = [];
  for (const a of acts) {
    let pv;
    try { pv = api.previewAction(fen, state, a); } catch (e) { continue; }
    const pos = G.fromFen(pv.fen);
    const hasMoves = G.legal(pos).length > 0;
    const s = { a, pv, pos, noMoves: !hasMoves, score: 0, quick: 0 };
    if (!hasMoves) s.quick = s.score = G.inCheck(pos, pos.side) ? MATE : 0;      // mate / stalemate on the board
    else if (pos.half >= 100) s.quick = s.score = 0;
    else s.quick = -evaluate(pos);
    scored.push(s);
  }
  // Order by the quick static score so the alpha-beta root prunes well.
  scored.sort((x, y) => y.quick - x.quick);
  let best = -Infinity;
  for (const s of scored) {
    if (!s.noMoves && s.pos.half < 100) {
      if (depth <= 1) s.score = -quiesce(s.pos, -MATE * 2, MATE * 2, 1, ctx);
      else {
        const alpha = best === -Infinity ? -MATE * 2 : best - opts.margin - 1;
        s.score = -search(s.pos, depth - 1, -MATE * 2, -alpha, 1, ctx);
      }
      if (ctx.stop) s.score = s.quick;
    }
    s.score += hazardDelta(api, s.pv.fen, s.pv.state, side);
    if (s.score > best) best = s.score;
  }
  scored.sort((x, y) => y.score - x.score);
  return { list: scored, nodes: ctx.nodes, stopped: ctx.stop, best: scored.length ? scored[0].score : -MATE };
}

function leastAttacker(pos, sq, by) {
  let best = Infinity; const bd = pos.b;
  for (const j of G.PAWN_ATT[by][sq]) if (bd[j] === by * G.P) best = Math.min(best, VAL[1]);
  for (const j of G.KNIGHT_T[sq]) if (bd[j] === by * G.N) best = Math.min(best, VAL[2]);
  for (const j of G.KING_T[sq]) if (bd[j] === by * G.K) best = Math.min(best, 20000);
  const rays = G.RAYS[sq];
  for (let d = 0; d < 8; d++) {
    const ray = rays[d], diag = d >= 4;
    for (let k = 0; k < ray.length; k++) {
      const v = bd[ray[k]]; if (v === 0) continue;
      if (v * by > 0) { const t = Math.abs(v); if (t === G.Q) best = Math.min(best, VAL[5]); else if (diag ? t === G.BI : t === G.R) best = Math.min(best, VAL[t]); }
      break;
    }
  }
  return best;
}

// Expected material loss on `sq` if the opponent simply takes it (0 .. piece value), including pending hazards.
function exposure(api, fen, state, sq, side) {
  const b = B.parse(fen), ch = b.cells[B.idx(sq)]; if (!ch) return 0;
  const v = VAL[{ p: 1, n: 2, b: 3, r: 4, q: 5, k: 0 }[B.typeOf(ch)]];
  const pos = G.fromFen(fen), me = side === 'w' ? 1 : -1, i = B.idx(sq);
  let e = 0;
  const la = leastAttacker(pos, i, -me);
  if (la < Infinity) { const defended = G.attacked(pos, i, me); e = defended ? (la >= 20000 ? 0 : Math.max(0, v - la)) : v; }
  if (api.hazardAt(state, sq)) e = Math.max(e, v * 0.9);
  return e;
}

function candidates(api, fen, state, side, level) {
  const me = side === 'w' ? state.w : state.b, cfg = state.cfg, out = [];
  const b = B.parse(fen);
  const mine = [], theirs = [];
  for (let i = 0; i < 64; i++) { const ch = b.cells[i]; if (!ch || B.typeOf(ch) === 'k') continue; (B.colorOf(ch) === side ? mine : theirs).push(B.sq(i)); }
  const canCard = cfg.cards && !me.cardPlayed && (side === 'w' || cfg.carCards);
  const canCyber = cfg.cyber && !me.cyberUsed && (side === 'w' || cfg.carCyber);
  if (canCard) for (const c of Array.from(new Set(me.hand))) {
    if (c === 'systemReboot' && state.hazards.length) out.push({ card: { card: c } });
    if (c === 'powerSurge') out.push({ card: { card: c } });
    if (c === 'pawnBoost') for (const s of mine.filter(s => b.cells[B.idx(s)] === B.make('p', side)).slice(0, 8)) out.push({ card: { card: c, target: s } });
    if (c === 'glitchSwap') {
      const risky = mine.filter(s => exposure(api, fen, state, s, side) > 0);
      let n = 0;
      for (const a of risky) for (const o of mine) if (a !== o && n < 24) { out.push({ card: { card: c, target: a, target2: o } }); n++; }
    }
  }
  if (canCyber) {
    for (const ab of ['firewall', 'emp', 'overdrive']) {
      if (me.charges < cfg.costs[ab] || me.cd[ab] > 0) continue;
      if (ab === 'firewall') for (const s of mine.map(s => [s, exposure(api, fen, state, s, side)]).filter(x => x[1] > 0 && !api.shieldAt(state, x[0]) && !api.empAt(state, x[0])).sort((x, y) => y[1] - x[1]).slice(0, 3)) out.push({ cyber: { ability: ab, target: s[0] } });
      if (ab === 'emp') for (const s of theirs.slice(0, 40)) { if (api.shieldAt(state, s) || VAL[{ p: 1, n: 2, b: 3, r: 4, q: 5 }[B.typeOf(b.cells[B.idx(s)])]] >= 500) out.push({ cyber: { ability: ab, target: s } }); }
      if (ab === 'overdrive') for (const s of mine.filter(s => !api.empAt(state, s)).slice(0, 12)) out.push({ cyber: { ability: ab, target: s } });
    }
  }
  void level;
  return out;
}

function applyCandidate(api, fen, state, cand) {
  const st = api.cloneState(state), ev = [];
  let f = fen;
  if (cand.card) f = api.playCard(f, st, cand.card, ev);
  if (cand.cyber) api.useCyber(f, st, cand.cyber, ev);
  return { fen: f, state: st };
}

function plan(api, q, state0) {
  const t0 = Date.now();
  const level = LEVELS[(q.difficulty || 'normal').toLowerCase()] || LEVELS.normal;
  if (state0.over) return { ok: false, error: 'game over' };
  if (state0.phase !== 'main') return { ok: false, error: 'start the turn before planning' };
  const side = state0.side, rng = lcg((state0.rng ^ (state0.turnNo * 2654435761)) >>> 0);
  const deadline = q.budgetMs ? t0 + q.budgetMs : 0;
  let fen = q.fen, state = api.cloneState(state0);
  const chosen = { card: null, cyber: null };
  let totalNodes = 0;
  const baseDepth = Math.max(1, q.depth || level.depth);
  if (level.abilities && !q.noAbilities) {
    // Greedy pre-action screening at a shallow depth: best card first, then best Cyber ability.
    for (const kind of ['card', 'cyber']) {
      const base = evalActions(api, fen, state, 1, { margin: 0 });
      totalNodes += base.nodes;
      let bestGain = 40, pick = null;
      for (const cand of candidates(api, fen, state, side, level).filter(c => c[kind])) {
        let r;
        try { r = applyCandidate(api, fen, state, cand); } catch (e) { continue; }
        const ev = evalActions(api, r.fen, r.state, 1, { margin: 0 });
        totalNodes += ev.nodes;
        let bonus = 0;
        if (cand.cyber && cand.cyber.ability === 'firewall') bonus += exposure(api, fen, state, cand.cyber.target, side) * 0.8;
        if (cand.card && cand.card.card === 'powerSurge') bonus += 25;
        const cost = cand.cyber ? state.cfg.costs[cand.cyber.ability] * 18 : 6;
        const gain = (ev.best + bonus - cost) - base.best;
        if (gain > bestGain) { bestGain = gain; pick = { cand, r }; }
      }
      if (pick) { chosen[kind] = pick.cand[kind]; fen = pick.r.fen; state = pick.r.state; }
    }
  }
  // Final move at full depth (iterative deepening if a time budget exists).
  let result = null;
  for (let d = deadline ? 1 : baseDepth; d <= baseDepth; d++) {
    const r = evalActions(api, fen, state, d, { margin: level.margin, deadline });
    totalNodes += r.nodes;
    if (r.stopped && result) break;
    result = r; result.depth = d;
    if (Math.abs(r.best) > MATE / 2) break;
  }
  if (!result || !result.list.length) return { ok: false, error: 'no legal action' };
  // Difficulty noise: choose among near-best actions.
  const top = result.list.filter(s => s.score >= result.best - level.margin);
  const pickA = top[Math.floor(rng() * top.length)] || result.list[0];
  const a = pickA.a;
  return {
    ok: true, plan: { card: chosen.card, cyber: chosen.cyber, action: { kind: a.kind, from: a.from, to: a.to, promotion: a.promotion || '', steps: a.steps || [] } },
    score: Math.round(pickA.score), depth: result.depth, nodes: totalNodes, ms: Date.now() - t0, level: level.name,
    san: a.san || '', options: top.length,
  };
}

module.exports = { plan, evaluate, search, evalActions, LEVELS, VAL, MATE };
