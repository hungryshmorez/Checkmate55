'use strict';
// Editor / CI adapter: reads one JSON request on stdin, writes one JSON reply on stdout.
// ChessEngine.Query() spawns this with `node Tools/Chess/bridge.cjs` when running inside the Unity Editor.
const engine = require('./road-engine.cjs');
let input = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', d => { input += d; });
process.stdin.on('end', () => {
  try { process.stdout.write(JSON.stringify(engine.handle(JSON.parse(input)))); }
  catch (e) { process.stdout.write(JSON.stringify({ error: String(e && e.message || e) })); }
});
