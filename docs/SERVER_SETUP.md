# CHECKMATE 55 room server: setup for beginners

You only need this for the two ONLINE modes (Co-op Escape and Driverless Duel). Everything else in the game works without it.
The server is a tiny program that stays switched on and introduces two players to each other and referees their chess. It must live on the internet, because the game page cannot run it.

You will do four things: (1) put these files on GitHub, (2) connect GitHub to Render, (3) test it, (4) give the address to the game. About 15 minutes, no card needed.

## 1. Put these files on GitHub

1. Go to https://github.com/new (sign in first). 
2. Repository name: `checkmate55-rooms`. Choose **Public**. Leave "Add a README", ".gitignore" and "license" **unticked**. Click **Create repository**.
3. On the next page click the link **"uploading an existing file"**.
4. On your computer, unzip `Checkmate55_Server.zip`. Open the unzipped folder and select EVERYTHING inside it (the `Tools` folder, `render.yaml`, `SERVER_SETUP.md`). Drag it all onto the GitHub page. (On an iPad: use "Choose your files" and pick the files; if folders will not upload, use a computer.)
5. Wait until every file is listed, then click **Commit changes**.
6. Check the repo now shows `Tools/`, `render.yaml` and `SERVER_SETUP.md` at its top level.

## 2. Start it on Render

1. Go to https://render.com and click **Get Started**. Choose **Sign in with GitHub** and allow it.
2. Click **New +** (top right) > **Blueprint**.
3. Pick the repo `checkmate55-rooms` (if it is not listed, click "Configure account" and allow Render to see it). 
4. Render reads `render.yaml` and shows a service called **checkmate55-rooms** on the **Free** plan. Click **Apply** (or "Create").
5. Wait 2-4 minutes. When the status says **Live** (green), click the service name and copy its address at the top. It looks like `https://checkmate55-rooms-abcd.onrender.com`.

## 3. Test it

Open `https://YOUR-ADDRESS.onrender.com/health` in a browser. You should see something like `{"ok":true,"proto":1,"conns":0,"rooms":0}`.
If you see that, the server works. (The first visit after it has been idle can take about a minute.)

Even better, a full check that plays two fake players through a co-op match (needs Node on any computer): in the unzipped folder run
`cd Tools/Server && npm install && node probe.cjs wss://YOUR-ADDRESS.onrender.com` - it prints PASS when everything works end to end.

## 4. Give the address to the game

Your game address for the server is the same name, but starting with **wss://** instead of https://, for example:

    wss://checkmate55-rooms-abcd.onrender.com

Tell the Nimble Fox agent exactly this:

    Set the multiplayer server address to wss://YOUR-ADDRESS.onrender.com : put that line in Assets/Resources/server.txt and also as the value of the constant BuiltInServer in Assets/Checkmate55/Scripts/NetClient.cs, then rebuild.

Players can also type the address into the online room screen themselves, or open the game with `?server=wss://YOUR-ADDRESS.onrender.com` added to its link.

## Playing online

1. Player 1 opens Co-op Escape (or Driverless Duel), presses CONNECT, then HOST. A 5-letter room code appears.
2. Player 2 opens the same mode, presses CONNECT, types the code, presses JOIN.
3. Player 1 presses START MATCH.

## Things to know

* **Free plan sleeps** after about 15 minutes with no one connected. The next player waits up to a minute for it to wake. Before a demo or before the judges try it, open the `/health` address yourself to wake it.
* **Keep it awake** (optional): the free service https://uptimerobot.com can load your `/health` address every 5 minutes.
* **Restrict who can connect** (optional): in Render open the service > Environment > add `ALLOWED_ORIGINS` = the address of the page your game is on (for example `https://something.nimblefox.ai`). Leave it empty if you are unsure; the game then works from any page.
* If Render shows an error, open the service's **Logs** tab and send me the last 20 lines.

## Running it on your own computer instead (for testing only)

1. Install Node.js 20 or newer from https://nodejs.org.
2. In a terminal inside the unzipped folder: `cd Tools/Server`, then `npm install`, then `node server.cjs`.
3. It listens on port 8080; open http://localhost:8080/health to check. For other people to reach it you would need a tunnel such as `cloudflared tunnel --url http://localhost:8080`, which prints an https address; use it with wss:// instead.
