# CHECKMATE 55: OVERRIDE

The post-Foundation build of CHECKMATE 55 (Unity 6, WebGL). Foundation Prompts 1-3 are untouched: the cabin, seats, windows, glove box,
tray and the five original security challenges still work exactly as before. OVERRIDE is layered on top at runtime.

> **Read `docs/VERIFICATION_REPORT.md` first.** It lists exactly what has been machine-verified (rules engines, server, pure game logic, compile
> checks) and what has *not* been run yet (anything that needs the Unity Editor: scene visuals, hand animation, audio, WebGL performance).

## Open it

1. Open the folder in **Unity 6000.4.11f1** (the version the project was made with). Let it import; new scripts get their `.meta` files automatically.
2. Radio: `Assets/Resources/Radio/stations.txt` defines five stations. Presets 1-3 are Driftwave Static, Shmorez and Tanky Johnson (minus the Homer Simpson album),
   streamed from your GitHub Pages `Media` repo; presets 4-5 are for YouTube playlists. See `Assets/Resources/Radio/README.txt`. The game never invents music.
3. Open `Assets/Scenes/Checkmate55.unity` and press Play. The title menu appears.
4. Menu `CHECKMATE 55 > Override` has shortcuts: *Play Story*, *Play with title menu*, *Run AI Simulation (x8)*, *Clear saved records*.
5. WebGL build: your existing build flow. The WebGL plugins are `Assets/Plugins/WebGL/CheckmateChess.jslib` (generated chess engine),
   `CheckmateBrowser.jslib` (speech, vibration, text prompt, room-server WebSocket, YouTube radio stations: one file).

## Modes

