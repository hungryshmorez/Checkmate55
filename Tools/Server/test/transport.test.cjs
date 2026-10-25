'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const WebSocket = require('ws');
const { createServer } = require('../server.cjs');

function client(port) {
  const ws = new WebSocket('ws://127.0.0.1:' + port); const inbox = []; const waiters = [];
  ws.on('message', (d) => { const m = JSON.parse(d.toString()); inbox.push(m); waiters.splice(0).forEach(w => w()); });
  const api = {
    ws, inbox, send: (m) => ws.send(JSON.stringify(m)),
    async wait(type, ms) {
      const end = Date.now() + (ms || 2000);
      for (;;) { const i = inbox.findIndex(m => m.t === type); if (i >= 0) return inbox.splice(i, 1)[0]; if (Date.now() > end) throw new Error('timeout waiting for ' + type + ' (have ' + inbox.map(m => m.t) + ')'); await new Promise(r => { waiters.push(r); setTimeout(r, 50); }); }
    },
    open: new Promise((ok, bad) => { ws.on('open', ok); ws.on('error', bad); }),
  };
  return api;
}

test('real sockets: two passengers meet, start, and play a validated move end-to-end', async () => {
  const srv = createServer({ hub: { aiBudgetMs: 60 } }); const port = await srv.listen(0, '127.0.0.1');
  try {
    const a = client(port), b = client(port); await Promise.all([a.open, b.open]);
    assert.equal((await a.wait('welcome')).proto, 1); await b.wait('welcome');
    a.send({ t: 'hello', name: 'Ana' }); a.send({ t: 'create', mode: 'coop', coop: 'shared' });
    const room = await a.wait('room'); b.send({ t: 'join', code: room.code }); assert.equal((await b.wait('room')).slot, 'B');
    a.send({ t: 'start' }); assert.equal((await b.wait('start')).seed, (await a.wait('start')).seed);
    a.send({ t: 'chessBegin', challengeId: 'final', req: { fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1', seed: 3, cfgJson: JSON.stringify({ road: true }), lane: 1, speedMph: 80, knownMask: -1 } });
    await a.wait('ops'); await b.wait('ops');
    b.send({ t: 'chess', req: { op: 'act', kind: 'move', from: 'e2', to: 'e4', lane: 1, speedMph: 80, knownMask: -1 } });
    const ops = await a.wait('ops'); assert.equal(ops.who, 'w'); assert.equal(ops.turn, 'w');
    b.ws.close(); const left = await a.wait('left'); assert.equal(left.takeover, 'partner');
    const hr = await fetch('http://127.0.0.1:' + port + '/health'); assert.equal(hr.headers.get('access-control-allow-origin'), '*'); const h = await hr.json(); assert.equal(h.ok, true);
    a.ws.close();
  } finally { await srv.close(); }
});
test('real sockets: binary and oversized frames cannot crash the server', async () => {
  const srv = createServer(); const port = await srv.listen(0, '127.0.0.1');
  try {
    const a = client(port); await a.open; await a.wait('welcome');
    a.ws.send(Buffer.from([1, 2, 3])); a.send({ t: 'ping', at: 5 }); assert.equal((await a.wait('pong')).at, 5);
    a.ws.send('x'.repeat(40000)); await new Promise(r => setTimeout(r, 200));
    const b = client(port); await b.open; await b.wait('welcome'); b.send({ t: 'ping' }); await b.wait('pong');
  } finally { await srv.close(); }
});

test('ALLOWED_ORIGINS: pages from other sites cannot open a socket', async () => {
  const srv = createServer({ origins: 'https://good.example' }); const port = await srv.listen(0, '127.0.0.1');
  try {
    const ok = new WebSocket('ws://127.0.0.1:' + port, { origin: 'https://good.example' }); await new Promise((res, rej) => { ok.on('open', res); ok.on('error', rej); }); ok.close();
    const bad = new WebSocket('ws://127.0.0.1:' + port, { origin: 'https://evil.example' });
    const outcome = await new Promise((res) => { bad.on('open', () => res('open')); bad.on('error', () => res('refused')); });
    assert.equal(outcome, 'refused');
  } finally { await srv.close(); }
});
