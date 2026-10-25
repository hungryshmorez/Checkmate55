CHECKMATE 55: OVERRIDE - RADIO SONGS
====================================

The car radio has FIVE presets. The game ships with NO music: drop in your own five songs here and name them exactly:

    preset1.mp3   (or .ogg / .wav)
    preset2.mp3
    preset3.mp3
    preset4.mp3   <- the passenger's hidden transmission is mixed into THIS preset (rear-left speaker only)
    preset5.mp3

Unity imports them automatically (Assets/Checkmate55/Editor/OverrideTools.cs applies web-friendly import settings).
Presets with no file simply read "NO SIGNAL" - the game never invents or substitutes music.

Optional: edit stations.txt to give each preset a station name and the song title shown on the dashboard.

Keep the five songs short enough for a browser build (about 2-4 minutes each, 128 kbps) - they are loaded in memory.
Only use music you have the right to publish with your entry.

STATIONS (Assets/Resources/Radio/stations.txt)
----------------------------------------------
There are five radio presets. Out of the box:
  1  DRIFTWAVE STATIC  - everything under music/driftwave/ in your Media repo (63 tracks), shuffled
  2  SHMOREZ           - everything under music/shmorez/ (55 tracks)
  3  TANKY JOHNSON     - everything under music/tanky/ EXCEPT the Homer Simpson - Last Call Lullabies album (41 tracks)
  4, 5                 - for your YouTube playlists (edit stations.txt)

The tracks come from https://hungryshmorez.github.io/Media/manifest.json, which your repo regenerates whenever you add music, so new songs
appear on the radio with no change to the game. To exclude another album, add "; !music/<artist>/<album-folder>/" to that line.

Only one song (plus the next one) is held in memory at a time. A very long file takes a moment to load; keep radio tracks under ~8 minutes.
YouTube stations play through a hidden YouTube player: they work in the WebGL build only, need a connection, and cannot use the balance /
fader knobs (the hidden transmission still works on every station). Videos with embedding disabled are skipped.
A file named preset1..preset5 in this folder overrides a station.
