'use strict';
const E = require('../road-engine.cjs');
const B = require('../board.cjs');
const { Chess } = require('../chess.cjs');

// Build a ready-to-play scenario: begin() + startTurn() for the side to move.
function setup(fen, o) {
  o = o || {};
  const cfg = Object.assign({ road: true, cyber: true, cards: false, hazards: false }, o.cfg || {});
  let st = E.newState({ seed: o.seed === undefined ? 7 : o.seed, lane: o.lane === undefined ? 1 : o.lane, speed: o.speed === undefined ? 60 : o.speed, cfg, known: o.known });
  st.side = B.parse(fen).turn;
  if (o.charges !== undefined) E.cloneState; // noop
  const events = [];
  E.startTurn(fen, st, events);
  if (o.charges !== undefined) { st[st.side].charges = o.charges; }
  if (o.hand) st[st.side].hand = o.hand.slice();
  return { fen, state: st, events };
}
function acts(s) { return E.listActions(s.fen, s.state); }
function find(s, kind, from, to) { return acts(s).find(a => a.kind === kind && a.from === from && a.to === to); }
function play(s, from, to, kind, promotion) {
  const events = [];
  const r = E.act(s.fen, s.state, { kind, from, to, promotion }, events);
  s.fen = r.fen; s.events = events; return r;
}
function next(s) { const ev = []; E.startTurn(s.fen, s.state, ev); s.events = s.events.concat(ev); }
function validFen(fen) { const c = new Chess(); return c.load(fen); }
function has(s, from, to, kind) { return !!find(s, kind || 'move', from, to); }
module.exports = { E, B, Chess, setup, acts, find, play, next, validFen, has };
