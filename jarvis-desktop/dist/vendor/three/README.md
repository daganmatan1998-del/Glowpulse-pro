# three.js r128 — vendored

These are unmodified files from the `three` npm package at version 0.128.0,
copied here so the hologram draws without fetching anything at runtime. They
are the exact thirteen files `dist/index.html` used to pull from
`https://cdn.jsdelivr.net/npm/three@0.128.0/`, at the same relative paths, so
the two are interchangeable — which is what lets the page fall back to the CDN
per file when one of these is missing.

Do not edit them. To move to a different version, replace the folder and change
the version in the fallback URL inside `dist/index.html` (the `__three` helper),
then bump `SHELL_VERSION` in `sw.js`. Editing a file in place under the same
name will keep serving the copy already in the service worker's cache.

three.js is MIT licensed — see LICENSE in this folder.
