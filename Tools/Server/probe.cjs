'use strict';
// End-to-end check of a RUNNING room server: two fake players meet, start a co-op match, deploy a board and make a move.
//   node Tools/Server/probe.cjs wss://your-server.example.com        (or ws://localhost:8080)
// Prints PASS and exits 0 on success, otherwise says what failed and exits 1. Run it from the folder that contains node_modules.
const WebSocket = require('ws');
const url = process.argv[2] || 'ws://localhost:8080';
const fail = (m) => { console.error('FAIL: ' + m); process.exit(1); };
setTimeout(() => fail('timed out after 90 s (a sleeping free server can take about a minute to wake; run it again)'), 90000).unref();
const open = (name) => new Promise((ok, bad) => { const ws = new WebSocket(url); const inbox = []; const waiters = [];
  ws.on('message', (d) => { inbox.push(JSON.parse(d.toString())); waiters.splice(0).forEach(w => w()); });
  ws.on('open', () => ok({ name, ws, inbox, send: (m) => ws.send(JSON.stringify(m)),
    async wait(t, ms) { const end = Date.now() + (ms || 15000); for (;;) { const i = inbox.findIndex(m => m.t === t); if (i >= 0) return inbox.splice(i, 1)[0]; if (Date.now() > end) throw new Error(name + ' never received "' + t + '"'); await new Promise(r => { waiters.push(r); setTimeout(r, 100); }); } } }));
  ws.on('error', (e) => bad(new Error('cannot connect to ' + url + ': ' + e.message))); });
(async () => {
  const a = await open('A'), b = await open('B'); console.log('connected to ' + url);
  a.send({ t: 'create', mode: 'coop', coop: 'shared' }); const room = await a.wait('room'); console.log('room ' + room.code + ' created');
  b.send({ t: 'join', code: room.code }); await b.wait('room'); console.log('B joined');
  a.send({ t: 'start' }); await a.wait('start'); await b.wait('start'); console.log('match started');
  const veh = { lane: 1, speedMph: 80, knownMask: -1 };
  a.send({ t: 'chessBegin', challengeId: 'probe', req: Object.assign({ fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1', seed: 1, cfgJson: '{"road":true}' }, veh) }); await a.wait('ops'); await b.wait('ops'); console.log('board deployed on both clients');
  b.send({ t: 'chess', req: Object.assign({ op: 'act', kind: 'move', from: 'e2', to: 'e4' }, veh) }); const m = await a.wait('ops'); if (m.who !== 'w') fail('unexpected reply'); console.log('a move was validated and the car answered');
  console.log('PASS'); process.exit(0);
})().catch(e => fail(e.message));
