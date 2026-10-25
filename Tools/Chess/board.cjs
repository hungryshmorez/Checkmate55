'use strict';
// Pure board primitives for CHECKMATE 55 Road Chess. No chess.js dependency:
// chess.js stays authoritative for ordinary legality; this module only does geometry,
// FEN (de)serialisation and attack tests used by the Road / Cyber layers.
const FILES = 'abcdefgh';
const KNIGHT = [[1, 2], [2, 1], [2, -1], [1, -2], [-1, -2], [-2, -1], [-2, 1], [-1, 2]];
const KING = [[1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1], [0, -1], [1, -1]];
const ORTHO = [[1, 0], [-1, 0], [0, 1], [0, -1]];
const DIAG = [[1, 1], [1, -1], [-1, 1], [-1, -1]];

const idx = s => (s.charCodeAt(1) - 49) * 8 + (s.charCodeAt(0) - 97);
const sq = i => FILES[i & 7] + ((i >> 3) + 1);
const fileOf = i => i & 7;
const rankOf = i => i >> 3;
const onBoard = (f, r) => f >= 0 && f < 8 && r >= 0 && r < 8;
const colorOf = ch => (ch === ch.toUpperCase() ? 'w' : 'b');
const typeOf = ch => ch.toLowerCase();
const make = (type, color) => (color === 'w' ? type.toUpperCase() : type);
const other = c => (c === 'w' ? 'b' : 'w');

function parse(fen) {
  const f = fen.trim().split(/\s+/);
  const cells = new Array(64).fill(null);
  const rows = f[0].split('/');
  if (rows.length !== 8) throw new Error('Invalid FEN');
  for (let r = 0; r < 8; r++) {
    let file = 0;
    for (const ch of rows[r]) {
      if (ch >= '1' && ch <= '8') file += +ch;
      else cells[(7 - r) * 8 + file++] = ch;
    }
    if (file !== 8) throw new Error('Invalid FEN');
  }
  return {
    cells, turn: f[1] || 'w', castling: f[2] && f[2] !== '-' ? f[2] : '',
    ep: f[3] || '-', half: +(f[4] || 0), full: +(f[5] || 1),
  };
}

function toFen(b) {
  const rows = [];
  for (let r = 7; r >= 0; r--) {
    let s = '', e = 0;
    for (let f = 0; f < 8; f++) {
      const ch = b.cells[r * 8 + f];
      if (ch) { if (e) { s += e; e = 0; } s += ch; } else e++;
    }
    if (e) s += e;
    rows.push(s);
  }
  return rows.join('/') + ' ' + b.turn + ' ' + (b.castling || '-') + ' ' + (b.ep || '-') + ' ' + b.half + ' ' + b.full;
}

const clone = b => ({ cells: b.cells.slice(), turn: b.turn, castling: b.castling, ep: b.ep, half: b.half, full: b.full });

function findKing(b, color) {
  const k = make('k', color);
  for (let i = 0; i < 64; i++) if (b.cells[i] === k) return i;
  return -1;
}

// Is square i attacked by any piece of colour `by`?
function isAttacked(b, i, by) {
  const f = fileOf(i), r = rankOf(i), c = b.cells;
  const pr = by === 'w' ? r - 1 : r + 1;
  for (const df of [-1, 1]) if (onBoard(f + df, pr) && c[pr * 8 + f + df] === make('p', by)) return true;
  for (const [df, dr] of KNIGHT) if (onBoard(f + df, r + dr) && c[(r + dr) * 8 + f + df] === make('n', by)) return true;
  for (const [df, dr] of KING) if (onBoard(f + df, r + dr) && c[(r + dr) * 8 + f + df] === make('k', by)) return true;
  for (const [df, dr] of ORTHO) {
    let x = f + df, y = r + dr;
    while (onBoard(x, y)) {
      const p = c[y * 8 + x];
      if (p) { if (colorOf(p) === by && (typeOf(p) === 'r' || typeOf(p) === 'q')) return true; break; }
      x += df; y += dr;
    }
  }
  for (const [df, dr] of DIAG) {
    let x = f + df, y = r + dr;
    while (onBoard(x, y)) {
      const p = c[y * 8 + x];
      if (p) { if (colorOf(p) === by && (typeOf(p) === 'b' || typeOf(p) === 'q')) return true; break; }
      x += df; y += dr;
    }
  }
  return false;
}

function inCheck(b, color) {
  const k = findKing(b, color);
  return k >= 0 && isAttacked(b, k, other(color));
}

// Remove castling rights that no longer match the pieces on their home squares.
function sanitizeCastling(b) {
  let s = '';
  const has = (ch, i) => b.cells[i] === ch;
  if (b.castling.includes('K') && has('K', 4) && has('R', 7)) s += 'K';
  if (b.castling.includes('Q') && has('K', 4) && has('R', 0)) s += 'Q';
  if (b.castling.includes('k') && has('k', 60) && has('r', 63)) s += 'k';
  if (b.castling.includes('q') && has('k', 60) && has('r', 56)) s += 'q';
  b.castling = s;
  return b;
}

function material(b, color, values) {
  let t = 0;
  for (const ch of b.cells) if (ch && colorOf(ch) === color) t += values[typeOf(ch)] || 0;
  return t;
}

module.exports = {
  FILES, KNIGHT, KING, ORTHO, DIAG, idx, sq, fileOf, rankOf, onBoard, colorOf, typeOf, make, other,
  parse, toFen, clone, findKing, isAttacked, inCheck, sanitizeCastling, material,
};