| Mode | What it is |
|---|---|
| **Story: Override** | Human passenger vs the AI car. ~30-35 minutes first run (planned), ~10 minutes (planned) for an optimised run. Five endings. Includes the **Ride Game**: a full game of chess against the car, played across the whole ride (below). |
| **Calibration Ride** | The official 15-step, 8-10 minute training program. No danger. Teaches look/focus/touch, seats, climate, radio, compartments, standard chess, Road Jump, Lane Shift, Cyber, cards, hazards. |
| **Passenger Chess** | Relaxed endless daytime ride, chess in your lap (Standard / Road / Cyber Road rules; four difficulties). |
| **Road Chess** | The vehicle's rules as an arcade game: speed tiers 70/90/110, lanes A-C/D-E/F-H, Lane Shift, Road Jump, Lane Charge, hazards, Cyber, cards. |
| **Co-op Escape** | Two passengers, **one** White side, **one** White action per team turn. Shared / Alternating / Tactical Assist. Online with a room code; offline you get an AI partner (Tactical Assist). |
| **Driverless Duel** | Human passenger vs a **human car**. The car pays for everything from a SYSTEM AUTHORITY meter (no god mode); the server enforces costs and cooldowns. Online only. |
| **Car Master** | You are the car (authority console, sensors, Black's moves) against an AI passenger who plays the physical cabin. |
| **AI Simulation** | AI passenger vs AI car: the whole story plays itself, with x1/x2/x4/x8 speed. Used for QA and spectating. |

## The Ride Game (a full game of chess, not just security checkpoints)

From the first lockdown the car challenges you to a **full game of chess** (Road / Cyber rules, hazards and cards included) on the console tray.
Press **RIDE GAME** on the rear console (or **G**, or the button at the top-left of the screen), make a move, retract the tray, go and solve something,
come back. The position is kept between visits.
* **The car does not wait.** A move clock runs after the lockdown (4:00 early, 2:10 near the end). It nags you, and if you leave it too long the car
  plays a weak move *for you*. Those moves do not count.
* **You must play it.** The collision course will not start until you have played 16 moves of your own (or the game is over).
* **It matters.** Checkmate the car and you escape (CHECKMATE); it checkmates you and the ride ends (DESTINATION). Come out of the game ahead in material and
  you start the final collision-course board with extra Cyber charges (and a second card for a big lead).
* Story setup lets you choose how well the car plays (Easy / Normal / Hard / Brutal). In rooms the game is shared and resumes for both players.

## Story, in brief

Normal ride -> REQUEST STOP -> lockdown -> authenticate (chess) -> glove box (route card, USB, phone, maintenance card) -> diagnostics, window,
the missing knight, the scratched notation, the right-speaker recording -> **service dock puzzle** (seat interlock, climate pattern, dome-light rhythm)
-> hidden transmission on **preset 4** (rear-left speaker only) -> diversion and route override -> phone charging in the *other* USB port,
the mile marker, the call -> police -> Cyber Chess escalation -> **COLLISION COURSE: IMPACT 03:00** on an authored position.
Endings: **Checkmate**, **Cheater** (use a hidden rule and the car panics), **Previous Passenger** (finish Passenger 04's unfinished sequence),
**System Escape** (take the systems back and let the police do the rest), **Destination** (impact).

The car's personality moves Helpful -> Controlling -> Sarcastic -> Angry -> Nervous -> Afraid; it speaks through the browser's speech synthesis
(pitch and rate follow its mood; toggle in the title menu or pause menu).

## Controls

Drag / arrow keys to look, tap / click or Space to touch, Tab switches seat, G = ride game, H = hint, R = recenter, Q/E lean, Esc = pause (NOTES, VOICE, MAIN MENU there).
Looking at a control for a moment narrows the field of view and leans the camera toward it (Interaction Focus); hands reach out physically.

## Publishing

See `docs/DEPLOY.md`: a GitHub Actions workflow builds the WebGL player and publishes it to Pages; `Tools/serve-build.cjs` previews a build exactly as a static host serves it;
the room server (online modes only) ships with a Dockerfile and a Render blueprint.

## Online play (Co-op and Duel)

```
cd Tools/Server
npm install
node server.cjs --port 8080        # health check: http://localhost:8080/health
```
In the game: Co-op Escape or Driverless Duel -> enter the server address (`ws://host:8080`, or `wss://` behind TLS), CONNECT, HOST (share the 5-letter code) or JOIN.
For a WebGL build served over HTTPS the server must be `wss://`. The address can also be passed as `?server=wss://your.host`.
The server is authoritative for rooms, roles, the co-op turn lock, car authority, and every chess action (same engine as the game); clients replay the
exact ops it broadcasts and ask for a resync if their position ever differs. If a partner or the car drops, an AI takes over.

## Tests (run these before you submit)

```
node --test Tools/Chess/test/*.test.cjs      # rules engine, AI, WebGL plugin; 105 tests
cd Tools/Server && npm install && node --test test/*.test.cjs   # room server + network QA; 34 tests
cd Tools/CSharpTests && dotnet run -v q      # pure game logic (threat director, story graph, ride game, AI agent...); 75 tests
node Tools/Chess/build-jslib.js              # regenerate CheckmateChess.jslib after ANY change under Tools/Chess
```

## Where things are

| Path | Contents |
|---|---|
| `Tools/Chess/` | `board.cjs`, `road-engine.cjs` (Road / Cyber / hazards / cards), `movegen.cjs`, `ai.cjs`, vendored chess.js, `bridge.cjs` (Editor), `build-jslib.js` |
| `Tools/Server/` | `room.cjs` (pure authoritative logic), `server.cjs` (WebSocket), tests incl. whole-match simulation and a fuzzer |
| `Tools/CSharpTests/` | tests for `Assets/Checkmate55/Scripts/Core` |
| `Assets/Checkmate55/Scripts/Core/` | Unity-free game logic: story graph, threat director, radio/climate/phone models, police, modes, roles, AI agent |
| `Assets/Checkmate55/Scripts/Override*.cs` | presentation of that logic in the cabin (director, builder, puzzles, threats, police, final, modes, roles, UI, audio) |
| `HandRig.cs` | procedural two-bone IK hands |
| `OverrideExterior.cs` | the car's outside (body, wheels, lamps, plate), no-clip traffic and police, the walk-out after an escape |
| `OverrideRideGame.cs`, `Core/RideGame.cs` | the full-length chess game, its clock, quota and rewards |
| `NetClient.cs`, `Plugins/WebGL/CheckmateBrowser.jslib` | room client |
| `Assets/Checkmate55/Editor/OverrideTools.cs` | menu items + radio import settings |

## Known limits (be aware before the contest)

* Everything that needs the Unity Editor is unverified: hand/arm animation, the new geometry (radio deck, dock, ports, phone, visors, mile markers), audio mix, timing of the pacing targets, WebGL frame rate.
* Online play replicates puzzle progress by flags and shares the chess exactly, but does not show your partner's body; you see their touches as text ("PASSENGER B . CLIMATE").
* Pause is local in online matches (the match keeps running). The car does not move for you in a room (the clock still nags); the quota still applies.
* The old Foundation verification components (`Foundation*Verification.cs`, `RoadSecurityVerification.cs`) test the pre-OVERRIDE flow and are kept only for reference; use *Run AI Simulation* instead.
