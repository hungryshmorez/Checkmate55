# Verification report: CHECKMATE 55: OVERRIDE

Honest status as of this build. "Verified" means a machine ran it and it passed in this environment. There is **no Unity Editor** in the environment
this was written in, so nothing that needs one has been run.

## Verified (automated, passing)

| Area | How | Result |
|---|---|---|
| Standard chess legality | `board.cjs` cross-checked against vendored chess.js on ~7,000 positions; `movegen.cjs` perft-verified and cross-checked | pass |
| Road Chess rules: speed tiers, lanes, Lane Shift, Road Jump (3+1 / 1+3), Lane Charge (exactly one friendly piece), Overclock gives no extra move, Hard Brake, Highway Castle, Ghost Pawn, King Reversal, hazards NORMAL->WARNING->ACTIVE->CLEAR (WARNING lasts one White turn) | `Tools/Chess/test/road.test.cjs`, `fuzz.test.cjs` (~15,000 random plies, invariants) | pass |
| Cyber (+1 charge per White turn, one ability per turn, EMP / Firewall / Overdrive) and the four tactical cards (one per turn) | `cyber.test.cjs` | pass |
| AI (alpha-beta + quiescence, hazard aware, four levels), self-play over the authored final positions | `ai.test.cjs`, `selfplay.test.cjs`, `positions.test.cjs` (8 final FENs validated) | pass |
| The five original (Foundation) security challenges still solve | `story-chess.test.cjs` | pass |
| Generated WebGL plugin runs in an emulated Emscripten environment, and is not stale | `jslib.test.cjs`, `jslib-fresh.test.cjs` | pass (105 chess tests total) |
| Room server: rooms, roles, co-op turn lock (Shared / Alternating / Assist), car SYSTEM AUTHORITY costs + cooldowns, forbidden actions, flag whitelist, host-only messages, disconnect handling (AI takeover), idle sweeps, rate limits, oversize / malformed input | `Tools/Server/test/room.test.cjs`, `transport.test.cjs` (real WebSockets) | pass |
| Network QA: whole matches (Co-op and Duel) played through the protocol by AI clients; both replicas and the server stay bit-identical; 3,000-message fuzz; authority spend bounded over 10 minutes | `Tools/Server/test/sim.test.cjs` | pass (34 server tests total) |
| Game logic shared by all modes: threat director pacing / budgets / cooldowns, contests, story graph (30 beats, every flag has a producer, planned pacing windows), radio / climate / light / phone models, police, vehicle policy, endings priority, records persistence, co-op and authority rules, controller abstraction | `Tools/CSharpTests` | pass (75 tests) |
| C# and JS agree on the numbers that matter (authority costs/cooldowns, co-op operator table) | shared `Tools/Server/test/vectors.json` read by both suites | pass |
| AI passenger micro-protocols (climate pattern, dome-light rhythm, preset-4 isolation, REQUEST STOP, mile marker heading) driven against the **real** puzzle models, with the pattern numbers *parsed from the maintenance-card text* | `AgentTests` | pass |
| All Unity C# compiles against Unity reference assemblies in both `#if` branches (WebGL, and Editor/standalone) | compile check | pass, 0 errors |

## NOT verified (needs you, in Unity)

* **The car's exterior** (`OverrideExterior.cs`): body, wheels, lamps, plate, the swinging rear-left door skin, traffic that steers clear of the car, police kept behind it, and the walk-out camera after an escape ending. Proportions were set from the interior's measurements in the builder script; they have never been rendered. Expect to nudge sizes.
* **The Ride Game in the scene:** the console button, move clock, forced move, quota gate and win/lose endings. The model (clock, nags, quota, material bonus, story gating) is unit tested; the server's persistence is tested; the Unity wiring is compile-checked only.

* Scene visuals and placement of everything built at runtime: radio deck, climate buttons, USB ports, service dock, phone, window tint, sun visors, mile markers, warning text.
* Hand / arm IK, interaction-focus camera feel, FOV changes, audio mix (procedural SFX, car voice, five radio presets), the hidden transmission mix, siren.
* Frame rate and memory in a WebGL build, mobile touch feel, IMGUI layout on small / portrait screens.
* The pacing targets (30-35 min first run, ~10 min optimised) are **planned** from the story graph (par 7.75 min, first run 30.9 min); they have not been measured by a person playing.
* End-to-end behaviour of the AI passenger in the real scene (Car Master / AI Simulation): its logic is tested, the scene integration is not. `CHECKMATE 55 > Override > Run AI Simulation (x8)` runs it and writes `docs/OVERRIDE_SIM.txt`.
* Online play in a browser against a deployed server (the protocol and server are tested; the Unity client `NetClient.cs` and the WebSocket part of `CheckmateBrowser.jslib` are compile-checked only).
* Radio playlists (manifest filtering was checked against the live manifest: Driftwave 63, Shmorez 55, Tanky 41 tracks with the Homer Simpson album excluded) and the YouTube player (`CheckmateBrowser.jslib`) have never run in a browser. Radio streaming inside Unity (URL building, decode, mp3 in WebGL). I confirmed with curl that all five URLs return 200 with `access-control-allow-origin: *`; the download/decode code is compile-checked only.

## Suggested first hour in Unity

1. Play *Calibration Ride* (title menu) end to end. Anything that does not look right is a scene-placement issue, not a rules issue.
2. Run *AI Simulation (x8)*. Read `docs/OVERRIDE_SIM.txt`: it lists the beat times and flags any beat the AI sat on for 90+ seconds.
3. Play Story once manually, checking the radio deck (preset 4, balance left, fader rear), the dock, the climate / light puzzles and the phone.
4. Start the room server and try Co-op with two browser tabs.
