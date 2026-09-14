# Glowpulse-pro — working notes

## Delivering changes

**After every fix, send the updated files to the user, always.** Not a summary of
the change — the files themselves:

- `jarvis-worker.js` — whenever the worker changed
- `jarvis-desktop/dist/index.html` — whenever the page changed
- `jarvis-desktop/mic-test.html` — whenever the mic test changed
- `jarvis-desktop.zip` — the whole `jarvis-desktop/` folder, rebuilt, every time
  any file inside it changed

Build the zip so it unpacks to a `jarvis-desktop/` folder, matching the layout
the user already has, so it drops straight in:

```bash
cd /home/user/Glowpulse-pro && rm -f /tmp/jarvis-desktop.zip && \
  zip -qr /tmp/jarvis-desktop.zip jarvis-desktop \
  -x 'jarvis-desktop/node_modules/*' 'jarvis-desktop/src-tauri/target/*' \
     'jarvis-desktop/src-tauri/gen/*'
```

Send them with SendUserFile. Committing and pushing is not a substitute — the
user works from the files, not from the branch.

## Layout

Two deployables, separate: `jarvis-worker.js` is the Cloudflare Worker holding
every secret; `jarvis-desktop/` is the Tauri app whose entire frontend is the
single file `dist/index.html`.

## Things that bite here

- **The page is one file with no build step.** No bundler, no imports of its
  own — edit `dist/index.html` directly. It is ~500KB, so edit by targeted
  replacement, never by rewriting the file.
- **Tauri, not Electron.** `-webkit-app-region` does nothing; window dragging
  needs `data-tauri-drag-region`. A CSS `mask` does not create a hole in
  hit-testing — only stacking or `clip-path` does.
- **Elements can exist with no stylesheet.** Several ids and state classes were
  toggled by live code and styled nowhere, so the behaviour ran invisibly. When
  adding a class or element, check the CSS actually exists for it.
- **Check the page against the worker.** Every endpoint the page fetches must
  exist in `jarvis-worker.js`, and every `invoke()` name must match
  `generate_handler!` in `src-tauri/src/main.rs`.
- **`mic-test.html` deliberately duplicates the detector's maths.** If the VAD
  constants or the RMS calculation change in `dist/index.html`, change them
  there too or the tool starts lying.

## Verifying without a build

Rust: `rustfmt --edition 2021 --check src-tauri/src/main.rs` (exit 0 or 1 means
it parses; 101 is a real syntax error). The full Tauri build needs Windows or
macOS and is not runnable here.

The worker runs under plain Node — import it as an ES module, stub
`globalThis.fetch`, and drive it through `worker.fetch(new Request(...), env)`.
It exports `__test` for cooldown and chain inspection.

The page's CSS can be checked in the pre-installed Chromium via Playwright
(`/opt/node22/lib/node_modules/playwright`), forcing state by setting
`document.documentElement.className`. Wait out any transition before reading
`getComputedStyle`, or you read the value at the start of the animation.
