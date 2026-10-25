'use strict';
// Serves a Unity WebGL build exactly like a plain static host (GitHub Pages) would: no special headers, no Content-Encoding.
//   node Tools/serve-build.cjs <build folder> [port]        e.g.  node Tools/serve-build.cjs "Builds/WebGL" 8000
// If the game loads here it will load on Pages. It also reports files that Pages would refuse (over 100 MB) and compressed builds.
const http = require('http'), fs = require('fs'), path = require('path');
const root = path.resolve(process.argv[2] || 'Builds/WebGL'), port = parseInt(process.argv[3] || '8000', 10);
const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.css': 'text/css', '.png': 'image/png', '.jpg': 'image/jpeg', '.ico': 'image/x-icon', '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.txt': 'text/plain' };
function audit(dir, out) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name); if (e.isDirectory()) { audit(p, out); continue; }
    const mb = fs.statSync(p).size / 1048576;
    if (mb > 100) out.push('TOO LARGE FOR GITHUB PAGES (' + mb.toFixed(0) + ' MB): ' + path.relative(root, p));
    if (/\.(br|gz|unityweb)$/.test(e.name)) out.push('COMPRESSED (needs Content-Encoding, which Pages cannot send): ' + path.relative(root, p));
  }
  return out;
}
function handler(req, res) {
  let rel = decodeURIComponent(req.url.split('?')[0]); if (rel.endsWith('/')) rel += 'index.html';
  const file = path.normalize(path.join(root, rel));
  if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); res.end('not found'); return; }
  res.writeHead(200, { 'content-type': TYPES[path.extname(file)] || 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
}
module.exports = { handler, audit };
if (require.main === module) {
  if (!fs.existsSync(path.join(root, 'index.html'))) { console.error('No index.html in ' + root + ' - build the WebGL player first (File > Build Settings > WebGL).'); process.exit(1); }
  const problems = audit(root, []); problems.forEach(p => console.log('PROBLEM: ' + p));
  http.createServer(handler).listen(port, () => console.log('Serving ' + root + ' at http://localhost:' + port + (problems.length ? '   (fix the problems above before deploying)' : '   (looks good for a static host)')));
}
