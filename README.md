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

## The orb no longer needs the network to draw itself

The hologram *is* three.js, and its thirteen files used to be fetched from
jsdelivr on every cold start — which made whether the app drew an orb or the
`no-webgl` fallback glow a property of the network: a locked-down wifi, a
captive portal, a tunnel, or just a slow first paint on a phone.

They now ship in `dist/vendor/three/` (868KB, pinned to r128, and they never
change — which is why re-fetching them was never buying anything). The CDN
stays as a per-file fallback: each local script is followed by a check for the
global it should have defined, and only a missing one is re-requested from
jsdelivr. So uploading `index.html` without `vendor/` beside it still behaves
exactly as it did before rather than silently losing the orb.

Verified by serving `dist/` with every off-origin request blocked: THREE r128
and all twelve addons load, one canvas, no fallback class, no CDN request
made. With `vendor/` removed, all thirteen CDN requests are attempted — the
fallback is real, not decorative.

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

## Installing it on a phone

`dist/` is now also a progressive web app, so the same `index.html` is the
website, the desktop orb and the phone app — one file, three targets, no
second copy to keep in sync.

Upload these alongside it, all in the same folder, and serve them over https
(a service worker will not register otherwise):

    index.html   manifest.webmanifest   sw.js
    icon-192.png   icon-512.png   apple-touch-icon.png
    vendor/      (the whole folder, keeping its structure)

`vendor/` is three.js. Leave it out and the page still works, but the orb goes
back to depending on the CDN being reachable.

Then open the site on the phone and add it to the home screen — Share → Add to
Home Screen on iOS, the install prompt on Android. It opens without browser
chrome, with its own icon, and the second launch is near-instant because the
worker serves the page from cache.

The service worker serves `vendor/` cache-first, the same way it used to serve
the CDN copies: 868KB is not worth re-validating on every cold start when the
files are pinned and cannot change. Change the path under `vendor/` (or bump
`CDN_VERSION`) to ship a different build — editing a file in place under the
same name will keep serving the copy already cached.

**Bump `SHELL_VERSION` in `sw.js` whenever `index.html` changes**, or phones
will keep serving the copy they already installed.

Nothing about the backend changes. Every feature the page has — search, page
reading, code execution, Shopify, calendar, camera, voice — runs on the phone
exactly as it does on the desktop, because the page is the client and it
brought its own tool loop with it.

## Telling him something once, and having it hold

The Memory tab summarizes your conversations, which is useful and also a
rebuild: every refresh regenerates the whole picture from the transcripts, so
anything it did not infer that round is simply gone. That is the wrong shape
for a standing instruction — "always quote prices in shekels" is not something
to be re-derived and possibly missed.

The tab now has an **Add** box. What you type there goes into `profile.pinned`,
which the summarizer is explicitly forbidden to touch, and it is handed to him
at the top of every request as a standing instruction that outranks anything
inferred. It reaches the system prompt on the very next message, in every
conversation, old and new, and survives closing the tab because it is written
to `localStorage` rather than held in the page.

Each note has an ✕ next to it, because a note that cannot be removed is a
setting you are stuck with. Notes he saved himself (the `remember_this` tool)
and notes you typed land in the same list and behave identically — one code
path, so there is no second kind of memory to reason about.

It is per browser and per device, like everything else this page stores. The
phone and the desktop keep separate lists; there is no account syncing them.

## The 3D generator was only running half of itself

Meshy's text-to-3D is two jobs. `preview` produces the mesh — the right shape,
but bare geometry with nothing on its surface. `refine` takes that finished
preview and paints it: base colour, and with `enable_pbr` the metalness,
roughness and normal maps that are the difference between a render and a clay
study. Only the first was ever run, which is why generated models arrived grey.

Both now run, chained in the worker rather than the page: the page polls one
endpoint and is told which task to poll next, so the worker stays stateless and
an older deployed copy of it still answers the page correctly. The progress bar
gives each stage half its range.

It costs a second credit and about another minute. If the texture pass cannot
start, or fails, or times out, the mesh that was already generated and paid for
is shown instead and the result says `textured:false` — so he says it is a bare
mesh rather than describing colours it does not have. If `enable_pbr` is
refused, texturing is retried without it before anything is given up, because a
textured model without PBR maps still beats a grey one.

Prompts matter more than they did, and the tool now says so: the texture pass
reads the same prompt, so naming the finish and material of each part ("matte
black anodised aluminium, brushed steel cap, thin copper band") gives it
something to paint where "a bottle" gives it nothing.

## What the model viewer was doing to every model

Two things, both visible in a screenshot.

The camera sat at a fixed `(0, 0.6, 3)` whatever arrived in front of it. At the
stage's real 260px height that is a 2:1 frame, and a model normalised to 1.8
units reached 86% of the way to the bottom edge — so wide objects clipped the
moment you turned them. The camera is now fitted to the model's bounding
**sphere**, which is the same size from every angle, so a framed model cannot
grow out of frame however it is rotated. Measured across a full rotation sweep
(every 15° of azimuth, six elevations) on a tall bottle, a flat wide box and a
sphere, at both desktop and phone aspect: worst case 0.966 of the way to the
edge, on all of them.

And there was nothing under the model, so it floated, and a floating object
reads as a preview rather than as a thing. There is now a soft contact shadow
on the ground beneath it, sized to that model's own footprint. It is a painted
gradient rather than a shadow map: a real one needs per-model bias tuning and
streaks across half of them when it is wrong, and this cannot fail that way.
It is back-face culled, so turning the model underneath does not reveal a dark
disc floating in front of it.
