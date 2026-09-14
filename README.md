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

## Two web tools, and only one of them needs Anthropic

`web_search` is Anthropic's own server-side tool. The worker notices a server
tool in the request and puts an Anthropic engine first — but if no
`ANTHROPIC_API_KEY` is set, the request goes to whatever engine is configured
and the tool is **dropped from it silently**. Nothing errors; JARVIS simply
answers from memory as though he had searched. If search matters to you, set
`ANTHROPIC_API_KEY`, and check `GET /health` — `engines` lists what the chain
actually holds.

`search_web` is the way round that. It is an ordinary tool backed by the
worker's own `/search`, which queries DuckDuckGo's HTML endpoint and needs no
key of its own, so it works on Google, Groq, Cerebras, xAI and Workers AI
alike. The page offers exactly one search tool, chosen from what `/health`
reports: Anthropic's when an Anthropic engine is in the chain, otherwise this
one, and neither when the worker is too old to have `/search` — in which case
the system prompt tells him outright that he cannot search, rather than
leaving him to answer from memory believing he did.

`read_page` has no such dependency. It is an ordinary tool backed by the
worker's own `/fetch`, so it survives on Google, Groq, Cerebras, xAI and
Workers AI alike. It fetches one page server-side and returns its text, which
is what lets him answer about a specific product listing or competitor page
rather than about search results.

`/fetch` only reaches public http and https addresses: loopback, private
ranges, link-local (including cloud metadata at 169.254.169.254) and non-http
schemes are refused, and because redirects can point anywhere, the final URL
is re-checked after they are followed rather than only the one submitted.

## What each request costs, and what is done about it

The system prompt and the tool schemas are identical from one request to the
next — around 4,200 tokens of them for JARVIS in English, 5,400 for ULTRON —
and they used to be re-sent and re-billed every time.

The worker now marks a cache breakpoint after them on the Anthropic path, so
subsequent requests read that prefix back instead of paying to reprocess it.
It is done in the worker rather than the page for a reason: the page does not
know which engine will answer, the worker does, so Google, Groq, Cerebras, xAI
and Workers AI never see a field they would not understand. If Anthropic ever
refuses the marker the turn is retried once with the untouched body — a saving
must never be the reason an answer fails to arrive.

The other cost was live view. It attaches a fresh camera frame to every
message, and with a sixteen-message window up to eight stale frames rode along
on every request: roughly 4,000 tokens of pictures already looked at and
answered. Only the newest is kept now; the rest are replaced by a line saying
a frame was there, so referring back to what you showed him still makes sense.
Photographs attached deliberately are never touched — those are the
conversation.
