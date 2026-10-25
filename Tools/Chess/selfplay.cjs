'use strict';
// Plays complete games through the same engine API a human or networked client uses (startTurn / card / cyber / act),
// with AI controllers on both sides. Used for AI-vs-AI simulation mode, balance checks and desync hunting.
const E = require('./road-engine.cjs');
const B = require('./board.cjs');
const AI = require('./ai.cjs');

function playGame(fen, o) {
  o = o || {};
  const cfg = Object.assign({ road: true, cyber: true, cards: true, hazards: true, carCyber: true, carCards: false, autoVehicle: true, startHand: 1, pressure: [
    { on: 'check', do: 'slow', mph: 15 }, { on: 'sacrifice', piece: 'r', do: 'brake', mph: 25 }, { on: 'sacrifice', piece: 'n', do: 'laneChange' } ] }, o.cfg || {});
  const state = E.newState({ seed: o.seed || 1, lane: 1, speed: o.speed || 85, cfg, known: Object.assign({ shift: true, jump: true, charge: true, overclock: true, castle: true, ghost: true, reversal: !!o.reversal }, o.known || {}) });
  state.side = B.parse(fen).turn;
  const start = state.cfg.cards ? state.cfg.startHand : 0;
  for (let i = 0; i < start; i++) state.w.hand.push(state.deck.shift());
  let cur = fen; const stats = { plies: 0, cards: 0, cyber: 0, specials: 0, blocked: 0, hazardsLost: { w: 0, b: 0 }, hardBrakes: 0, kinds: {} };
  const levels = { w: o.white || 'normal', b: o.black || 'hard' };
  const maxPlies = o.maxPlies || 120;
  while (!state.over && stats.plies < maxPlies) {
    const side = state.side, ev = [];
    E.startTurn(cur, state, ev);
    const plan = AI.plan(E, { fen: cur, difficulty: levels[side], budgetMs: o.budgetMs || 0, noAbilities: !!(o.noAbilities && o.noAbilities[side]) }, state);
    if (!plan.ok) throw new Error('AI could not plan: ' + JSON.stringify(plan) + ' ' + cur);
    if (plan.plan.card) { cur = E.playCard(cur, state, plan.plan.card, ev); stats.cards++; }
    if (plan.plan.cyber) { E.useCyber(cur, state, plan.plan.cyber, ev); stats.cyber++; }
    const r = E.act(cur, state, plan.plan.action, ev); cur = r.fen; stats.plies++;
    stats.kinds[r.action.kind] = (stats.kinds[r.action.kind] || 0) + 1; if (r.action.kind !== 'move' && r.action.kind !== 'castle') stats.specials++;
    if (r.blocked) stats.blocked++;
    for (const e of ev) { if (e.t === 'hazardDestroyed') stats.hazardsLost[e.side]++; if (e.t === 'hardBrake') stats.hardBrakes++; }
    if (o.onPly) o.onPly({ side, fen: cur, state, events: ev, plan });
  }
  return { fen: cur, over: state.over, winner: state.winner, reason: state.reason || (stats.plies >= maxPlies ? 'ply-cap' : ''), stats, state };
}
module.exports = { playGame };
if (require.main === module) {
  const fens = require('fs').readFileSync(require('path').resolve(__dirname, '../../Assets/Checkmate55/Scripts/Core/Notes.cs'), 'utf8').match(/"[rnbqkpRNBQKP1-8\/]+ w[^"]*"/g).map(s => s.slice(1, -1));
  const tally = { w: 0, b: 0, draw: 0, cap: 0 }; const per = [];
  fens.forEach((fen, i) => {
    for (let g = 0; g < 2; g++) {
      const r = playGame(fen, { seed: 100 + i * 7 + g, white: process.argv[2] || 'normal', black: process.argv[3] || 'hard', maxPlies: 70 });
      const key = r.winner || (r.over ? 'draw' : 'cap'); tally[key]++; per.push([i, g, r.winner || r.reason, r.stats.plies, r.stats.specials, r.stats.hazardsLost.w + '/' + r.stats.hazardsLost.b]);
    }
  });
  console.log(per.map(p => p.join(' ')).join('\n')); console.log('TALLY', JSON.stringify(tally));
}
