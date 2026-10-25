# Putting CHECKMATE 55: OVERRIDE online

The game is a Unity WebGL build: plain static files. Only online Co-op / Duel needs a server, and that is optional.

## A0. The easy route: build in Unity yourself, no secrets (recommended)

1. Unity: **File > Build Settings > WebGL > Build**, into a folder called `Builds/WebGL` inside the project. Check **Player Settings > Publishing > Compression Format = Disabled** first (it already is).
2. Optional check: `node Tools/serve-build.cjs Builds/WebGL 8000` and open http://localhost:8000.
3. Commit and push the *contents* of that folder (`index.html`, `Build/`, `TemplateData/`, ...). `.github/workflows/deploy-prebuilt.yml` publishes it to Pages automatically on every push that changes `Builds/WebGL`.
4. One-time: repository **Settings > Pages > Source: GitHub Actions**, and the repository must be public (free accounts).

## A. The game on GitHub Pages (automatic build on GitHub, needs Unity secrets)

1. Create a GitHub repository and push this project to `main` (the `.gitignore` is already set up).
2. Repository **Settings > Pages > Source: GitHub Actions**.
3. Repository **Settings > Secrets and variables > Actions**: add `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
   (free Personal licence; one-time activation guide: https://game.ci/docs/github/activation).
4. Push. `.github/workflows/deploy-pages.yml` then
   * runs all three test suites (chess engine, room server, game logic),
   * builds the WebGL player with the Unity version in `ProjectSettings/ProjectVersion.txt`,
   * refuses to publish if a file is over Pages' 100 MB limit or the build is compressed (Pages cannot send `Content-Encoding`; the project is already set to *Compression Format: Disabled*),
   * deploys to `https://<you>.github.io/<repo>/`.

## B. Doing it by hand instead

1. Unity: File > Build Settings > WebGL > Build (into a folder such as `Builds/WebGL`). Keep **Player Settings > Publishing > Compression Format = Disabled**.
2. Check it behaves like a static host: `node Tools/serve-build.cjs Builds/WebGL 8000`, open http://localhost:8000. It also lists anything Pages would refuse.
3. Upload the *contents* of the build folder (index.html, Build/, TemplateData/ ...) to a Pages branch, Netlify, Cloudflare Pages, itch.io (HTML5), etc.

## C. The room server (only for online Co-op and Duel)

**Beginner step-by-step guide: `docs/SERVER_SETUP.md`** (also shipped as the small `Checkmate55_Server.zip`).

Put the server's address in `Assets/Resources/server.txt` before building (one `wss://...` line) and every player's lobby is pre-filled with it.


Pages cannot run it (it holds WebSocket connections open). Any host that runs Node or Docker works; Render has a free plan:

1. Render > New > Blueprint > pick the repository (it reads `render.yaml`). Set `ALLOWED_ORIGINS` to your game's address, e.g. `https://you.github.io`.
2. Note the service address, e.g. `checkmate55-rooms.onrender.com`. Players use `wss://checkmate55-rooms.onrender.com` in the lobby, or you link the game with `?server=wss://checkmate55-rooms.onrender.com`.
3. Free instances sleep when idle: the first connection can take about a minute. Wake it (open `https://.../health`) shortly before a demo or judging.
4. Anywhere else: `docker build -f Tools/Server/Dockerfile -t checkmate55-rooms .` or `cd Tools/Server && npm ci && node server.cjs`.

If you do nothing for C, everything except the two online modes still works, and Co-op still has its offline AI partner.

## Radio

The artist stations load from `https://hungryshmorez.github.io/Media/` and need that repo to stay public. YouTube stations need the page to be served over HTTPS and the browser to allow embedded players.

## Not done for you

Nothing here has been run by me: there is no Unity in the environment I work in, so the build, the Action and the Render deployment are untested. The Action's first run is the real test; expect to fix a setting or two.
