'use strict';
// WebSocket transport for the CHECKMATE 55: OVERRIDE room server.  Usage:  node server.cjs [--port 8080] [--host 0.0.0.0]
// All rules live in room.cjs; this file only moves text frames and keeps the room clock ticking.
const http = require('http');
const { WebSocketServer } = require('ws');
const { Hub, PROTO, MAX_MESSAGE } = require('./room.cjs');

function createServer(opts) {
  opts = opts || {};
  const hub = new Hub(opts.hub || {});
  const sockets = new Map(); let nextId = 1;
  const httpServer = http.createServer((req, res) => {
    if (req.url === '/health') { res.writeHead(200, { 'content-type': 'application/json', 'access-control-allow-origin': '*', 'cache-control': 'no-store' }); res.end(JSON.stringify(Object.assign({ ok: true, proto: PROTO }, hub.size()))); return; }
    res.writeHead(404); res.end('CHECKMATE 55 room server. Connect with a WebSocket.');
  });
  // ALLOWED_ORIGINS="https://you.github.io,https://other.example" restricts which pages may open a socket (unset = any page).
  const origins = (opts.origins !== undefined ? opts.origins : (process.env.ALLOWED_ORIGINS || '')).split(',').map(x => x.trim().replace(/\/$/, '')).filter(Boolean);
  const verifyClient = origins.length ? (info) => origins.includes(String(info.origin || '').replace(/\/$/, '')) : undefined;
  const wss = new WebSocketServer({ server: httpServer, maxPayload: MAX_MESSAGE, perMessageDeflate: false, verifyClient });
  const deliver = (out) => {
    for (const o of out) {
      if (o.close) { const s = sockets.get(o.close); if (s) s.close(1000, 'idle'); continue; }
      const s = sockets.get(o.to); if (s && s.readyState === 1) s.send(JSON.stringify(o.msg));
    }
  };
  wss.on('connection', (ws) => {
    const id = 'c' + (nextId++); sockets.set(id, ws);
    deliver(hub.connect(id));
    ws.on('message', (data, isBinary) => { if (isBinary) return; deliver(hub.message(id, data.toString())); });
    const gone = () => { if (sockets.delete(id)) deliver(hub.disconnect(id)); };
    ws.on('close', gone); ws.on('error', gone);
  });
  const timer = setInterval(() => deliver(hub.sweep()), 1000); timer.unref();
  return {
    hub, httpServer, wss,
    listen(port, host) { return new Promise((ok) => httpServer.listen(port, host || '0.0.0.0', () => ok(httpServer.address().port))); },
    close() { clearInterval(timer); for (const s of sockets.values()) s.terminate(); return new Promise((ok) => wss.close(() => httpServer.close(() => ok()))); },
  };
}
module.exports = { createServer };

if (require.main === module) {
  const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > 0 ? process.argv[i + 1] : d; };
  const port = parseInt(arg('port', process.env.PORT || '8080'), 10), host = arg('host', '0.0.0.0');
  createServer().listen(port, host).then((p) => console.log('CHECKMATE 55 room server listening on ' + host + ':' + p + '  (health: /health)'));
}
