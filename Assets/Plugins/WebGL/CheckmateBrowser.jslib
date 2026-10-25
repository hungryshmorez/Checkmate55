// CHECKMATE 55: OVERRIDE - all browser-side plugin functions in ONE file (browser helpers, room-server WebSocket, YouTube radio).
// (They used to live in three files; some project importers drop new .jslib files, so everything is here.)
mergeInto(LibraryManager.library, {

 CM55_IsCoarsePointer: function() { return window.matchMedia('(pointer:coarse)').matches ? 1 : 0; },
 // The car's voice: the browser speech synthesiser, tuned per mood. Never throws.
 CM55_Speak: function(text, pitch, rate, volume) {
  try {
   if (!window.speechSynthesis) return;
   var s = UTF8ToString(text);
   window.speechSynthesis.cancel();
   var u = new SpeechSynthesisUtterance(s);
   u.pitch = Math.max(0, Math.min(2, pitch)); u.rate = Math.max(0.1, Math.min(2, rate)); u.volume = Math.max(0, Math.min(1, volume));
   window.speechSynthesis.speak(u);
  } catch (e) {}
 },
 CM55_SpeakStop: function() { try { if (window.speechSynthesis) window.speechSynthesis.cancel(); } catch (e) {} },
 // Phones: IMGUI text fields do not raise the on-screen keyboard reliably, so text entry uses the browser's own prompt.
 CM55_Prompt: function(message, current) {
  try {
   var cur = UTF8ToString(current);
   var r = window.prompt(UTF8ToString(message), cur);
   if (r === null || r === undefined) r = cur;
   var size = lengthBytesUTF8(r) + 1;
   var ptr = _malloc(size);
   stringToUTF8(r, ptr, size);
   return ptr;
  } catch (e) { return 0; }
 },
 CM55_PromptFree: function(ptr) { if (ptr) _free(ptr); },
 CM55_Vibrate: function(ms) { try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {} },

 // A tiny polled WebSocket: C# calls Open/Send/Poll each frame. No callbacks into Unity, so nothing can fire at a bad time.
 CM55_NetOpen: function(url) {
  try {
   if (window.__cm55net && window.__cm55net.ws) { try { window.__cm55net.ws.close(); } catch (e0) {} }
   var st = { ws: null, q: [], state: 1, error: '' };
   window.__cm55net = st;
   // If the hosting page's security policy (connect-src) forbids the address, the browser reports it here: far more useful than "disconnected".
   var onCsp = function(ev) { try { st.error = 'blocked by the hosting page security policy (' + ev.violatedDirective + ') for ' + ev.blockedURI; } catch (e9) {} };
   document.addEventListener('securitypolicyviolation', onCsp);
   var ws = new WebSocket(UTF8ToString(url));
   st.ws = ws;
   ws.onopen = function() { st.state = 2; };
   ws.onmessage = function(ev) { if (typeof ev.data === 'string' && st.q.length < 2000) st.q.push(ev.data); };
   ws.onerror = function() { if (!st.error) st.error = 'the browser could not open the connection (server asleep, wrong address, or blocked by the host page)'; };
   ws.onclose = function() { st.state = 0; };
  } catch (e) { window.__cm55net = { ws: null, q: [], state: 0, error: String(e) }; }
 },
 CM55_NetState: function() { var st = window.__cm55net; return st ? st.state : 0; },
 CM55_NetError: function() {
  var st = window.__cm55net;
  var s = st && st.error ? st.error : '';
  var size = lengthBytesUTF8(s) + 1;
  var ptr = _malloc(size);
  stringToUTF8(s, ptr, size);
  return ptr;
 },
 CM55_NetSend: function(text) {
  var st = window.__cm55net;
  if (!st || !st.ws || st.state !== 2) return 0;
  try { st.ws.send(UTF8ToString(text)); return 1; } catch (e) { return 0; }
 },
 CM55_NetPoll: function() {
  var st = window.__cm55net;
  if (!st || st.q.length === 0) return 0;
  var s = st.q.shift();
  var size = lengthBytesUTF8(s) + 1;
  var ptr = _malloc(size);
  stringToUTF8(s, ptr, size);
  return ptr;
 },
 CM55_NetFree: function(ptr) { if (ptr) _free(ptr); },
 CM55_NetClose: function() {
  var st = window.__cm55net;
  if (st && st.ws) { try { st.ws.close(); } catch (e) {} }
  window.__cm55net = null;
 },

 // A hidden YouTube IFrame player used for radio stations that are YouTube playlists. YouTube's audio cannot be read by the game, so these
 // stations support play / pause / volume only (no balance or fader). The player is kept off-screen but full size, as YouTube requires.
 CM55_YtPlay: function(listId) {
  try {
   var id = UTF8ToString(listId);
   var yt = window.__cm55yt;
   if (!yt) { yt = window.__cm55yt = { player: null, ready: false, pending: null, state: -1, title: '', volume: 80, error: 0, current: '' }; }
   yt.pending = id;
   function start() {
    if (!yt.ready || !yt.pending) return;
    var want = yt.pending; yt.pending = null;
    if (yt.current === want) { try { yt.player.playVideo(); } catch (e0) {} return; }
    yt.current = want;
    try {
     yt.player.loadPlaylist({ list: want, listType: 'playlist', index: Math.floor(Math.random() * 20) });
     yt.player.setShuffle(true);
     yt.player.setVolume(yt.volume);
    } catch (e1) { yt.error = 1; }
   }
   if (yt.player) { start(); return; }
   if (!document.getElementById('cm55yt-host')) {
    var host = document.createElement('div');
    host.id = 'cm55yt-host';
    host.style.cssText = 'position:fixed;left:-10000px;top:0;width:220px;height:220px;pointer-events:none;';
    var inner = document.createElement('div'); inner.id = 'cm55yt'; host.appendChild(inner);
    document.body.appendChild(host);
   }
   function create() {
    yt.player = new YT.Player('cm55yt', {
     height: '220', width: '220',
     playerVars: { controls: 0, playsinline: 1, rel: 0, modestbranding: 1, disablekb: 1, fs: 0 },
     events: {
      onReady: function() { yt.ready = true; start(); },
      onStateChange: function(ev) {
       yt.state = ev.data;
       try { var vd = yt.player.getVideoData(); if (vd && vd.title) yt.title = vd.title; } catch (e2) {}
      },
      onError: function() { yt.error = 1; try { yt.player.nextVideo(); } catch (e3) {} }
     }
    });
   }
   if (window.YT && window.YT.Player) { create(); return; }
   var prev = window.onYouTubeIframeAPIReady;
   window.onYouTubeIframeAPIReady = function() { if (prev) prev(); create(); };
   if (!document.getElementById('cm55yt-api')) {
    var s = document.createElement('script'); s.id = 'cm55yt-api'; s.src = 'https://www.youtube.com/iframe_api';
    s.onerror = function() { yt.error = 2; };
    document.head.appendChild(s);
   }
  } catch (e) {}
 },
 CM55_YtPause: function() { try { var yt = window.__cm55yt; if (yt && yt.ready && yt.player) yt.player.pauseVideo(); if (yt) yt.pending = null; } catch (e) {} },
 CM55_YtVolume: function(v) { try { var yt = window.__cm55yt; if (!yt) return; yt.volume = Math.max(0, Math.min(100, v | 0)); if (yt.ready && yt.player) yt.player.setVolume(yt.volume); } catch (e) {} },
 // 0 = unavailable / loading, 1 = playing, 2 = paused / buffering, 3 = failed (blocked, embedding disabled, offline)
 CM55_YtStatus: function() {
  var yt = window.__cm55yt;
  if (!yt) return 0;
  if (yt.error) return 3;
  if (!yt.ready) return 0;
  return yt.state === 1 ? 1 : (yt.state === 2 || yt.state === 3 || yt.state === 5) ? 2 : 0;
 },
 CM55_YtTitle: function() {
  var yt = window.__cm55yt;
  var s = yt && yt.title ? yt.title : '';
  var size = lengthBytesUTF8(s) + 1;
  var ptr = _malloc(size);
  stringToUTF8(s, ptr, size);
  return ptr;
 },
 CM55_YtFree: function(ptr) { if (ptr) _free(ptr); }
});
