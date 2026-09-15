/* =====================================================================
   JARVIS — service worker
   =====================================================================

   Two jobs, and deliberately nothing else.

   It makes the app open instantly instead of re-downloading a 500KB page
   over a phone connection, and it caches the three.js files the hologram is
   built from. Those come from a CDN at runtime, which is fine on a desk and
   is the difference between an orb and a blank screen on a train — it is
   already written up as a known issue in the README, and this is the fix for
   the mobile case.

   What it must never do is touch the worker. Every reply, transcription,
   search and page read is a cross-origin POST to the backend, and a service
   worker that served any of those from cache would hand back yesterday's
   answer to today's question. Anything that is not a same-origin GET, or a
   GET of one of the CDN scripts, is passed straight through untouched.

   Bump SHELL_VERSION whenever dist/index.html changes, or phones will keep
   serving the copy they already have.
   ===================================================================== */

const SHELL_VERSION = 'jarvis-shell-v2';
const CDN_VERSION   = 'jarvis-cdn-v1';

/* Only what the app cannot start without. Icons are left out on purpose:
   the phone reads them once at install time from the manifest and never
   again, so caching them buys nothing and costs storage. */
const SHELL = [
  './',
  './index.html',
  './manifest.webmanifest'
];

const isCDN = url => url.origin === 'https://cdn.jsdelivr.net';

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(SHELL_VERSION)
      /* addAll fails the whole install if any single entry 404s, which would
         leave the app with no worker at all. Added one at a time so a missing
         file costs that file and nothing more. */
      .then(cache => Promise.all(SHELL.map(url => cache.add(url).catch(() => null))))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(names => Promise.all(
        names.filter(n => n !== SHELL_VERSION && n !== CDN_VERSION)
             .map(n => caches.delete(n))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;          // the backend is all POSTs

  let url;
  try { url = new URL(request.url); } catch (e) { return; }

  /* The three.js bundle never changes for a pinned version, so cache-first is
     both correct and the whole point: after one load the hologram draws with
     no network at all. */
  if (isCDN(url)) {
    event.respondWith(
      caches.match(request).then(hit => hit || fetch(request).then(res => {
        /* Opaque because it is cross-origin without CORS. It cannot be read
           here, but it can be replayed to the page, which is all that is
           needed. */
        const copy = res.clone();
        caches.open(CDN_VERSION).then(c => c.put(request, copy)).catch(() => {});
        return res;
      }).catch(() => hit))
    );
    return;
  }

  if (url.origin !== self.location.origin) return;   // anything else: not ours

  /* Network first for the page itself. A cached shell that never refreshes is
     an app frozen at the version it was installed on, and this one is updated
     often. The cache is the fallback for being offline, not the source. */
  event.respondWith(
    fetch(request)
      .then(res => {
        if (res && res.ok) {
          const copy = res.clone();
          caches.open(SHELL_VERSION).then(c => c.put(request, copy)).catch(() => {});
        }
        return res;
      })
      .catch(() => caches.match(request).then(hit => hit || caches.match('./index.html')))
  );
});
