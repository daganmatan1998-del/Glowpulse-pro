# Glowpulse-pro

J.A.R.V.I.S. — the voice assistant, in the two halves it actually ships as.

| Path | What it is |
|---|---|
| `jarvis-worker.js` | The Cloudflare Worker backend. Every secret lives here; the browser never sees one. Model proxy with fallback keys, `/tts`, `/stt`, `/image`, Shopify, Google Calendar, remote MCP proxy. |
| `jarvis-desktop/` | The Tauri desktop app — the same page as a floating always-on-top orb on `Ctrl+Shift+Space`. See [its README](jarvis-desktop/README.md) for setup and for what the orb can and cannot do. |
| `jarvis-desktop/dist/index.html` | The whole frontend, one file, no bundler. This is also what you host on the web. |
| `jarvis-desktop/mic-test.html` | A standalone page that measures the exact RMS level the voice detector thresholds against, so "he cannot hear me" becomes a number instead of a guess. |

The two halves are deployed separately and do not need each other to build: the
worker goes up with `wrangler deploy`, the desktop app with `npm run build`
inside `jarvis-desktop/`.

## Deploying the worker

```bash
wrangler deploy jarvis-worker.js
```

The secrets it reads are listed at the top of the file. Only `JARVIS_PIN`,
`JARVIS_TOKEN_SECRET` and one model API key are required — every other feature
switches itself off when its secret is absent, and `GET /health` reports which
ones came up.

Voice input needs `/stt`, which this worker has. WebView2 carries no Web Speech
API, so on the desktop app transcription has nowhere else to come from.

## One thing to know before you set `ALLOWED_ORIGIN`

It defaults to `*`, which reflects whatever origin asked — so both the website
and the desktop orb work. Pinning it to your site's URL locks the orb out: its
page is served from `tauri://localhost` (`http://tauri.localhost` on Windows),
not from your domain, so the browser drops every reply as a CORS failure. The
symptom is not an error message but an app that appears to have lost its
backend entirely.

If you want the lock, make `ALLOWED_ORIGIN` accept both — the site and
`http://tauri.localhost` — rather than one of them.

## Known: the orb needs the network to draw itself

`dist/index.html` pulls three.js and twelve of its addons from jsdelivr at
runtime. The hologram *is* three.js, so with no connection (or a blocked CDN)
the desktop app opens to the `no-webgl` fallback glow rather than the orb.
Vendoring those files into `dist/` alongside the page would make the app start
offline and faster; it has not been done yet.
