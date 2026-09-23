/* =====================================================================
   J.A.R.V.I.S. — Cloudflare Worker backend
   =====================================================================

   Every secret lives here, as a Worker secret. Nothing sensitive is ever
   sent to the browser.

   Required secrets (wrangler secret put NAME):
     JARVIS_PIN            the PIN you type on the auth gate
                           (AUTH_PIN is accepted too, if that is what you have)
     JARVIS_TOKEN_SECRET   any long random string — signs session tokens
                           (SESSION_SECRET is accepted too)
     ANTHROPIC_API_KEY     sk-ant-...

   Optional secrets — each feature simply switches itself off if missing:
     CARTESIA_API_KEY      sk_car_...        → voice
     CARTESIA_VOICE_ID     a voice uuid      → voice (has a default)
     SHOPIFY_STORE         e.g. ovrea         → single-store tools (see SHOPIFY_STORES below)
     SHOPIFY_ADMIN_TOKEN   shpat_...          → single-store tools
     GOOGLE_CLIENT_ID                        → calendar
     GOOGLE_CLIENT_SECRET                    → calendar
     GOOGLE_REFRESH_TOKEN                    → calendar
     ALLOWED_ORIGIN        https://your.site → CORS lock (defaults to *)
     PRIMARY_API_KEY       AIza/gsk_/sk-or-... → try THIS before Anthropic. Put a
                           free-tier key here and Anthropic becomes the safety
                           net instead of the meter.
     PRIMARY_MODEL         optional override (inferred from the key prefix)
     PRIMARY_API_URL       optional override (inferred from the key prefix)
     FALLBACK_API_KEY      another key         → tried after Anthropic
     FALLBACK_API_KEY_2    and another         → tried after that
     FALLBACK_API_KEY_3    and another         → every free tier has a daily
                           ceiling, so two or three in a row is what makes a
                           free backend dependable rather than free until 4pm
     FALLBACK_MODEL[_2/_3] optional overrides
     FALLBACK_API_URL[_2/_3] optional overrides
     SHOPIFY_STORES         JSON array for MORE THAN ONE store, e.g.
                            [{"name":"ovrea","store":"ovrea","token":"shpat_..."},
                             {"name":"aura","store":"aura-store-9f2","token":"shpat_..."}]
                            "name" is what the user calls it in conversation and
                            what shopify_admin_query expects as store_name; "store"
                            is the *.myshopify.com subdomain, which is often
                            different from the storefront's public name. When this
                            is set it is the full list — SHOPIFY_STORE/_ADMIN_TOKEN
                            are folded in automatically if they are also present,
                            so nothing already configured stops working.

   Endpoints:
     POST /auth/pin            { pin }                  → { ok, token }
     POST /                    Anthropic messages proxy (streams if stream:true)
     POST /v1/messages         same thing, explicit path
     POST /tts                 { text, language }       → audio/wav
     POST /image               { prompt, reference? }   → { image: base64 }
     POST /mcp/<name>          proxy to a remote MCP server, adding its own auth header
     GET  /calendar/upcoming?days=7                     → { events: [...] }
     POST /calendar/create     { title, start, end, ... } → { ok, event }
     POST /shopify/query       { query, variables }     → GraphQL result
     GET  /health                                       → capability report
     GET  /session                                      → { ok:true } if the token
                               is good. Costs nothing and calls nobody, so the
                               page can check a stored token before trusting it.
     POST /fallback/test       (no body)              → { ok, engines: [...] } — asks
                               every configured engine, before you need them
     GET  /router/stats, GET /router/registry, POST /router/explain,
     POST /router/feedback     the Claude model router — see its section at the
                               end of this file
   ===================================================================== */

const WORKER_VERSION = '2.4.0';
const TOKEN_TTL_SECONDS = 60 * 60 * 24 * 30; // 30 days
const ANTHROPIC_VERSION = '2023-06-01';
const DEFAULT_VOICE_ID = 'ef191366-f52f-447a-a398-ed8c0f2943a1';
const SHOPIFY_API_VERSION = '2025-07';

/* This account already had its secrets set up under different names, so accept
   either spelling rather than making anyone re-enter them. New name first, then
   the one that is already there. */
function pinSecret(env)   { return env.JARVIS_PIN || env.AUTH_PIN || ''; }
function tokenSecret(env) { return env.JARVIS_TOKEN_SECRET || env.SESSION_SECRET || ''; }

export default {
  async fetch(request, env, ctx) {
    routerCtx = ctx || null;
    const url = new URL(request.url);
    const path = url.pathname.replace(/\/+$/, '') || '/';

    if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers: cors(env, request) });

    try {
      if (path === '/health' || (path === '/' && request.method === 'GET')) {
        return json(await health(env), 200, env, request);
      }
      if (path === '/auth/pin')     return await handleAuth(request, env);

      const authed = await requireToken(request, env);
      if (!authed) return json({ error: 'unauthorized' }, 401, env, request);

      if (path === '/' || path === '/v1/messages') return await handleMessages(request, env);
      if (path === '/tts')                         return await handleTts(request, env);
      if (path === '/model3d')                     return await handleModel3d(request, env);
      if (path === '/model3d/status')              return await handleModel3dStatus(request, env);
      if (path === '/image')                       return await handleImage(request, env);
      if (path === '/stt')                         return await handleStt(request, env);
      if (path === '/fetch')                       return await handleFetch(request, env);
      if (path === '/search')                      return await handleSearch(request, env);
      if (path.indexOf('/mcp/') === 0)             return await handleMcpProxy(request, env, path);
      if (path === '/calendar/upcoming')           return await handleCalendarUpcoming(request, env, url);
      if (path === '/calendar/create')             return await handleCalendarCreate(request, env);
      if (path === '/shopify/query')               return await handleShopify(request, env);
      if (path === '/fallback/test')               return await handleFallbackTest(env, request);
      if (path === '/session')                     return json({ ok: true }, 200, env, request);
      if (path.indexOf('/router/') === 0)          return await handleRouter(request, env, path);

      return json({ error: 'not found: ' + path }, 404, env, request);
    } catch (err) {
      return json({ error: String((err && err.message) || err) }, 500, env, request);
    }
  }
};

function cors(env, request) {
  const allowed = env.ALLOWED_ORIGIN || '*';
  const origin = request ? request.headers.get('Origin') : null;
  const value = allowed === '*' ? (origin || '*') : allowed;
  return {
    'Access-Control-Allow-Origin': value,
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type, X-Jarvis-Token, Authorization, anthropic-version, anthropic-beta',
    /* Without this the page cannot READ a single one of the X-Jarvis-* headers
       below: a cross-origin response only exposes a handful of safelisted
       headers unless the server names the rest here. Everything the worker was
       trying to tell the page about itself — which engine answered, that it had
       fallen back to another model, that an image was dropped — arrived and was
       then discarded by the browser. That is why the debug line said
       "engine=unknown" while the worker knew perfectly well which engine it was. */
    'Access-Control-Expose-Headers': 'X-Jarvis-Engine, X-Jarvis-Fallback, X-Jarvis-Voice, X-Jarvis-Blind, X-Jarvis-Vision, X-Jarvis-Route, X-Jarvis-Model, X-Jarvis-Task, X-Jarvis-Escalated',
    'Access-Control-Max-Age': '86400',
    'Vary': 'Origin'
  };
}

function json(obj, status, env, request) {
  return new Response(JSON.stringify(obj), {
    status: status || 200,
    headers: { 'Content-Type': 'application/json', ...cors(env, request) }
  });
}

function b64urlEncode(bytes) {
  let bin = '';
  const arr = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  for (let i = 0; i < arr.length; i++) bin += String.fromCharCode(arr[i]);
  return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
function b64urlDecode(str) {
  const pad = str.replace(/-/g, '+').replace(/_/g, '/');
  const bin = atob(pad + '='.repeat((4 - (pad.length % 4)) % 4));
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}
async function hmac(secret, message) {
  const key = await crypto.subtle.importKey(
    'raw', new TextEncoder().encode(secret),
    { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']
  );
  return new Uint8Array(await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(message)));
}
async function mintToken(env) {
  const payload = JSON.stringify({ exp: Math.floor(Date.now() / 1000) + TOKEN_TTL_SECONDS, v: 1 });
  const encoded = b64urlEncode(new TextEncoder().encode(payload));
  const sig = b64urlEncode(await hmac(tokenSecret(env) || 'dev-secret', encoded));
  return encoded + '.' + sig;
}
async function verifyToken(env, token) {
  if (!token || token.indexOf('.') < 0) return false;
  const [encoded, sig] = token.split('.');
  const expected = b64urlEncode(await hmac(tokenSecret(env) || 'dev-secret', encoded));
  if (sig.length !== expected.length) return false;
  let diff = 0;
  for (let i = 0; i < sig.length; i++) diff |= sig.charCodeAt(i) ^ expected.charCodeAt(i);
  if (diff !== 0) return false;
  try {
    const payload = JSON.parse(new TextDecoder().decode(b64urlDecode(encoded)));
    return payload.exp > Math.floor(Date.now() / 1000);
  } catch (e) { return false; }
}
function bearerToken(request) {
  const auth = request.headers.get('Authorization') || '';
  const m = /^Bearer\s+(.+)$/i.exec(auth.trim());
  return m ? m[1].trim() : '';
}

async function requireToken(request, env) {
  const candidates = [
    request.headers.get('X-Jarvis-Token') || '',
    bearerToken(request)
  ];
  try {
    const q = new URL(request.url).searchParams.get('t');
    if (q) candidates.push(q);
  } catch (e) {}
  for (const token of candidates) {
    if (token && await verifyToken(env, token)) return true;
  }
  return false;
}

async function handleAuth(request, env) {
  const body = await request.json().catch(() => ({}));
  const pin = String((body && body.pin) || '');
  const real = String(pinSecret(env));
  if (!real) return json({ error: 'no PIN configured: set JARVIS_PIN (or AUTH_PIN)' }, 500, env, request);
  let diff = pin.length ^ real.length;
  for (let i = 0; i < Math.max(pin.length, real.length); i++) {
    diff |= (pin.charCodeAt(i) || 0) ^ (real.charCodeAt(i) || 0);
  }
  if (diff !== 0) return json({ ok: false }, 200, env, request);
  return json({ ok: true, token: await mintToken(env) }, 200, env, request);
}

async function health(env) {
  const missing = [];
  const chain = engineChain(env);
  if (!chain.length) missing.push('PRIMARY_API_KEY or ANTHROPIC_API_KEY');
  if (!pinSecret(env))        missing.push('JARVIS_PIN (or AUTH_PIN)');
  if (!tokenSecret(env))      missing.push('JARVIS_TOKEN_SECRET (or SESSION_SECRET)');

  return {
    ok: true,
    version: WORKER_VERSION,
    max_tokens: 'passthrough',
    using: {
      pin: env.JARVIS_PIN ? 'JARVIS_PIN' : (env.AUTH_PIN ? 'AUTH_PIN' : null),
      token: env.JARVIS_TOKEN_SECRET ? 'JARVIS_TOKEN_SECRET' : (env.SESSION_SECRET ? 'SESSION_SECRET' : null)
    },
    brain: chain.length > 0,
    engines: chain.map(e => e.label),
    /* Whether anything in the chain can actually LOOK at a picture.
       Reported here so the page can say so when the camera opens, rather
       than letting him hold something up, wait, and be told afterwards
       that it never arrived. The name of the first seeing engine comes
       with it, because "which one" is the next question. */
    /* Two ways to see, and the page needs to know which it has: an engine
       that looks at the pixels itself, or a vision model writing a
       description for one that cannot. Both beat "I cannot see images";
       they are not the same thing and are not reported as the same. */
    vision: chain.some(e => e.vendor === 'anthropic' || engineSeesImages(e, env)) || !!env.AI,
    vision_via: chain.some(e => e.vendor === 'anthropic' || engineSeesImages(e, env))
      ? 'engine' : (env.AI ? 'described' : false),
    vision_engine: (chain.find(e => e.vendor === 'anthropic' || engineSeesImages(e, env)) || {}).label || null,
    voice: !!(env.CARTESIA_API_KEY || env.AI),
    voice_via: env.CARTESIA_API_KEY ? 'cartesia' : (env.AI ? 'workers-ai' : false),
    model3d: !!env.MESHY_API_KEY,
    images: !!env.AI,
    stt: !!env.AI,
    read_page: true,          // present only on workers that carry /fetch
    search: true,             // /search — engine-agnostic, needs no Anthropic key
    stt_language_hint: true,   // present only on workers that accept ?language=
    /* Present only on workers that DETECT the language first and treat the
       hint as a second opinion. The page gates on this: an older worker
       feeds ?language= straight to Whisper as a lock, which is what made
       English spoken into a Hebrew-set interface come back as "Thank you".
       So the page sends the hint only where it is safe to send. */
    stt_detect_first: true,
    fallback: chain.length > 1 ? chain[1].model : false,
    fallback_via: chain.length > 1 ? chain[1].vendor : false,
    mcp: Object.keys(env)
      .filter(k => /^MCP_[A-Z0-9_]+_URL$/.test(k))
      .map(k => k.slice(4, -4).toLowerCase()),
    shopify: shopifyStores(env).map(s => s.name),
    /* The admin handle beside the friendly name, so a deep link can be built
       instead of guessed. admin.shopify.com/store/<handle> needs the
       myshopify subdomain, and with SHOPIFY_STORES the configured name is a
       label \u2014 "Glowpulse" \u2014 which is not it. Without this the page
       has to ask him for a URL it already knows, which was exactly what he
       asked never to be asked for.

       Not a secret: the handle is in the address bar of every admin page he
       has open. The token, which is the secret, stays here. */
    shopify_stores: shopifyStores(env).map(s => ({
      name: s.name,
      handle: String(s.store || '').trim().toLowerCase().replace(/^https?:\/\//, '').split('/')[0].replace(/\.myshopify\.com$/, '')
    })),
    calendar: !!(env.GOOGLE_CLIENT_ID && env.GOOGLE_CLIENT_SECRET && env.GOOGLE_REFRESH_TOKEN),
    router: routerEnabled(env) && !!env.ANTHROPIC_API_KEY
      ? { enabled: true, models: loadRegistry(env).filter(m => m.enabled).map(m => m.id) }
      : { enabled: false },
    missing: missing
  };
}

async function handleMessages(request, env) {
  let chain = engineChain(env);
  if (!chain.length) {
    return json({ error: 'no model configured: set PRIMARY_API_KEY or ANTHROPIC_API_KEY' }, 500, env, request);
  }
  const body = await request.json().catch(() => null);
  if (!body) return json({ error: 'invalid JSON body' }, 400, env, request);

  const wantsServerTool = (body.tools || []).some(t => t && t.type && !t.input_schema);
  if (wantsServerTool) {
    const anthropicFirst = chain.filter(e => e.vendor === 'anthropic');
    if (anthropicFirst.length) {
      chain = anthropicFirst.concat(chain.filter(e => e.vendor !== 'anthropic'));
    }
  }

  const hasImage = (body.messages || []).some(m =>
    Array.isArray(m.content) && m.content.some(b => b && b.type === 'image'));
  let describedBy = null;
  if (hasImage) {
    const seeing = chain.filter(e => e.vendor === 'anthropic' || engineSeesImages(e, env));
    if (seeing.length) {
      chain = seeing.concat(chain.filter(e => seeing.indexOf(e) < 0));
    } else {
      /* Nothing here can look at it. Rather than strip the picture and
         apologise, have a model that CAN see write down what is in it, and
         hand that to the one that is answering. Done once, before any
         engine is tried, so a failover does not re-describe. */
      describedBy = await describeImagesInBody(body, env);
    }
  }

  const skipped = [];
  let lastError = null;

  /* Two passes: everything that is awake, then — only if something was
     skipped — the ones that were resting, because a five-minute cooldown is
     a guess about a provider's state, not a fact, and being wrong about it
     is worse than one extra call when nothing else answered.

     Which engines pass 2 revisits has to be decided HERE, before any of them
     runs. Selecting them with cooling() at the time meant a failure in pass 1
     put that engine on cooldown and pass 2 then read the cooldown it had just
     set as "was resting, try it" — so every engine that failed was called a
     second time, back to back, in the same request. Twice the latency and
     twice the quota, at exactly the moment the user is already waiting. */
  const resting = chain.filter(engine => cooling(engine));
  const awake = chain.filter(engine => !cooling(engine));

  for (const group of [awake, resting]) {
    for (let i = 0; i < group.length; i++) {
      const engine = group[i];
      const attempt = await callEngine(engine, body, env, request, group !== awake || i > 0, describedBy);
      if (attempt.ok) { clearCooldown(engine); return attempt.response; }

      if (!attempt.retriable) return attempt.response;
      setCooldown(engine);
      skipped.push(engine.label + ': ' + attempt.reason);
      lastError = attempt;
    }
  }

  return json({
    code: 'all_engines_down',
    tried: skipped,
    reason: (lastError && lastError.reason) || 'unknown',
    vendor: (lastError && lastError.engine && lastError.engine.vendor) || '?',
    model: (lastError && lastError.engine && lastError.engine.model) || '?',
    error: 'Every configured model is unavailable — ' + skipped.join('; ')
  }, 502, env, request);
}

/* The system prompt and the tool schemas are byte-identical from one request
   to the next — roughly four thousand tokens of them — and they were being
   re-sent and re-billed every single time. Marking a cache breakpoint after
   them is what that costs: Anthropic caches everything before the breakpoint
   (tools first, then system, in that order), and subsequent requests read the
   prefix back at a fraction of the price instead of paying to reprocess it.

   Done HERE rather than in the page, deliberately. The page does not know
   which engine will answer; the worker does. So the caching marker only ever
   reaches Anthropic, and Google, Groq, Cerebras, xAI and Workers AI never see
   a field they would not understand.

   It also stays fail-safe. If Anthropic ever rejects the marker, callEngine
   retries once with the original untouched body rather than failing the turn
   — a caching optimisation must never be the reason an answer does not
   arrive. */
function withCaching(body) {
  if (!body || typeof body.system !== 'string' || !body.system) return body;
  /* Below roughly a thousand tokens there is nothing worth caching and the
     marker is refused; ~4 chars per token is rough but the floor here is far
     above it either way. */
  if (body.system.length < 4000) return body;
  return {
    ...body,
    system: [{ type: 'text', text: body.system, cache_control: { type: 'ephemeral' } }]
  };
}

function looksLikeCacheComplaint(status, detail) {
  return status === 400 && /cache_control|cache|ephemeral/i.test(String(detail || ''));
}

/* system arrives as a plain string from the page, but withCaching turns it
   into blocks — and a failover to an OpenAI-compatible engine then passes
   that array through here. String(array) yields "[object Object]", which
   would have handed the model a system prompt made of nothing. */
function systemText(system) {
  if (!system) return '';
  if (typeof system === 'string') return system;
  if (Array.isArray(system)) {
    return system.map(b => (b && typeof b.text === 'string') ? b.text : '').join('\n').trim();
  }
  return String(system);
}

async function callEngine(engine, body, env, request, announce, describedBy) {
  /* Workers AI is a binding, not an endpoint: no fetch, no key, no streaming
     to convert. Handled up front so the HTTP path below stays untouched. */
  if (engine.vendor === 'workers-ai') return await callWorkersAI(engine, body, env, request, announce, describedBy);
  /* Anthropic goes through the model router first, which calls back in here
     once per model it tries, marked `routed` so it does not route again. */
  if (engine.vendor === 'anthropic' && !engine.routed && routerEnabled(env)) {
    return await callAnthropicRouted(engine, body, env, request, announce, describedBy);
  }
  let upstream;
  try {
    upstream = engine.vendor === 'anthropic'
      ? await fetch('https://api.anthropic.com/v1/messages', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'x-api-key': engine.key,
            'anthropic-version': ANTHROPIC_VERSION,
            'anthropic-beta': 'mcp-client-2025-04-04'
          },
          body: JSON.stringify(withCaching(body))
        })
      : await fetch(engine.url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + engine.key },
          body: JSON.stringify(toOpenAIRequest(body, env, engine))
        });
  } catch (err) {
    return { ok: false, retriable: true, reason: 'unreachable', engine: engine,
             response: json({ error: 'could not reach ' + engine.label }, 502, env, request) };
  }

  let detail = upstream.ok ? '' : await upstream.text().catch(() => '');

  /* Caching must never be the reason an answer does not arrive. If the marker
     is ever refused — an account without it, a version that wants it spelled
     differently — the turn is retried once with the body exactly as the page
     sent it, and the only thing lost is the saving. */
  if (!upstream.ok && engine.vendor === 'anthropic' &&
      looksLikeCacheComplaint(upstream.status, detail) && withCaching(body) !== body) {
    try {
      const plain = await fetch('https://api.anthropic.com/v1/messages', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-api-key': engine.key,
          'anthropic-version': ANTHROPIC_VERSION,
          'anthropic-beta': 'mcp-client-2025-04-04'
        },
        body: JSON.stringify(body)
      });
      upstream = plain;
      detail = upstream.ok ? '' : await upstream.text().catch(() => '');
    } catch (err) {
      return { ok: false, retriable: true, reason: 'unreachable', engine: engine,
               response: json({ error: 'could not reach ' + engine.label }, 502, env, request) };
    }
  }

  if (!upstream.ok && upstream.status === 400 && engine.vendor === 'google' &&
      !googleRejectsReasoningEffort && /INVALID_ARGUMENT/i.test(detail)) {
    googleRejectsReasoningEffort = true;
    try {
      const retry = await fetch(engine.url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + engine.key },
        body: JSON.stringify(toOpenAIRequest(body, env, engine))
      });
      upstream = retry;
      detail = upstream.ok ? '' : await upstream.text().catch(() => '');
    } catch (err) {
      return { ok: false, retriable: true, reason: 'unreachable', engine: engine,
               response: json({ error: 'could not reach ' + engine.label }, 502, env, request) };
    }
  }

  if (!upstream.ok) {
    if (engine.vendor !== 'anthropic' && !engine.repicked &&
        failureReason(upstream.status, detail) === 'no_such_model') {
      const picked = await pickAvailableModel(engine);
      if (picked && picked !== engine.model) {
        engine.model = picked;
        engine.label = engine.vendor + '/' + picked;
        engine.repicked = true;
        return await callEngine(engine, body, env, request, announce, describedBy);
      }
    }

    const retriable = shouldFailover(upstream.status, detail);
    return {
      ok: false,
      retriable: retriable,
      reason: failureReason(upstream.status, detail),
      engine: engine,
      response: json({
        code: retriable ? 'engine_failed' : 'request_rejected',
        vendor: engine.vendor, model: engine.model, status: upstream.status,
        reason: failureReason(upstream.status, detail),
        error: engine.label + ' returned ' + upstream.status + ': ' + detail.slice(0, 200)
      }, retriable || upstream.status === 401 || upstream.status === 403
           ? 502 : upstream.status, env, request)
    };
  }

  const headers = { ...cors(env, request), 'X-Jarvis-Engine': engine.label };
  if (announce) headers['X-Jarvis-Fallback'] = engine.model;
  /* The reply will talk about an image it never received. Without this the page
     has no way to know that, and the user is left thinking the app is lying. */
  if (imageWillBeDropped(engine, body, env)) headers['X-Jarvis-Blind'] = engine.label;
  if (describedBy) headers['X-Jarvis-Vision'] = describedBy;

  if (engine.vendor === 'anthropic') {
    const contentType = upstream.headers.get('Content-Type') || '';
    if (body.stream && contentType.includes('text/event-stream')) {
      return { ok: true, engine: engine, response: new Response(upstream.body, {
        status: upstream.status,
        headers: { ...headers, 'Content-Type': 'text/event-stream; charset=utf-8',
                   'Cache-Control': 'no-cache, no-transform', 'Connection': 'keep-alive',
                   'X-Accel-Buffering': 'no' }
      })};
    }
    return { ok: true, engine: engine, response: new Response(await upstream.text(), {
      status: upstream.status, headers: { ...headers, 'Content-Type': 'application/json' }
    })};
  }

  if (body.stream) {
    return { ok: true, engine: engine, response: new Response(openAIStreamToAnthropic(upstream.body), {
      status: 200,
      headers: { ...headers, 'Content-Type': 'text/event-stream; charset=utf-8',
                 'Cache-Control': 'no-cache, no-transform', 'X-Accel-Buffering': 'no' }
    })};
  }
  const data = await upstream.json();
  return { ok: true, engine: engine, response: new Response(JSON.stringify(openAIMessageToAnthropic(data)), {
    status: 200, headers: { ...headers, 'Content-Type': 'application/json' }
  })};
}

const AURA_MODEL = '@cf/deepgram/aura-1';
const MELO_MODEL = '@cf/myshell-ai/melotts';

/* The keyless engine, called from callEngine when the chain reaches it. This
   function was referenced but never actually defined — the reference shipped,
   the body did not — so the last-resort engine would have thrown the moment
   every keyed provider was dry, which is precisely when it is needed.

   Tools are not offered here: these models handle function calling
   inconsistently, and a mangled tool call is worse than a plain answer from
   an engine whose only job is to keep something responding. */
async function callWorkersAI(engine, body, env, request, announce, describedBy) {
  const messages = [];
  { const sys = systemText(body.system); if (sys) messages.push({ role: 'system', content: sys }); }
  for (const m of (body.messages || [])) {
    const text = anthropicBlocksToOpenAI(m.content, false);
    if (text && text.length) messages.push({ role: m.role, content: text });
  }
  try {
    const out = await env.AI.run(engine.model, {
      messages: messages,
      max_tokens: Math.min(body.max_tokens || 2048, 4096)
    });
    const text = (out && (out.response || out.result || '')) || '';
    if (!String(text).trim()) {
      return { ok: false, retriable: true, reason: 'other', engine: engine,
               response: json({ error: engine.label + ' returned nothing' }, 502, env, request) };
    }
    const headers = { ...cors(env, request), 'X-Jarvis-Engine': engine.label };
    if (announce) headers['X-Jarvis-Fallback'] = engine.model;
    if (imageWillBeDropped(engine, body, env)) headers['X-Jarvis-Blind'] = engine.label;
    if (describedBy) headers['X-Jarvis-Vision'] = describedBy;
    const shaped = {
      content: [{ type: 'text', text: stripLeadingThinkingBlock(String(text)) }],
      stop_reason: 'end_turn',
      usage: { output_tokens: 0 }
    };
    /* The client may have asked for a stream. Rather than refuse, hand the
       whole answer back as one well-formed SSE burst so the page's existing
       reader, typewriter and speech queue all behave normally. */
    if (body.stream) {
      const enc = new TextEncoder();
      const ev = (n, d) => enc.encode('event: ' + n + '\ndata: ' + JSON.stringify(d) + '\n\n');
      const stream = new ReadableStream({
        start(c) {
          c.enqueue(ev('message_start', { type:'message_start', message:{ role:'assistant', content:[] } }));
          c.enqueue(ev('content_block_start', { type:'content_block_start', index:0,
                        content_block:{ type:'text', text:'' } }));
          c.enqueue(ev('content_block_delta', { type:'content_block_delta', index:0,
                        delta:{ type:'text_delta', text: shaped.content[0].text } }));
          c.enqueue(ev('content_block_stop', { type:'content_block_stop', index:0 }));
          c.enqueue(ev('message_delta', { type:'message_delta', delta:{ stop_reason:'end_turn' },
                        usage:{ output_tokens:0 } }));
          c.enqueue(ev('message_stop', { type:'message_stop' }));
          c.close();
        }
      });
      return { ok: true, engine: engine, response: new Response(stream, { status: 200,
        headers: { ...headers, 'Content-Type':'text/event-stream; charset=utf-8',
                   'Cache-Control':'no-cache, no-transform', 'X-Accel-Buffering':'no' } }) };
    }
    return { ok: true, engine: engine, response: new Response(JSON.stringify(shaped), {
      status: 200, headers: { ...headers, 'Content-Type': 'application/json' } }) };
  } catch (err) {
    const msg = String((err && err.message) || err);
    return { ok: false, retriable: true,
             reason: /limit|quota|neuron/i.test(msg) ? 'no_credit' : 'other',
             engine: engine,
             response: json({ error: engine.label + ': ' + msg.slice(0, 200) }, 502, env, request) };
  }
}

async function ttsViaCartesia(env, text, voiceId, language) {
  const upstream = await fetch('https://api.cartesia.ai/tts/bytes', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Cartesia-Version': '2026-03-01',
      'Authorization': 'Bearer ' + env.CARTESIA_API_KEY
    },
    body: JSON.stringify({
      model_id: 'sonic-3.5',
      transcript: text,
      voice: { mode: 'id', id: voiceId || env.CARTESIA_VOICE_ID || DEFAULT_VOICE_ID },
      language: language,
      output_format: { container: 'wav', encoding: 'pcm_s16le', sample_rate: 44100 }
    })
  });
  if (upstream.ok) return { ok: true, body: upstream.body };
  const detail = await upstream.text().catch(() => '');
  return { ok: false, status: upstream.status, detail: detail.slice(0, 200) };
}

async function ttsViaWorkersAI(env, text, language) {
  if (!env.AI) return { ok: false, status: 0, detail: 'Workers AI is not bound' };
  const attempts = language === 'en'
    ? [{ model: env.TTS_MODEL || AURA_MODEL, input: { text: text, speaker: env.TTS_SPEAKER || 'orion', container: 'wav' } },
       { model: MELO_MODEL, input: { prompt: text, lang: 'en' } }]
    : [{ model: MELO_MODEL, input: { prompt: text, lang: language } }];

  let last = 'no attempt ran';
  for (const attempt of attempts) {
    try {
      const result = await env.AI.run(attempt.model, attempt.input, { returnRawResponse: true });
      if (result && typeof result.body !== 'undefined' && typeof result.status === 'number') {
        if (result.ok) return { ok: true, body: result.body };
        last = attempt.model + ' returned ' + result.status;
        continue;
      }
      if (result && typeof result.getReader === 'function') return { ok: true, body: result };
      if (result && result.audio) {
        const bin = atob(result.audio);
        const bytes = new Uint8Array(bin.length);
        for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
        return { ok: true, body: bytes };
      }
      last = attempt.model + ' returned an unrecognised shape';
    } catch (err) {
      last = attempt.model + ' threw: ' + String(err).slice(0, 120);
    }
  }
  return { ok: false, status: 0, detail: last };
}

async function handleTts(request, env) {
  const body = await request.json().catch(() => ({}));
  const text = String((body && body.text) || '').slice(0, 3000);
  if (!text.trim()) return json({ error: 'empty text' }, 400, env, request);
  const language = body.language === 'he' ? 'he' : (body.language || 'en');

  const notes = [];

  if (env.CARTESIA_API_KEY) {
    const first = await ttsViaCartesia(env, text, body.voiceId, language);
    if (first.ok) return audioResponse(first.body, env, request, 'cartesia');
    notes.push('cartesia ' + first.status + ': ' + first.detail);
  } else {
    notes.push('cartesia: no key set');
  }

  const second = await ttsViaWorkersAI(env, text, language);
  if (second.ok) return audioResponse(second.body, env, request, 'workers-ai');
  notes.push('workers-ai: ' + second.detail);

  return json({
    error: notes.join(' | '),
    upstream_status: 502,
    voice_exhausted: true
  }, 502, env, request);
}

function audioResponse(body, env, request, via) {
  return new Response(body, {
    status: 200,
    headers: {
      'Content-Type': 'audio/wav',
      'Cache-Control': 'no-store',
      'X-Jarvis-Voice': via,
      ...cors(env, request)
    }
  });
}

let googleRejectsReasoningEffort = false;

const MESHY_BASE = 'https://api.meshy.ai/openapi/v2/text-to-3d';
/* A photograph is a different job to a sentence, on a different endpoint and a
   different version, and it is a SINGLE pass: the picture already carries the
   colour, so there is no preview-then-paint split to chain. Which of the two a
   task belongs to has to travel in the query string on the way back, exactly
   like `stage` does, because the worker keeps no state and cannot look a task
   id up later to find out what kind it was. */
const MESHY_IMAGE_BASE = 'https://api.meshy.ai/openapi/v1/image-to-3d';

function meshyBaseFor(kind){
  return kind === 'image' ? MESHY_IMAGE_BASE : MESHY_BASE;
}

/* Meshy's text-to-3D is two jobs, not one. `preview` produces the mesh: the
   right shape, but bare geometry with no surface on it. `refine` takes that
   finished preview and paints it — base colour, and with enable_pbr the
   metalness, roughness and normal maps that make a render look like a
   photograph rather than a clay study.
   Only the first half was ever run here, which is why generated models arrived
   looking like grey sculpture. The second half costs another credit and about
   another minute, and it is the whole difference.
   Chaining is done here rather than in the page because the page must not hold
   a Meshy task id's meaning: it polls one endpoint and is told what to poll
   next. The worker stays stateless — the stage travels in the query string. */
async function startMeshyTask(payload, env, kind) {
  const res = await fetch(meshyBaseFor(kind), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ' + env.MESHY_API_KEY
    },
    body: JSON.stringify(payload)
  });
  const text = await res.text();
  if (!res.ok) return { error: 'meshy ' + res.status + ': ' + text.slice(0, 300) };
  let data; try { data = JSON.parse(text); } catch (err) { data = {}; }
  const taskId = data.result || data.id;
  if (!taskId) return { error: 'meshy did not return a task id: ' + text.slice(0, 200) };
  return { taskId: taskId };
}

async function handleModel3d(request, env) {
  if (request.method !== 'POST') return json({ error: 'POST only' }, 405, env, request);
  if (!env.MESHY_API_KEY) {
    return json({ error: '3D generation is not configured: add MESHY_API_KEY to the worker' }, 503, env, request);
  }
  const body = await request.json().catch(() => ({}));

  /* A picture, if one came. Meshy takes it as a data URI, which is what the
     page already holds for every attachment and every camera frame, so
     nothing has to be uploaded anywhere first. */
  const image = String((body && body.image) || '').trim();
  if (image) {
    if (!/^data:image\/(png|jpe?g|webp);base64,/i.test(image)) {
      return json({ error: 'the image must be a png, jpeg or webp data URI' }, 400, env, request);
    }
    /* Meshy's own ceiling is generous but a request this size is worth
       refusing early with a readable reason rather than as a 413 from
       somewhere downstream. */
    if (image.length > 12 * 1024 * 1024) {
      return json({ error: 'that image is too large; send one under about 8MB' }, 413, env, request);
    }
    const startedImg = await startMeshyTask({
      image_url: image,
      enable_pbr: true,
      should_remesh: true,
      should_texture: true
    }, env, 'image');
    if (startedImg.error) return json({ error: startedImg.error }, 502, env, request);
    return json({ taskId: startedImg.taskId, kind: 'image', stage: 'single' }, 200, env, request);
  }

  const prompt = String((body && body.prompt) || '').trim().slice(0, 600);
  if (!prompt) return json({ error: 'no prompt and no image' }, 400, env, request);

  const started = await startMeshyTask({
    mode: 'preview',
    prompt: prompt,
    art_style: body.style === 'sculpture' ? 'sculpture' : 'realistic',
    should_remesh: true
  }, env);
  if (started.error) return json({ error: started.error }, 502, env, request);

  return json({ taskId: started.taskId, kind: 'text', stage: 'preview' }, 200, env, request);
}

async function readMeshyTask(id, env, kind) {
  const res = await fetch(meshyBaseFor(kind) + '/' + encodeURIComponent(id), {
    headers: { 'Authorization': 'Bearer ' + env.MESHY_API_KEY }
  });
  const text = await res.text();
  if (!res.ok) return { httpError: 'meshy ' + res.status + ': ' + text.slice(0, 300) };
  let data; try { data = JSON.parse(text); } catch (err) { data = {}; }
  return { data: data };
}

async function handleModel3dStatus(request, env) {
  if (!env.MESHY_API_KEY) {
    return json({ error: '3D generation is not configured' }, 503, env, request);
  }
  const params = new URL(request.url).searchParams;
  const id = params.get('id');
  if (!id) return json({ error: 'missing id' }, 400, env, request);

  const kind = params.get('kind') === 'image' ? 'image' : 'text';
  const stage = params.get('stage') === 'refine' ? 'refine' : 'preview';
  /* From a photograph there is only one pass, so there is nothing to chain
     and the progress bar is the raw one. */
  const wantsTexture = kind === 'image' ? false : params.get('refine') !== '0';

  const read = await readMeshyTask(id, env, kind);
  if (read.httpError) return json({ error: read.httpError }, 502, env, request);
  const data = read.data;

  const status = String(data.status || '').toUpperCase();
  const urls = data.model_urls || {};
  const raw = typeof data.progress === 'number' ? data.progress : 0;

  /* Two jobs run back to back, so raw progress would count 0-100 twice. Each
     stage owns half the bar, and it only ever moves forwards. */
  const progress = (stage === 'refine' || !wantsTexture)
    ? (wantsTexture ? 50 + Math.round(raw / 2) : raw)
    : Math.round(raw / 2);

  if (status === 'FAILED') {
    return json({
      status: 'FAILED', stage: stage, progress: progress, glb: null,
      error: (data.task_error && data.task_error.message) || 'generation failed'
    }, 200, env, request);
  }

  if (status !== 'SUCCEEDED') {
    return json({ status: status, stage: stage, progress: progress, glb: null, error: null }, 200, env, request);
  }

  // The untextured mesh, either as the answer or as the thing to paint next.
  const previewGlb = urls.glb || null;

  if (stage === 'preview' && wantsTexture) {
    /* enable_pbr is what buys the metal/roughness maps rather than a flat
       colour. If this build of the API will not take it, texturing still
       matters far more than PBR does — so try again plainly before giving up,
       and if even that fails hand back the mesh we already paid for rather
       than losing the whole job to an optional flag. */
    let refine = await startMeshyTask({ mode: 'refine', preview_task_id: id, enable_pbr: true }, env);
    if (refine.error) refine = await startMeshyTask({ mode: 'refine', preview_task_id: id }, env);
    if (refine.error) {
      return json({
        status: 'SUCCEEDED', stage: 'preview', progress: 100,
        glb: previewGlb, textured: false,
        thumbnail: data.thumbnail_url || null,
        note: 'texturing could not be started (' + refine.error + '); this is the untextured mesh',
        error: null
      }, 200, env, request);
    }
    return json({
      status: 'TEXTURING', stage: 'refine', taskId: refine.taskId,
      progress: 50, glb: null, previewGlb: previewGlb,
      thumbnail: data.thumbnail_url || null, error: null
    }, 200, env, request);
  }

  return json({
    status: 'SUCCEEDED', stage: stage, progress: 100,
    glb: previewGlb,
    /* A model from a photograph is textured by construction \u2014 the picture is
       where the colour came from \u2014 so it must not be reported as a bare mesh
       just because it never went through a refine stage. */
    textured: kind === 'image' ? true : stage === 'refine',
    thumbnail: data.thumbnail_url || null, error: null
  }, 200, env, request);
}

const XAI_FALLBACK_URL     = 'https://api.x.ai/v1/chat/completions';
const DEFAULT_FALLBACK_URL = XAI_FALLBACK_URL;
const OPENAI_FALLBACK_URL  = 'https://api.openai.com/v1/chat/completions';
const GOOGLE_FALLBACK_URL  = 'https://generativelanguage.googleapis.com/v1beta/openai/chat/completions';

const GROQ_FALLBACK_URL       = 'https://api.groq.com/openai/v1/chat/completions';
const OPENROUTER_FALLBACK_URL = 'https://openrouter.ai/api/v1/chat/completions';
const CEREBRAS_FALLBACK_URL   = 'https://api.cerebras.ai/v1/chat/completions';

const OPENAI_VENDOR = { vendor: 'openai', url: OPENAI_FALLBACK_URL, model: 'gpt-5.2' };
const FALLBACK_VENDORS = [
  { test: /^(AIza|AQ\.)/, vendor: 'google',  url: GOOGLE_FALLBACK_URL,     model: 'gemini-3.6-flash' },
  { test: /^gsk_/,     vendor: 'groq',       url: GROQ_FALLBACK_URL,       model: 'llama-3.3-70b-versatile' },
  { test: /^sk-or-/i,  vendor: 'openrouter', url: OPENROUTER_FALLBACK_URL, model: 'openai/gpt-oss-20b:free' },
  { test: /^csk-/i,    vendor: 'cerebras',   url: CEREBRAS_FALLBACK_URL,   model: 'gpt-oss-120b' },
  { test: /^xai-/i,    vendor: 'xai',        url: XAI_FALLBACK_URL,        model: 'grok-4.5' },
  { test: /^sk-/i,     ...OPENAI_VENDOR }
];

function openAIEngine(key, model, url, slot, modelVar) {
  key = String(key || '').trim();
  if (!key) return null;
  const v = FALLBACK_VENDORS.find(x => x.test.test(key)) || OPENAI_VENDOR;
  const engine = {
    key: key,
    slot: slot,
    modelVar: modelVar || (slot + '_MODEL'),
    vendor: v.vendor,
    url: url || v.url,
    model: model || v.model,
    label: ''
  };
  engine.label = engine.vendor + '/' + engine.model;
  return engine;
}

function engineChain(env) {
  const chain = [];
  const primary = openAIEngine(env.PRIMARY_API_KEY, env.PRIMARY_MODEL, env.PRIMARY_API_URL,
                               'PRIMARY', 'PRIMARY_MODEL');
  if (primary) chain.push(primary);
  if (env.ANTHROPIC_API_KEY) {
    chain.push({ vendor: 'anthropic', key: env.ANTHROPIC_API_KEY, slot: 'ANTHROPIC',
                 model: 'claude', label: 'anthropic/claude' });
  }
  for (const n of ['', '_2', '_3']) {
    const e = openAIEngine(env['FALLBACK_API_KEY' + n], env['FALLBACK_MODEL' + n],
                           env['FALLBACK_API_URL' + n], 'FALLBACK' + n, 'FALLBACK_MODEL' + n);
    if (e) chain.push(e);
  }
  /* Last in the chain, and the only engine here that needs no key of its own:
     it runs on the Workers AI binding this worker already has for images and
     transcription. Cloudflare's free plan allows 10,000 Neurons a day, which
     is roughly 15-25 replies — nowhere near enough to be the main brain, and
     it is deliberately last for that reason. What it buys is that the app is
     never completely dead: when every keyed provider is dry or misconfigured
     at once, something still answers instead of "all engines down". */
  if (env.AI) {
    chain.push({
      vendor: 'workers-ai', key: '', slot: 'WORKERS_AI',
      model: env.WORKERS_AI_MODEL || '@cf/meta/llama-3.3-70b-instruct-fp8-fast',
      label: 'workers-ai/' + (env.WORKERS_AI_MODEL || 'llama-3.3-70b')
    });
  }
  return chain;
}

function fallbackProvider(env) {
  return openAIEngine(env.FALLBACK_API_KEY, env.FALLBACK_MODEL, env.FALLBACK_API_URL,
                      'FALLBACK', 'FALLBACK_MODEL');
}

const cooldowns = new Map();
const PRIMARY_COOLDOWN_MS = 5 * 60 * 1000;

function cooling(engine)      { return Date.now() < (cooldowns.get(engine.label) || 0); }
function setCooldown(engine)  { cooldowns.set(engine.label, Date.now() + PRIMARY_COOLDOWN_MS); }
function clearCooldown(engine){ cooldowns.delete(engine.label); }

function failureReason(status, detail) {
  const text = String(detail || '');
  if (status === 401 || status === 403) return 'key_rejected';
  if (/quota|billing|credit|insufficient|exceeded your current/i.test(text)) return 'no_credit';
  if (status === 429) return 'rate_limited';
  if (/model/i.test(text) && (status === 404 || status === 400)) return 'no_such_model';
  if (status >= 500) return 'provider_down';
  return 'other';
}

export const __test = {
  resetCooldown() { cooldowns.clear(); },
  cooldownActive(label) {
    if (label) return Date.now() < (cooldowns.get(label) || 0);
    return cooldowns.size > 0;
  },
  chain(env) { return engineChain(env).map(e => e.label); }
};

function shouldFailover(status, bodyText) {
  const text = String(bodyText || '');
  if (status === 402 || status === 429) return true;
  if (status >= 500) return true;
  if (status === 401 || status === 403) return true;
  if (status === 404) return true;
  if (status === 413) return true;
  if ((status === 400 || status === 422) &&
      /model|not found|does not exist|decommissioned|deprecated/i.test(text)) return true;
  if (status === 400 &&
      /credit balance|insufficient|quota|billing/i.test(text)) return true;
  return false;
}

function anthropicBlocksToOpenAI(content, allowImages) {
  if (typeof content === 'string') return content;
  if (!Array.isArray(content)) return '';

  if (!allowImages) {
    const pieces = [];
    for (const block of content) {
      if (block.type === 'text') pieces.push(block.text);
      else if (block.type === 'image') pieces.push(
        '[An image was attached here by the user, but it could not be delivered to you: ' +
        'the engine answering this request has no vision, so the picture was stripped out on the way. ' +
        'Say plainly that the image did not reach you and that this is a backend limitation — ' +
        'do NOT claim you are unable to look at images as a general matter, and do not pretend ' +
        'the user failed to send one. Asking them to describe it is a reasonable fallback, ' +
        'but only after saying why.]');
      else if (block.type === 'document') pieces.push('[the user attached a document, which this model cannot read]');
    }
    return pieces.join('\n').trim();
  }

  const parts = [];
  for (const block of content) {
    if (block.type === 'text') {
      parts.push({ type: 'text', text: block.text });
    } else if (block.type === 'image' && block.source && block.source.data) {
      parts.push({
        type: 'image_url',
        image_url: { url: 'data:' + (block.source.media_type || 'image/jpeg') +
                          ';base64,' + block.source.data }
      });
    }
  }
  if (parts.length === 1 && parts[0].type === 'text') return parts[0].text;
  return parts.length ? parts : '';
}

/* Whether a model can look at a picture is a property of the MODEL, not of the
   company that sells it. This used to whitelist exactly one vendor — google —
   so GPT-4o, Grok, Pixtral, Llama 4 and every vision model on OpenRouter were
   all declared blind, their images replaced by a line of text, and the model
   then told the user it could not see images. It was right: by the time it read
   the request, there was no image in it. */
const VISION_MODELS = [
  /gpt-4o/i, /gpt-4\.1/i, /gpt-4-turbo/i, /gpt-4\.5/i, /gpt-5/i, /chatgpt-4o/i,
  /(^|[\/-])o[134]([-.]|$)/i,
  /gemini/i, /grok-[2-9]/i, /grok.*vision/i,
  /llama-?4/i, /scout/i, /maverick/i, /llama-3\.2-(11|90)b/i, /llama.*vision/i,
  /pixtral/i, /qwen.*-?vl/i, /internvl/i, /molmo/i, /claude/i, /mistral-(small|medium)-3/i
];

function engineSeesImages(provider, env) {
  const vendor = (provider && provider.vendor) || '';
  const model  = String((provider && provider.model) || '');

  /* The manual override, and the only thing that does not go stale as models
     ship. Takes a vendor ("xai") or a piece of a model name ("qwen2.5-vl"). */
  const extra = String((env && env.VISION_ENGINES) || '')
    .split(',').map(x => x.trim().toLowerCase()).filter(Boolean);
  if (extra.some(x => vendor === x || model.toLowerCase().indexOf(x) >= 0)) return true;

  if (vendor === 'anthropic') return true;     // every Claude sees
  if (vendor === 'google')    return true;     // every Gemini sees
  return VISION_MODELS.some(re => re.test(model));
}

/* WHEN NOTHING IN THE CHAIN CAN SEE.

   Until now a picture sent to a text-only engine was simply removed, and
   the assistant then explained that it could not see images. That was
   honest and completely useless: the picture existed, the user was
   looking at it, and the one thing standing between them was that the
   model answering happened to be text-only.

   Workers AI has vision models, and the binding is already here for
   speech. So the picture is DESCRIBED first, by a model that can see,
   and the description goes to the text model in its place. It is not as
   good as a model looking at the pixels itself — and it never claims to
   be: the replacement block says out loud that it is a description, and
   tells the model to say so rather than guess if what it needs is not in
   there.

   Costs one extra Workers AI call per picture and needs no new key. */
const VISION_DESCRIBERS = [
  '@cf/meta/llama-3.2-11b-vision-instruct',
  '@cf/llava-hf/llava-1.5-7b-hf'
];

const DESCRIBE_PROMPT =
  'Describe this image for someone who cannot see it. Name what is in it, ' +
  'read out any text exactly as it appears, and give colours, materials, ' +
  'quantities and anything else specific. Be factual and concrete. Do not ' +
  'speculate about what is not visible.';

/* At most this many pictures per request get described. A turn carrying
   more is a gallery, not a question, and describing all of them would cost
   more time than the answer is worth. */
const DESCRIBE_MAX = 3;

function base64ToBytes(b64) {
  const bin = atob(String(b64 || ''));
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

async function describeOneImage(block, env) {
  let bytes;
  try {
    bytes = base64ToBytes(block.source && block.source.data);
  } catch (e) { return null; }
  if (!bytes.length) return null;
  const asArray = [...bytes];
  for (const model of VISION_DESCRIBERS) {
    try {
      const out = await env.AI.run(model, {
        image: asArray,
        prompt: DESCRIBE_PROMPT,
        max_tokens: 512
      });
      const text = String((out && (out.description || out.response || out.result || '')) || '').trim();
      if (text) return { text: text, model: model };
    } catch (err) { /* try the next describer */ }
  }
  return null;
}

/* Replaces image blocks in place. Returns the model that did the work, or
   null if nothing could be described — in which case the old behaviour
   stands and the picture is stripped further down, as before. */
async function describeImagesInBody(body, env) {
  if (!env || !env.AI) return null;
  let used = null, done = 0;
  for (const message of (body.messages || [])) {
    if (!Array.isArray(message.content)) continue;
    for (let i = 0; i < message.content.length; i++) {
      const block = message.content[i];
      if (!block || block.type !== 'image') continue;
      if (done >= DESCRIBE_MAX) continue;
      const got = await describeOneImage(block, env);
      if (!got) continue;
      done++;
      used = got.model;
      message.content[i] = {
        type: 'text',
        text: '[A picture he sent. The model answering this request cannot see ' +
              'pictures, so it was described by a vision model first. This is the ' +
              'description, not the picture:\n\n' + got.text +
              '\n\nAnswer from this description. If what he is asking about is not ' +
              'in it, say that you are working from a description and ask him ' +
              'what you need — do not invent a detail that is not written above.]'
      };
    }
  }
  return used;
}

/* Did this request carry a picture that this engine will not be shown? */
function imageWillBeDropped(engine, body, env) {
  if (!engine || engine.vendor === 'anthropic') return false;
  if (engineSeesImages(engine, env)) return false;
  return (body && body.messages || []).some(m =>
    Array.isArray(m.content) && m.content.some(b => b && b.type === 'image'));
}

function toOpenAIRequest(body, env, provider) {
  provider = provider || fallbackProvider(env);
  const allowImages = engineSeesImages(provider, env);
  const messages = [];
  { const sys = systemText(body.system); if (sys) messages.push({ role: 'system', content: sys }); }

  for (const message of (body.messages || [])) {
    const content = message.content;

    if (Array.isArray(content)) {
      const toolResults = content.filter(b => b && b.type === 'tool_result');
      const toolUses = content.filter(b => b && b.type === 'tool_use');

      if (toolResults.length) {
        for (const result of toolResults) {
          messages.push({
            role: 'tool',
            tool_call_id: result.tool_use_id,
            content: typeof result.content === 'string'
              ? result.content : JSON.stringify(result.content)
          });
        }
        const leftover = content.filter(b => b && b.type !== 'tool_result');
        if (leftover.length) {
          const converted = anthropicBlocksToOpenAI(leftover, allowImages);
          if (converted && converted.length) messages.push({ role: 'user', content: converted });
        }
        continue;
      }

      if (toolUses.length && message.role === 'assistant') {
        const text = content.filter(b => b.type === 'text').map(b => b.text).join('');
        messages.push({
          role: 'assistant',
          content: text || null,
          tool_calls: toolUses.map(call => ({
            id: call.id,
            type: 'function',
            function: { name: call.name, arguments: JSON.stringify(call.input || {}) }
          }))
        });
        continue;
      }
    }

    const converted = anthropicBlocksToOpenAI(content, allowImages);
    if (converted && converted.length) messages.push({ role: message.role, content: converted });
  }

  const tools = (body.tools || [])
    .filter(tool => tool && tool.name && tool.input_schema)
    .map(tool => ({
      type: 'function',
      function: {
        name: tool.name,
        description: tool.description || '',
        parameters: tool.input_schema
      }
    }));

  const request = {
    model: provider.model,
    messages: messages,
    max_tokens: body.max_tokens || 4096,
    stream: !!body.stream
  };
  if (tools.length) request.tools = tools;

  if (provider.vendor === 'google' && !googleRejectsReasoningEffort) {
    request.reasoning_effort = env.REASONING_EFFORT || 'none';
  }
  return request;
}

/* finish_reason alone is not trustworthy: some OpenAI-compatible providers
   (seen from Groq and OpenRouter's free models in particular) return a real
   tool_calls array while reporting finish_reason "stop" instead of
   "tool_calls" — a bug on their side, not a shape we can rely on. Mapped
   naively, that produced stop_reason "end_turn" alongside an actual
   tool_use block: the client's tool loop checks stop_reason to decide
   whether to run the tool, saw "end_turn", never ran it, and the user got
   an empty reply despite the model having done real work.
   hasToolCalls lets the content itself override a mismatched label. */
function mapFinishReason(reason, hasToolCalls) {
  if (reason === 'tool_calls' || hasToolCalls) return 'tool_use';
  if (reason === 'length') return 'max_tokens';
  return 'end_turn';
}

function stripLeadingThinkingBlock(text) {
  if (!text) return text;
  const match = /^\s*<(thought|thinking)>[\s\S]*?<\/\1>\s*/i.exec(text);
  return match ? text.slice(match[0].length) : text;
}

function openAIMessageToAnthropic(data) {
  const choice = (data.choices || [])[0] || {};
  const message = choice.message || {};
  const content = [];
  if (message.content) content.push({ type: 'text', text: stripLeadingThinkingBlock(message.content) });
  for (const call of (message.tool_calls || [])) {
    let input = {};
    try { input = JSON.parse((call.function && call.function.arguments) || '{}'); } catch (e) {}
    content.push({
      type: 'tool_use',
      id: call.id,
      name: call.function && call.function.name,
      input: input
    });
  }
  return {
    content: content,
    stop_reason: mapFinishReason(choice.finish_reason, (message.tool_calls || []).length > 0),
    usage: { output_tokens: (data.usage && data.usage.completion_tokens) || 0 }
  };
}

function openAIStreamToAnthropic(upstreamBody) {
  const encoder = new TextEncoder();
  const decoder = new TextDecoder();
  let buffer = '';
  let textOpen = false;
  let stopReason = 'end_turn';
  let outputTokens = 0;
  const toolBlocks = new Map();
  let nextIndex = 1;

  let leadingBuffer = '';
  let leadingResolved = false;
  let strippingThinking = false;
  let thinkingTag = '';
  const LEADING_BUFFER_LIMIT = 4000;

  function stripLeadingThinkingFromStream(text) {
    if (leadingResolved) return text;
    leadingBuffer += text;
    if (leadingBuffer.length > LEADING_BUFFER_LIMIT) {
      const all = leadingBuffer;
      leadingResolved = true;
      strippingThinking = false;
      leadingBuffer = '';
      return all;
    }
    if (!strippingThinking) {
      const trimmed = leadingBuffer.replace(/^\s+/, '');
      if (!trimmed) return '';
      const openMatch = /^<(thought|thinking)>/i.exec(trimmed);
      if (openMatch) {
        strippingThinking = true;
        thinkingTag = openMatch[1];
      } else if (trimmed[0] !== '<' || trimmed.length > 12) {
        leadingResolved = true;
        leadingBuffer = '';
        return trimmed;
      } else {
        return '';
      }
    }
    const closeRe = new RegExp('</' + thinkingTag + '>', 'i');
    const closeMatch = closeRe.exec(leadingBuffer);
    if (closeMatch) {
      const after = leadingBuffer.slice(closeMatch.index + closeMatch[0].length).replace(/^\s+/, '');
      leadingResolved = true;
      strippingThinking = false;
      leadingBuffer = '';
      return after;
    }
    return '';
  }

  return new ReadableStream({
    async start(controller) {
      const send = (event, payload) => {
        controller.enqueue(encoder.encode(
          'event: ' + event + '\ndata: ' + JSON.stringify(payload) + '\n\n'));
      };

      send('message_start', { type: 'message_start', message: { role: 'assistant', content: [] } });

      const reader = upstreamBody.getReader();
      try {
        for (;;) {
          const { done, value } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });

          let cut;
          while ((cut = buffer.indexOf('\n')) >= 0) {
            const line = buffer.slice(0, cut).trim();
            buffer = buffer.slice(cut + 1);
            if (!line || line.indexOf('data:') !== 0) continue;
            const payload = line.slice(5).trim();
            if (!payload || payload === '[DONE]') continue;

            let chunk;
            try { chunk = JSON.parse(payload); } catch (e) { continue; }
            if (chunk.usage && chunk.usage.completion_tokens) outputTokens = chunk.usage.completion_tokens;

            const choice = (chunk.choices || [])[0];
            if (!choice) continue;
            const delta = choice.delta || {};

            if (delta.content) {
              const visible = stripLeadingThinkingFromStream(delta.content);
              if (visible) {
                if (!textOpen) {
                  send('content_block_start', { type: 'content_block_start', index: 0,
                       content_block: { type: 'text', text: '' } });
                  textOpen = true;
                }
                send('content_block_delta', { type: 'content_block_delta', index: 0,
                     delta: { type: 'text_delta', text: visible } });
              }
            }

            for (const call of (delta.tool_calls || [])) {
              const slot = call.index == null ? 0 : call.index;
              if (!toolBlocks.has(slot)) {
                const index = nextIndex++;
                toolBlocks.set(slot, index);
                send('content_block_start', { type: 'content_block_start', index: index,
                     content_block: { type: 'tool_use', id: call.id || ('call_' + index),
                                      name: (call.function && call.function.name) || '', input: {} } });
              }
              const argsChunk = call.function && call.function.arguments;
              if (argsChunk) {
                send('content_block_delta', {
                  type: 'content_block_delta', index: toolBlocks.get(slot),
                  delta: { type: 'input_json_delta', partial_json: argsChunk }
                });
              }
            }

            if (choice.finish_reason) stopReason = mapFinishReason(choice.finish_reason, toolBlocks.size > 0);
          }
        }
      } catch (err) {
        send('error', { type: 'error', error: { message: String((err && err.message) || err) } });
      }

      if (!leadingResolved && leadingBuffer) {
        const leftover = strippingThinking ? '' : leadingBuffer;
        leadingResolved = true;
        leadingBuffer = '';
        if (leftover) {
          if (!textOpen) {
            send('content_block_start', { type: 'content_block_start', index: 0,
                 content_block: { type: 'text', text: '' } });
            textOpen = true;
          }
          send('content_block_delta', { type: 'content_block_delta', index: 0,
               delta: { type: 'text_delta', text: leftover } });
        }
      }

      if (textOpen) send('content_block_stop', { type: 'content_block_stop', index: 0 });
      for (const index of toolBlocks.values()) {
        send('content_block_stop', { type: 'content_block_stop', index: index });
      }
      send('message_delta', { type: 'message_delta', delta: { stop_reason: stopReason },
                              usage: { output_tokens: outputTokens } });
      send('message_stop', { type: 'message_stop' });
      controller.close();
    }
  });
}

async function handleFallbackTest(env, request) {
  const chain = engineChain(env);
  if (!chain.length) {
    return json({ ok: false, engines: [], error: 'nothing configured' }, 200, env, request);
  }
  const engines = await Promise.all(chain.map(e => probeEngine(e, env)));
  return json({
    ok: engines.some(e => e.ok),
    all_ok: engines.every(e => e.ok),
    engines: engines,
    vendor: engines[0].vendor, model: engines[0].model,
    error: engines.some(e => e.ok) ? undefined
         : (engines[0].error || 'no engine answered')
  }, 200, env, request);
}

async function probeEngine(engine, env) {
  const base = { slot: engine.slot, vendor: engine.vendor, model: engine.model, label: engine.label };
  /* Workers AI has no endpoint to probe — it is a binding. Exercise it the
     same way it will actually be used, or TEST ENGINES would try to fetch an
     undefined URL and report the one always-available engine as broken. */
  if (engine.vendor === 'workers-ai') {
    if (!env || !env.AI) return { ...base, ok: false, reason: 'other', error: 'the AI binding is missing' };
    try {
      const out = await env.AI.run(engine.model, {
        messages: [{ role: 'user', content: 'Reply with the single word: ready' }],
        max_tokens: 16
      });
      const said = String((out && (out.response || out.result)) || '').trim();
      return said ? { ...base, ok: true, said: said.slice(0, 40) }
                  : { ...base, ok: false, reason: 'other', error: 'returned nothing' };
    } catch (err) {
      const msg = String((err && err.message) || err);
      return { ...base, ok: false,
               reason: /limit|quota|neuron/i.test(msg) ? 'no_credit' : 'other',
               error: /limit|quota|neuron/i.test(msg)
                 ? 'the daily free Neuron allowance is used up (resets 00:00 UTC)'
                 : msg.slice(0, 160) };
    }
  }
  let upstream;
  try {
    upstream = engine.vendor === 'anthropic'
      ? await fetch('https://api.anthropic.com/v1/messages', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'x-api-key': engine.key,
                     'anthropic-version': ANTHROPIC_VERSION },
          /* The cheapest model the router may use: this proves the key, and
             pinging the most expensive one to hear "ready" would be waste. */
          body: JSON.stringify({ model: (loadRegistry(env).find(m => m.enabled) || { id: 'claude-haiku-4-5' }).id, max_tokens: 16,
                                 messages: [{ role: 'user', content: 'Reply with the single word: ready' }] })
        })
      : await fetch(engine.url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + engine.key },
          body: JSON.stringify({ model: engine.model, max_tokens: 16,
                                 messages: [{ role: 'user', content: 'Reply with the single word: ready' }] })
        });
  } catch (err) {
    return { ...base, ok: false, reason: 'unreachable', error: 'could not reach ' + engine.url };
  }
  if (!upstream.ok) {
    const detail = await upstream.text().catch(() => '');
    const reason = failureReason(upstream.status, detail);
    return { ...base, ok: false, status: upstream.status, reason: reason,
      error: reason === 'key_rejected'  ? 'the key was rejected'
           : reason === 'no_credit'     ? 'no credit or quota left'
           : reason === 'rate_limited'  ? 'rate limited right now'
           : reason === 'no_such_model' ? 'this account has no model called ' + engine.model +
                                          ' — set ' + engine.modelVar + ' to one it does have'
           : detail.slice(0, 160) };
  }
  const data = await upstream.json().catch(() => ({}));
  const said = engine.vendor === 'anthropic'
    ? ((data.content || []).find(b => b.type === 'text') || {}).text || ''
    : ((((data.choices || [])[0] || {}).message || {}).content || '');
  return { ...base, ok: true, said: String(said).trim().slice(0, 40) };
}

const modelListCache = new Map();

const NOT_A_CHAT_MODEL = /whisper|tts|embed|rerank|moderation|guard|safety|vision-only|image|audio|transcribe|distil/i;

async function pickAvailableModel(engine) {
  const listUrl = engine.url.replace(/\/chat\/completions\/?$/, '/models');
  if (modelListCache.has(listUrl)) return modelListCache.get(listUrl);
  let ids = [];
  try {
    const res = await fetch(listUrl, { headers: { 'Authorization': 'Bearer ' + engine.key } });
    if (!res.ok) return null;
    const data = await res.json();
    ids = (data.data || data.models || [])
      .map(m => (typeof m === 'string' ? m : m.id))
      .filter(Boolean)
      .map(id => id.replace(/^models\//, ''))
      .filter(id => !NOT_A_CHAT_MODEL.test(id));
  } catch (e) { return null; }
  if (!ids.length) return null;

  if (engine.vendor === 'openrouter') {
    const free = ids.filter(id => /:free$/.test(id));
    if (free.length) ids = free;
  }
  let preferred;
  if (engine.vendor === 'google') {
    preferred = ids.filter(id => /^(models\/)?gemini/i.test(id));
    if (!preferred.length) preferred = ids.filter(id => /instruct|chat|versatile|it\b|oss/i.test(id));
  } else {
    preferred = ids.filter(id => /instruct|chat|versatile|it\b|oss/i.test(id));
  }
  const chosen = (preferred.length ? preferred : ids)[0];
  modelListCache.set(listUrl, chosen);
  return chosen;
}

function mcpConfig(env, slug) {
  const key = slug.toUpperCase().replace(/[^A-Z0-9]/g, '_');
  const url = env['MCP_' + key + '_URL'];
  if (!url) return null;
  return {
    url: url,
    apiKey: env['MCP_' + key + '_KEY'] || '',
    header: env['MCP_' + key + '_HEADER'] || 'x-api-key'
  };
}

async function handleMcpProxy(request, env, path) {
  const slug = path.slice('/mcp/'.length).split('/')[0];
  if (!slug) return json({ error: 'no MCP name in the path' }, 400, env, request);

  const config = mcpConfig(env, slug);
  if (!config) {
    return json({
      error: 'no MCP server configured under "' + slug + '". Add secrets MCP_' +
             slug.toUpperCase() + '_URL and MCP_' + slug.toUpperCase() + '_KEY.'
    }, 503, env, request);
  }

  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream' };
  if (config.apiKey) headers[config.header] = config.apiKey;

  const upstream = await fetch(config.url, {
    method: request.method === 'GET' ? 'GET' : 'POST',
    headers: headers,
    body: request.method === 'GET' ? undefined : await request.text()
  });

  const contentType = upstream.headers.get('Content-Type') || 'application/json';
  return new Response(upstream.body, {
    status: upstream.status,
    headers: { 'Content-Type': contentType, 'Cache-Control': 'no-store', ...cors(env, request) }
  });
}

const IMAGE_GEN_MODEL  = '@cf/black-forest-labs/flux-1-schnell';
const IMAGE_EDIT_MODEL = '@cf/black-forest-labs/flux-2-klein-9b';

function stripDataUrl(s) {
  return String(s || '').replace(/^data:[^;]+;base64,/, '');
}

async function toBase64Image(result) {
  if (!result) return null;
  if (typeof result === 'string') return result;
  if (result.image) return result.image;
  if (result instanceof ArrayBuffer || ArrayBuffer.isView(result)) {
    const bytes = new Uint8Array(result instanceof ArrayBuffer ? result : result.buffer);
    let bin = '';
    for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
    return btoa(bin);
  }
  if (typeof result.getReader === 'function') {
    const chunks = [];
    const reader = result.getReader();
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      chunks.push(value);
    }
    let total = 0;
    for (const c of chunks) total += c.length;
    const all = new Uint8Array(total);
    let off = 0;
    for (const c of chunks) { all.set(c, off); off += c.length; }
    let bin = '';
    for (let i = 0; i < all.length; i++) bin += String.fromCharCode(all[i]);
    return btoa(bin);
  }
  return null;
}

async function handleImage(request, env) {
  if (!env.AI) {
    return json({ error: 'Workers AI is not bound. In the dashboard: Settings -> Bindings -> Add -> Workers AI, variable name AI, then Deploy.' }, 503, env, request);
  }
  const body = await request.json().catch(() => ({}));
  const prompt = String((body && body.prompt) || '').trim().slice(0, 2000);
  if (!prompt) return json({ error: 'no prompt' }, 400, env, request);

  const reference = body && body.reference ? stripDataUrl(body.reference) : null;
  const steps = Math.min(8, Math.max(1, parseInt((body && body.steps) || 4, 10) || 4));

  const attempts = reference ? [
    { model: IMAGE_EDIT_MODEL, shape: 'image_b64[]', input: { prompt, image_b64: [reference] } },
    { model: IMAGE_EDIT_MODEL, shape: 'images[]',    input: { prompt, images: [reference] } },
    { model: IMAGE_EDIT_MODEL, shape: 'image',       input: { prompt, image: reference } },
    { model: IMAGE_EDIT_MODEL, shape: 'prompt only', input: { prompt } },
    { model: IMAGE_GEN_MODEL,  shape: 'fallback',    input: { prompt, steps } }
  ] : [
    { model: IMAGE_EDIT_MODEL, shape: 'prompt only', input: { prompt } },
    { model: IMAGE_GEN_MODEL,  shape: 'fallback',    input: { prompt, steps } }
  ];

  const tried = [];
  for (const attempt of attempts) {
    try {
      const result = await env.AI.run(attempt.model, attempt.input);
      const image = await toBase64Image(result);
      if (!image) { tried.push(attempt.shape + ': no image in response'); continue; }
      return json({
        ok: true,
        image: image,
        model: attempt.model,
        shape: attempt.shape,
        used_reference: !!reference && attempt.shape !== 'fallback' && attempt.shape !== 'prompt only',
        tried: tried
      }, 200, env, request);
    } catch (err) {
      tried.push(attempt.shape + ': ' + String((err && err.message) || err).slice(0, 160));
    }
  }
  return json({ error: 'every image attempt failed', tried: tried }, 502, env, request);
}

/* One store used to be the only shape this supported: two flat secrets, no
   name to address it by because there was nothing to disambiguate. Multiple
   stores need a name for each — "how's the AURA store doing" only means
   anything if something maps "AURA" to a subdomain and a token. SHOPIFY_STORES
   carries that list; the old two secrets still work and are folded in here,
   so nothing already configured breaks by this shipping. */
function shopifyStores(env) {
  const stores = [];
  const seen = new Set();

  if (env.SHOPIFY_STORES) {
    try {
      const parsed = JSON.parse(env.SHOPIFY_STORES);
      if (Array.isArray(parsed)) {
        for (const s of parsed) {
          const store = String((s && s.store) || '').trim();
          const token = String((s && s.token) || '').trim();
          const name = String((s && s.name) || store).trim();
          if (!store || !token || !name) continue;
          const key = name.toLowerCase();
          if (seen.has(key)) continue;
          seen.add(key);
          stores.push({ name, store, token });
        }
      }
    } catch (e) { /* malformed JSON: fall through, the single-store fields still work */ }
  }

  if (env.SHOPIFY_STORE && env.SHOPIFY_ADMIN_TOKEN) {
    const store = String(env.SHOPIFY_STORE).trim();
    const name = store;
    if (!seen.has(name.toLowerCase())) {
      seen.add(name.toLowerCase());
      stores.push({ name, store, token: String(env.SHOPIFY_ADMIN_TOKEN).trim() });
    }
  }

  return stores;
}

function findStore(env, name) {
  const stores = shopifyStores(env);
  if (!stores.length) return null;
  const want = String(name || '').trim().toLowerCase();
  if (!want) return stores.length === 1 ? stores[0] : null; // >1 store with no name is ambiguous, not a default
  return stores.find(s => s.name.toLowerCase() === want) || null;
}

/* ------------------------------ Speech to text -----------------------
   The browser's own SpeechRecognition is a different class of tool from what
   a phone's assistant uses: it is locked to one language at a time, its
   quality varies wildly between browsers, and on iOS Safari it is weak
   enough that a clear sentence often needs repeating. Whisper is a real ASR
   model — it identifies the language itself, which is the part that matters
   here, because "build me a landing page" spoken inside a Hebrew sentence is
   exactly what a single-language recogniser gets wrong.

   Runs on the Workers AI binding already used for images, so there is no new
   key and no new bill. Audio arrives as raw bytes in the request body.
--------------------------------------------------------------------- */

/* Order matters, and it changed. turbo is a DISTILLED model: fewer decoder
   layers, much faster, and the distillation costs the most in languages other
   than English — which is exactly where the complaints are. The full
   large-v3 goes first now and turbo becomes the fallback, trading a few
   hundred milliseconds for noticeably better Hebrew.

   If English ever starts feeling slow, swapping these two lines back is the
   whole change. */
const WHISPER_MODELS = [
  '@cf/openai/whisper-large-v3',          // full model: best non-English accuracy
  '@cf/openai/whisper-large-v3-turbo',    // distilled: faster, weaker on Hebrew
  '@cf/openai/whisper'                    // original, last resort
];

/* What Whisper says when it has nothing to say.
 
   Given silence, a fragment, or audio in a language it has been told not to
   expect, Whisper does not fail and does not return empty \u2014 it emits the
   phrase it saw most often in training. The list is small, famous, and almost
   entirely drawn from the end of YouTube videos, which is what the training
   data was.
 
   Filtered only for SHORT audio: if he genuinely says "thanks" in a long
   sentence it survives, and a short standalone "thank you" is not a command,
   so dropping it costs nothing and mis-hearing it costs a whole turn. */
const WHISPER_NOISE = [
  'thank you', 'thanks', 'thank you very much', 'thank you so much',
  'thanks for watching', 'thank you for watching', 'thanks for watching!',
  'please subscribe', 'subscribe', 'like and subscribe', 'see you next time',
  'bye', 'bye bye', 'goodbye', 'you', 'the', 'so', 'okay', 'ok', 'uh', 'um',
  'music', 'applause', 'silence', 'blank_audio', 'inaudible', 'foreign',
  '\u05ea\u05d5\u05d3\u05d4', '\u05ea\u05d5\u05d3\u05d4 \u05e8\u05d1\u05d4', '\u05ea\u05d5\u05d3\u05d4 \u05e9\u05e6\u05e4\u05d9\u05ea\u05dd',
  '\u05dc\u05d4\u05ea\u05e8\u05d0\u05d5\u05ea', '\u05e9\u05dc\u05d5\u05dd', '\u05db\u05df',
  'amara.org', 'subtitles by the amara.org community', 'www.amara.org'
];

/* Roughly a second and a half of opus. Under this, a result matching the list
   above is far more likely to be the noise than the words. */
const WHISPER_SHORT_BYTES = 14000;

function whisperBare(text) {
  return String(text || '')
    .toLowerCase()
    .replace(/[\[\]()*_~♪♩·]/g, ' ')
    .replace(/[.,!?;:…׳״'"-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/* The list, put through the same normaliser as the transcript.

   Written by hand it could not have worked: "amara.org" and "blank_audio"
   both lose their punctuation on the way in, so the entries as typed were
   unreachable and [BLANK_AUDIO] sailed straight through as a command.
   Normalising both sides is the only version of this that stays correct
   when someone adds a phrase with an apostrophe in it. */
const WHISPER_NOISE_SET = WHISPER_NOISE.map(whisperBare);

function isWhisperHallucination(text, byteLength) {
  const bare = whisperBare(text);
  if (!bare) return true;
  if (byteLength >= WHISPER_SHORT_BYTES) return false;   // long enough to be real speech
  return WHISPER_NOISE_SET.indexOf(bare) >= 0;
}

async function handleStt(request, env) {
  if (!env.AI) {
    return json({ error: 'Workers AI is not bound: add the AI binding in the dashboard' }, 503, env, request);
  }
  const buf = await request.arrayBuffer().catch(() => null);
  if (!buf || buf.byteLength < 800) {
    return json({ error: 'no audio', bytes: buf ? buf.byteLength : 0 }, 400, env, request);
  }
  const bytes = new Uint8Array(buf);

  /* The two models want the audio in different shapes — the turbo one takes
     base64, the original takes a plain byte array. Rather than pin a guess,
     try each in the order we prefer them and report which worked. */
  let b64 = '';
  for (let i = 0; i < bytes.length; i += 8192) {
    b64 += String.fromCharCode.apply(null, bytes.subarray(i, i + 8192));
  }
  b64 = btoa(b64);

  /* THE LANGUAGE, AND WHY IT IS NO LONGER A LOCK.
 
     Every transcription used to arrive carrying ?language=he, because that is
     what the interface was set to. Say "open youtube" in English into a
     Hebrew-locked Whisper and it does not return empty and it does not return
     English — it returns, with complete confidence, its single most common
     training phrase. Usually "Thank you." So the assistant was answering
     "you're welcome" to a request to open YouTube, and the fallbacks below
     never ran, because a hallucination is not an empty string.
 
     This user speaks both languages and mixes them inside one sentence; his
     own prompt says so. Locking either one is wrong for him by construction.
 
     So: detect first, and keep the hint only as a second opinion for when
     detection comes back with nothing usable. */
  const url = new URL(request.url);
  const hint = url.searchParams.get('language');
  const lang = (hint === 'he' || hint === 'en') ? hint : null;
  const withLang = (input) => lang ? { ...input, language: lang } : input;

  /* THE WORDS HE ACTUALLY SAYS.

     Whisper takes a prompt of expected vocabulary and leans towards it.
     Without one it has never heard of this user's world, so "Shopify" comes
     back as "shopfly", "TikTok" as "tick tock", and his own assistant's name
     as almost anything. These are the words that appear in his commands more
     than any others, in both scripts, and naming them costs nothing.

     Add to it without touching this file by setting STT_VOCAB on the worker:
     product names, a brand, a supplier — whatever he says that a general
     model would not expect.

     Only the turbo model documents initial_prompt, so only it is given one,
     and a plain attempt at the same model follows in case it is refused. */
  const vocab = ('JARVIS, Shopify, Instagram, TikTok, Claude, Cloudflare, API, ' +
                 'GlowPulse, \u05d2\u05f3\u05e8\u05d5\u05d5\u05d9\u05e1, \u05e9\u05d5\u05e4\u05d9\u05e4\u05d9\u05d9, \u05d0\u05d9\u05e0\u05e1\u05d8\u05d2\u05e8\u05dd, \u05d8\u05d9\u05e7\u05d8\u05d5\u05e7, \u05e7\u05dc\u05d0\u05d5\u05d3' +
                 (env.STT_VOCAB ? ', ' + String(env.STT_VOCAB).slice(0, 400) : '') + '.');
  const withVocab = (input) => ({ ...input, initial_prompt: vocab });

  const attempts = [
    { model: WHISPER_MODELS[0], input: { audio: b64 } },                          // detect
    { model: WHISPER_MODELS[1], input: withVocab({ audio: b64 }) },               // detect, told his words
    { model: WHISPER_MODELS[1], input: { audio: b64 } },                          // detect, plain
    { model: WHISPER_MODELS[0], input: withLang({ audio: b64 }) },                // second opinion
    { model: WHISPER_MODELS[2], input: { audio: [...bytes] } },
    { model: WHISPER_MODELS[1], input: withLang(withVocab({ audio: [...bytes] })) }
  ];

  const tried = [];
  let firstDropped = null;
  for (const attempt of attempts) {
    try {
      const out = await env.AI.run(attempt.model, attempt.input);
      const text = ((out && (out.text || out.transcription || '')) || '').trim();
      if (!text) { tried.push(attempt.model + ': empty result'); continue; }
      if (isWhisperHallucination(text, bytes.length)) {
        /* Not a transcript \u2014 the noise Whisper makes when it has nothing.
           Treated as silence so the next model gets a turn, and reported, so
           this never becomes invisible again. */
        if (!firstDropped) firstDropped = text;
        tried.push(attempt.model + ': hallucination (' + text.slice(0, 40) + ')');
        continue;
      }
      return json({
        ok: true, text: text, model: attempt.model,
        detected: out && out.language ? out.language : null,
        dropped: firstDropped
      }, 200, env, request);
    } catch (err) {
      tried.push(attempt.model + ': ' + String((err && err.message) || err).slice(0, 140));
    }
  }
  // Silence is a legitimate outcome, not a failure — the caller just ignores it.
  return json({ ok: true, text: '', tried: tried, dropped: firstDropped }, 200, env, request);
}

/* Searching, without needing Anthropic.

   The page already had a search tool, but it was Anthropic's server-side one:
   the worker notices a server tool in the request, puts an Anthropic engine
   first, and if there is no ANTHROPIC_API_KEY the tool is dropped from the
   request on its way to Google or Groq. Nothing errors. He simply answers
   from memory as though he had searched, which is worse than having no search
   at all, because there is no sign it did not happen.

   This is an ordinary endpoint, so a tool built on it survives on every
   engine in the chain. DuckDuckGo's HTML endpoint is used because it needs no
   key and no account — the cost is that it is markup meant for a browser
   rather than an API, so when the parse finds nothing the honest answer is
   "search returned nothing usable", never an empty list dressed up as "no
   results", which the model would report to him as fact. */
const SEARCH_TIMEOUT_MS = 10000;
const SEARCH_MAX_RESULTS = 8;

function decodeEntities(s) {
  return String(s || '')
    .replace(/&nbsp;/gi, ' ').replace(/&amp;/gi, '&')
    .replace(/&lt;/gi, '<').replace(/&gt;/gi, '>')
    .replace(/&quot;/gi, '"').replace(/&#0?39;|&#x27;/gi, "'")
    .replace(/&#(\d+);/g, (_, n) => String.fromCharCode(Number(n)));
}
function stripTags(s) { return decodeEntities(String(s || '').replace(/<[^>]+>/g, '')).replace(/\s+/g, ' ').trim(); }

/* DuckDuckGo wraps every hit in a redirect of its own, with the real address
   in uddg=. Unwrapped here rather than handed over as-is, so the model reads
   and quotes the actual domain instead of a duckduckgo.com link. */
function unwrapDuck(href) {
  try {
    const u = new URL(href, 'https://duckduckgo.com');
    const real = u.searchParams.get('uddg');
    if (real) return real;
    return u.protocol === 'http:' || u.protocol === 'https:' ? u.toString() : null;
  } catch (e) { return null; }
}

async function handleSearch(request, env) {
  const body = await request.json().catch(() => ({}));
  const query = String((body && body.query) || '').trim().slice(0, 400);
  if (!query) return json({ error: 'no query' }, 400, env, request);

  const stop = new AbortController();
  const timer = setTimeout(() => stop.abort(), SEARCH_TIMEOUT_MS);
  let html = '';
  try {
    const upstream = await fetch('https://html.duckduckgo.com/html/?q=' + encodeURIComponent(query), {
      method: 'GET',
      signal: stop.signal,
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36',
        'Accept': 'text/html,application/xhtml+xml',
        'Accept-Language': 'en,he;q=0.9'
      }
    });
    clearTimeout(timer);
    if (!upstream.ok) {
      return json({ error: 'search is unavailable right now (' + upstream.status + ')' }, 502, env, request);
    }
    html = await upstream.text();
  } catch (err) {
    clearTimeout(timer);
    const aborted = String((err && err.name) || '') === 'AbortError';
    return json({ error: aborted ? 'the search timed out' : 'could not reach the search service' },
                502, env, request);
  }

  const results = [];
  const re = /<a[^>]+class="[^"]*result__a[^"]*"[^>]*href="([^"]+)"[^>]*>([\s\S]*?)<\/a>/gi;
  let m;
  while ((m = re.exec(html)) && results.length < SEARCH_MAX_RESULTS) {
    const url = unwrapDuck(decodeEntities(m[1]));
    const title = stripTags(m[2]);
    if (!url || !title) continue;
    if (results.some(r => r.url === url)) continue;
    results.push({ title, url, snippet: '' });
  }

  /* Snippets are in their own elements, in the same order as the links. */
  const snips = [...html.matchAll(/<a[^>]+class="[^"]*result__snippet[^"]*"[^>]*>([\s\S]*?)<\/a>/gi)]
    .map(x => stripTags(x[1]));
  results.forEach((r, i) => { if (snips[i]) r.snippet = snips[i].slice(0, 400); });

  if (!results.length) {
    /* Said plainly. An empty list would be reported to him as "nothing exists
       about that", which is a different and false claim. */
    return json({ ok: false, query: query, results: [],
                  error: 'the search returned nothing this worker could read — treat it as search being unavailable, not as the topic having no results' },
                200, env, request);
  }
  return json({ ok: true, query: query, count: results.length, results: results }, 200, env, request);
}

/* Reading one page, as opposed to searching. web_search answers "what is out
   there" from an index; it cannot answer "what does THIS page say", which is
   what actually gets asked — a competitor's product page, a spec sheet, a
   supplier listing, a thread somebody linked. open_url puts such a page on
   his screen and deliberately tells the model nothing about it, so until now
   there was no way for him to read inside a site at all.

   Everything here is a guard, because this endpoint takes a URL from a model
   and fetches it with the worker's own network position:

   - http and https only. Anything else is a scheme with local reach.
   - No private, loopback or link-local host. A worker has no route to a home
     LAN, but "probably cannot" is not a security boundary, and the check is
     three lines.
   - Redirects are followed by fetch, so the FINAL url is re-checked before
     its body is used — an open redirect to 169.254.169.254 is the standard
     way this endpoint gets turned into a metadata reader.
   - A timeout and a byte cap, so one enormous or one hanging page cannot
     occupy the worker.

   HTML comes back as text, not markup: the model is reading prose, and tags
   would spend its context without adding anything. */
const FETCH_TIMEOUT_MS = 12000;
const FETCH_MAX_BYTES   = 3 * 1024 * 1024;
const FETCH_MAX_CHARS   = 18000;

function blockedHost(hostname) {
  const h = String(hostname || '').toLowerCase().replace(/^\[|\]$/g, '');
  if (!h) return true;
  if (h === 'localhost' || h.endsWith('.localhost') || h.endsWith('.local') ||
      h.endsWith('.internal') || h === '::1' || h === '0.0.0.0') return true;
  const v4 = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/.exec(h);
  if (v4) {
    const [a, b] = [Number(v4[1]), Number(v4[2])];
    if (a === 10 || a === 127 || a === 0) return true;
    if (a === 172 && b >= 16 && b <= 31) return true;
    if (a === 192 && b === 168) return true;
    if (a === 169 && b === 254) return true;          // cloud metadata
    if (a >= 224) return true;                        // multicast and above
  }
  if (/^(fc|fd|fe80)/.test(h)) return true;           // IPv6 private / link-local
  return false;
}

function safeUrl(raw) {
  let u;
  try { u = new URL(String(raw || '').trim()); } catch (e) { return null; }
  if (u.protocol !== 'http:' && u.protocol !== 'https:') return null;
  if (blockedHost(u.hostname)) return null;
  return u;
}

function htmlToText(html) {
  return String(html || '')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<(script|style|noscript|svg|canvas|template)\b[\s\S]*?<\/\1>/gi, ' ')
    .replace(/<\/(p|div|section|article|li|tr|h[1-6]|br)\s*>/gi, '\n')
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/gi, ' ').replace(/&amp;/gi, '&')
    .replace(/&lt;/gi, '<').replace(/&gt;/gi, '>')
    .replace(/&quot;/gi, '"').replace(/&#39;/gi, "'")
    .replace(/[ \t ]+/g, ' ')
    .replace(/\n\s*\n\s*\n+/g, '\n\n')
    .trim();
}

async function handleFetch(request, env) {
  const body = await request.json().catch(() => ({}));
  const target = safeUrl(body && body.url);
  if (!target) {
    return json({ error: 'give a full http or https address to a public page' }, 400, env, request);
  }

  const stop = new AbortController();
  const timer = setTimeout(() => stop.abort(), FETCH_TIMEOUT_MS);
  let upstream;
  try {
    upstream = await fetch(target.toString(), {
      signal: stop.signal,
      redirect: 'follow',
      headers: {
        /* Announced as a browser because a bare fetch is refused outright by
           a good share of the web, which would read here as "the page is
           empty" rather than "we were turned away". */
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36',
        'Accept': 'text/html,application/xhtml+xml,text/plain;q=0.9,*/*;q=0.5',
        'Accept-Language': 'en,he;q=0.9'
      }
    });
  } catch (err) {
    clearTimeout(timer);
    const aborted = String((err && err.name) || '') === 'AbortError';
    return json({ error: aborted ? 'the page took too long to answer' : 'could not reach that page' },
                502, env, request);
  }
  clearTimeout(timer);

  // Redirects have already been followed, so this is where the body came from.
  const landed = safeUrl(upstream.url || target.toString());
  if (!landed) return json({ error: 'that address redirected somewhere not allowed' }, 400, env, request);

  if (!upstream.ok) {
    return json({ error: 'the site answered ' + upstream.status, status: upstream.status,
                  url: landed.toString() }, 502, env, request);
  }

  const type = (upstream.headers.get('Content-Type') || '').toLowerCase();
  if (!/text\/html|text\/plain|application\/(xhtml|json|xml)|text\/xml/.test(type)) {
    return json({ error: 'that link is ' + (type.split(';')[0] || 'a file') + ', not a readable page',
                  url: landed.toString() }, 415, env, request);
  }

  const declared = Number(upstream.headers.get('Content-Length') || 0);
  if (declared && declared > FETCH_MAX_BYTES) {
    return json({ error: 'that page is too large to read', url: landed.toString() }, 413, env, request);
  }

  const raw = await upstream.text().catch(() => '');
  if (raw.length > FETCH_MAX_BYTES) {
    return json({ error: 'that page is too large to read', url: landed.toString() }, 413, env, request);
  }

  const titleMatch = /<title[^>]*>([\s\S]*?)<\/title>/i.exec(raw);
  const text = /json|xml/.test(type) ? raw.trim() : htmlToText(raw);
  const clipped = text.length > FETCH_MAX_CHARS;

  return json({
    ok: true,
    url: landed.toString(),
    title: titleMatch ? htmlToText(titleMatch[1]).slice(0, 300) : '',
    text: clipped ? text.slice(0, FETCH_MAX_CHARS) : text,
    truncated: clipped,
    chars: text.length
  }, 200, env, request);
}

/* The read-only guard, and it has to be exact: this endpoint holds an Admin
   API token with write scope, so whatever slips past here runs against a real
   store. The previous pair of patterns — /(^|\s)mutation\s/ and /^\s*mutation/ —
   required either a space after the keyword or the keyword at the very start
   of the string, and a parameterised mutation has neither:

     # anything\nmutation($id:ID!){ productDelete(input:{id:$id}){ ... } }

   The "(" defeats the first pattern and the leading comment defeats the
   second, so a destructive document was accepted as a read.

   Comments and strings are stripped first, because both can carry the word
   "mutation" harmlessly and both can hide one. Then any `mutation` token at
   the top level of the document is rejected, whatever follows it — a name, a
   variable list, a directive or the selection set itself. */
function containsMutation(query) {
  const stripped = String(query || '')
    .replace(/"""[\s\S]*?"""/g, ' ')   // block strings
    .replace(/"(?:\\.|[^"\\])*"/g, ' ')  // ordinary strings
    .replace(/#[^\n]*/g, ' ');         // comments
  return /\bmutation\b/i.test(stripped);
}

async function handleShopify(request, env) {
  const stores = shopifyStores(env);
  if (!stores.length) {
    return json({ error: 'shopify not configured' }, 503, env, request);
  }
  const body = await request.json().catch(() => ({}));
  const query = String((body && body.query) || '');
  if (!query.trim()) return json({ error: 'empty query' }, 400, env, request);
  /* Writes are allowed now, at the owner's explicit choice, with no approval
     step in front of them. The guard did not go away though \u2014 it turned into
     a declaration. A caller that means to change something has to say so, so
     that a document which merely mentions the word cannot become a write by
     accident, and so a read path can never be widened into a write path by a
     prompt that talked its way into the query field.

     What replaces the gate is the record: every call through here is written
     to the action log in the app, with the document and its variables, which
     is what makes a change reversible by hand. */
  const isWrite = containsMutation(query);
  if (isWrite && body.allow_writes !== true) {
    return json({
      error: 'this document contains a mutation, but the caller did not ask for a write. ' +
             'Pass allow_writes: true to change the store.'
    }, 400, env, request);
  }

  const requestedName = (body && body.store_name) || '';
  const target = findStore(env, requestedName);
  if (!target) {
    return json({
      error: requestedName
        ? 'no store named "' + requestedName + '". Configured: ' + stores.map(s => s.name).join(', ')
        : 'more than one store is configured; pass store_name. Configured: ' + stores.map(s => s.name).join(', ')
    }, 400, env, request);
  }

  const storeSubdomain = target.store.replace(/\.myshopify\.com$/, '');
  const upstream = await fetch(
    `https://${storeSubdomain}.myshopify.com/admin/api/${SHOPIFY_API_VERSION}/graphql.json`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Shopify-Access-Token': target.token },
      body: JSON.stringify({ query, variables: (body && body.variables) || {} })
    }
  );
  const data = await upstream.json().catch(() => ({ error: 'bad shopify response' }));
  return json(data, upstream.ok ? 200 : 502, env, request);
}

async function googleAccessToken(env) {
  const res = await fetch('https://oauth2.googleapis.com/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      client_id: env.GOOGLE_CLIENT_ID,
      client_secret: env.GOOGLE_CLIENT_SECRET,
      refresh_token: env.GOOGLE_REFRESH_TOKEN,
      grant_type: 'refresh_token'
    })
  });
  const data = await res.json();
  if (!res.ok || !data.access_token) throw new Error('google auth failed: ' + JSON.stringify(data).slice(0, 200));
  return data.access_token;
}

function calendarConfigured(env) {
  return !!(env.GOOGLE_CLIENT_ID && env.GOOGLE_CLIENT_SECRET && env.GOOGLE_REFRESH_TOKEN);
}

async function handleCalendarUpcoming(request, env, url) {
  if (!calendarConfigured(env)) return json({ error: 'calendar not configured' }, 503, env, request);
  const days = Math.min(60, Math.max(1, parseInt(url.searchParams.get('days') || '7', 10) || 7));
  const token = await googleAccessToken(env);
  const now = new Date();
  const until = new Date(now.getTime() + days * 86400000);
  const api = new URL('https://www.googleapis.com/calendar/v3/calendars/primary/events');
  api.searchParams.set('timeMin', now.toISOString());
  api.searchParams.set('timeMax', until.toISOString());
  api.searchParams.set('singleEvents', 'true');
  api.searchParams.set('orderBy', 'startTime');
  api.searchParams.set('maxResults', '40');

  const res = await fetch(api.toString(), { headers: { Authorization: 'Bearer ' + token } });
  const data = await res.json();
  if (!res.ok) return json({ error: 'calendar read failed', detail: data }, 502, env, request);

  const events = (data.items || []).map(e => ({
    title: e.summary || '(no title)',
    start: (e.start && (e.start.dateTime || e.start.date)) || null,
    end: (e.end && (e.end.dateTime || e.end.date)) || null,
    allDay: !!(e.start && e.start.date && !e.start.dateTime),
    location: e.location || null,
    description: e.description ? String(e.description).slice(0, 300) : null,
    link: e.htmlLink || null
  }));
  return json({ days, count: events.length, events }, 200, env, request);
}

async function handleCalendarCreate(request, env) {
  if (!calendarConfigured(env)) return json({ error: 'calendar not configured' }, 503, env, request);
  const body = await request.json().catch(() => ({}));
  const title = String((body && body.title) || '').trim();
  const start = String((body && body.start) || '').trim();
  if (!title || !start) return json({ error: 'title and start are required' }, 400, env, request);

  const timeZone = String((body && body.timeZone) || 'Asia/Jerusalem');
  const allDay = /^\d{4}-\d{2}-\d{2}$/.test(start);
  let end = String((body && body.end) || '').trim();
  if (!end) {
    if (allDay) {
      const d = new Date(start + 'T00:00:00Z');
      d.setUTCDate(d.getUTCDate() + 1);
      end = d.toISOString().slice(0, 10);
    } else {
      end = new Date(new Date(start).getTime() + 60 * 60 * 1000).toISOString();
    }
  }

  const event = {
    summary: title,
    description: (body && body.description) || undefined,
    location: (body && body.location) || undefined,
    start: allDay ? { date: start } : { dateTime: start, timeZone },
    end: allDay ? { date: end } : { dateTime: end, timeZone }
  };

  const token = await googleAccessToken(env);
  const res = await fetch('https://www.googleapis.com/calendar/v3/calendars/primary/events', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token },
    body: JSON.stringify(event)
  });
  const data = await res.json();
  if (!res.ok) return json({ error: 'calendar write failed', detail: data }, 502, env, request);
  return json({
    ok: true,
    event: {
      title: data.summary,
      start: data.start && (data.start.dateTime || data.start.date),
      end: data.end && (data.end.dateTime || data.end.date),
      link: data.htmlLink
    }
  }, 200, env, request);
}

/* =====================================================================
   Claude model router
   =====================================================================

   Every request used to go to one hard-coded model, whether it was "hi" or
   "rewrite the whole checkout flow". This section picks, per request, the
   least expensive Claude model that can still do that particular job at the
   required quality — and climbs to a stronger one when it could not.

     request ─► analyzeTask ─► requirement ─► candidates ─► estimate ─► score
             ─► route ─► Claude ─► evaluateResponse ─► ok? return : escalate

   Nothing about which model wins is written into the code. The models live
   in ROUTER_DEFAULT_REGISTRY, and every field of it can be changed from the
   environment without touching this file:

     ROUTER                 "off" to send every request to the model the page
                            asked for, exactly as before. Anything else = on.
     ROUTER_MODELS          comma list of model ids that are available, e.g.
                            "claude-haiku-4-5,claude-sonnet-5,claude-opus-5".
                            Replaces the registry's own enabled flags.
     ROUTER_REGISTRY        JSON: an array of model entries, merged by id onto
                            the defaults (new ids are added). A new Claude
                            release is one entry here, not a code change.
     ROUTER_TASKS           JSON: per-task overrides of the task profiles
                            (required capability, output estimate, minQuality).
     ROUTER_WEIGHTS         JSON: { quality, cost, tokens, latency, risk }.
     ROUTER_MIN_QUALITY     the floor no choice may fall under (default 0.8).
     ROUTER_SUFFICIENT_QUALITY  quality past which a stronger model earns no
                            more credit (default 0.95), so it cannot outbid a
                            cheaper model that already does the job.
     ROUTER_TOKEN_BUDGET    JSON: { maxCostUsd, maxTotalTokens } per request.
     ROUTER_KV              optional KV binding: what the router has learned
                            survives the isolate being recycled.

   Endpoints (all behind the session token):
     GET  /router/stats     decisions, token and cost savings, per-model health
     GET  /router/registry  the registry exactly as the router sees it now
     POST /router/explain   a messages body → the decision, without calling
     POST /router/feedback  { id, rating: "good" | "bad" } for a routed reply
   ===================================================================== */

const ROUTER_DEFAULT_REGISTRY = [
  {
    id: 'claude-haiku-4-5', name: 'Claude Haiku 4.5', family: 'haiku', version: '4.5',
    enabled: true, contextWindow: 200000, maxOutput: 64000,
    pricing: { input: 1.00, output: 5.00, cacheRead: 0.10, cacheWrite: 1.25 },
    capability: 58,
    capabilities: ['vision', 'tools', 'web_search', 'mcp', 'sampling', 'forced_tool_choice'],
    thinking: 'none', effortLevels: [],
    recommendedTasks: ['chit_chat', 'simple_question', 'translation', 'summarization', 'extraction'],
    strengths: { chit_chat: 4, simple_question: 3, translation: 3 },
    tokenizerFactor: 1.0,
    latency: { ttftMs: 350, tokensPerSec: 160 },
    budget: { minOutput: 64, maxOutput: 16000 }
  },
  {
    id: 'claude-sonnet-4-6', name: 'Claude Sonnet 4.6', family: 'sonnet', version: '4.6',
    /* Superseded by Sonnet 5, which is both stronger and cheaper. Kept so the
       page's own default is priced (it is the savings baseline) and so it can
       be switched back on with ROUTER_MODELS if an account lacks Sonnet 5. */
    enabled: false, contextWindow: 1000000, maxOutput: 128000,
    pricing: { input: 3.00, output: 15.00, cacheRead: 0.30, cacheWrite: 3.75 },
    capability: 74,
    capabilities: ['vision', 'tools', 'web_search', 'mcp', 'sampling', 'forced_tool_choice', 'effort'],
    thinking: 'optional', effortLevels: ['low', 'medium', 'high', 'max'],
    recommendedTasks: ['coding', 'data_analysis', 'creative_writing'],
    strengths: {},
    tokenizerFactor: 1.0,
    latency: { ttftMs: 600, tokensPerSec: 80 },
    budget: { minOutput: 64, maxOutput: 64000 }
  },
  {
    id: 'claude-sonnet-5', name: 'Claude Sonnet 5', family: 'sonnet', version: '5',
    enabled: true, contextWindow: 1000000, maxOutput: 128000,
    pricing: { input: 2.00, output: 10.00, cacheRead: 0.20, cacheWrite: 2.50 },
    capability: 82,
    capabilities: ['vision', 'tools', 'web_search', 'mcp', 'forced_tool_choice', 'effort', 'thinking'],
    thinking: 'adaptive-default', effortLevels: ['low', 'medium', 'high', 'xhigh', 'max'],
    recommendedTasks: ['coding', 'debugging', 'data_analysis', 'creative_writing', 'agentic', 'long_context', 'vision'],
    strengths: { coding: 3, agentic: 2, data_analysis: 2 },
    tokenizerFactor: 1.0,
    latency: { ttftMs: 650, tokensPerSec: 85 },
    budget: { minOutput: 64, maxOutput: 64000 }
  },
  {
    id: 'claude-opus-5', name: 'Claude Opus 5', family: 'opus', version: '5',
    enabled: true, contextWindow: 1000000, maxOutput: 128000,
    pricing: { input: 5.00, output: 25.00, cacheRead: 0.50, cacheWrite: 6.25 },
    capability: 91,
    capabilities: ['vision', 'tools', 'web_search', 'mcp', 'forced_tool_choice', 'effort', 'thinking'],
    thinking: 'adaptive-default', effortLevels: ['low', 'medium', 'high', 'xhigh', 'max'],
    recommendedTasks: ['complex_reasoning', 'debugging', 'coding', 'agentic'],
    strengths: { complex_reasoning: 2, debugging: 2, agentic: 2 },
    tokenizerFactor: 1.15,
    latency: { ttftMs: 1100, tokensPerSec: 55 },
    budget: { minOutput: 128, maxOutput: 64000 }
  },
  {
    id: 'claude-opus-5-5', name: 'Claude Opus 5.5', family: 'opus', version: '5.5',
    /* Off until switched on by name (ROUTER_MODELS), as it is still launching.
       Cheaper than Opus 5 and at least as strong, so once enabled it simply
       takes Opus 5's place in the ladder. */
    enabled: false, contextWindow: 1000000, maxOutput: 128000,
    pricing: { input: 4.00, output: 20.00, cacheRead: 0.20, cacheWrite: 5.00 },
    capability: 93,
    capabilities: ['vision', 'tools', 'web_search', 'mcp', 'effort', 'thinking'],
    thinking: 'always', effortLevels: ['low', 'medium', 'high', 'xhigh', 'max'],
    recommendedTasks: ['complex_reasoning', 'debugging', 'coding', 'agentic'],
    strengths: { complex_reasoning: 2, debugging: 2, agentic: 2 },
    tokenizerFactor: 1.15,
    latency: { ttftMs: 1100, tokensPerSec: 55 },
    budget: { minOutput: 128, maxOutput: 64000 }
  },
  {
    id: 'claude-fable-5-1', name: 'Claude Fable 5.1', family: 'fable', version: '5.1',
    enabled: true, contextWindow: 1000000, maxOutput: 128000,
    pricing: { input: 10.00, output: 50.00, cacheRead: 0.25, cacheWrite: 12.50 },
    capability: 100,
    capabilities: ['vision', 'tools', 'web_search', 'mcp', 'effort', 'thinking'],
    thinking: 'always', effortLevels: ['low', 'medium', 'high', 'xhigh', 'max'],
    recommendedTasks: ['complex_reasoning', 'agentic'],
    strengths: { complex_reasoning: 2 },
    tokenizerFactor: 1.15,
    latency: { ttftMs: 2500, tokensPerSec: 45 },
    budget: { minOutput: 256, maxOutput: 64000 }
  }
];

/* What each kind of task needs. `required` is the capability score at which
   the task is reliably done; complexity moves it from 5 below to 15 above.
   `output` is a typical reply length in tokens before complexity scales it. */
const ROUTER_TASK_PROFILES = {
  chit_chat:         { label: 'Small talk',              required: 40, output: 120,  why: 'Conversational reply, no reasoning load' },
  simple_question:   { label: 'Simple question',         required: 50, output: 300,  why: 'Short factual answer' },
  translation:       { label: 'Translation',             required: 54, output: 0,    why: 'Faithful translation; length follows the source' },
  summarization:     { label: 'Summarization',           required: 57, output: 450,  why: 'Condensing supplied text' },
  extraction:        { label: 'Structured extraction',   required: 62, output: 700,  why: 'Must return valid structured output' },
  vision:            { label: 'Image understanding',     required: 62, output: 400,  why: 'Needs to read an image' },
  creative_writing:  { label: 'Creative writing',        required: 64, output: 700,  why: 'Tone and style matter more than reasoning depth' },
  general:           { label: 'General request',         required: 62, output: 600,  why: 'Open request with no strong signal' },
  data_analysis:     { label: 'Data analysis',           required: 72, output: 900,  why: 'Numbers must be read and reasoned about correctly' },
  long_context:      { label: 'Long-context analysis',   required: 72, output: 900,  why: 'Must hold a large input in view at once' },
  coding:            { label: 'Coding',                  required: 74, output: 1500, why: 'Requires code generation that has to run' },
  agentic:           { label: 'Agentic / multi-step',    required: 76, output: 800,  why: 'Multi-step tool use; a wrong step compounds' },
  debugging:         { label: 'Code debugging',          required: 78, output: 1200, why: 'Requires code reasoning to find a root cause' },
  complex_reasoning: { label: 'Complex reasoning',       required: 84, output: 1500, why: 'Multi-step reasoning where shallow answers are wrong' }
};

const ROUTER_DEFAULT_WEIGHTS = { quality: 1.0, cost: 0.35, tokens: 0.10, latency: 0.10, risk: 0.50 };
const ROUTER_DEFAULT_MIN_QUALITY = 0.8;

/* Checked in order; the first type with the most hits wins. Hebrew has no \b
   (JS word boundaries are ASCII-only), so those terms are matched bare. */
const ROUTER_TASK_PATTERNS = [
  ['debugging',         /\b(debug|bug|error|exception|traceback|stack ?trace|crash(es|ed)?|doesn'?t work|not working|broken|fails?|failing|undefined is not|typeerror|syntaxerror|segfault|fix (this|it|the))\b|שגיאה|באג|לא עובד|קורס|נשבר|תתקן/i],
  ['coding',            /\b(code|function|class|script|implement|refactor|regex|api|endpoint|sql|javascript|typescript|python|html|css|react|rust|component|compile|unit tests?|write a program)\b|```|קוד|פונקציה|סקריפט|תכנת|תכתוב (לי )?(אפליקציה|אתר|תוכנה)/i],
  ['translation',       /\b(translate|translation|in (english|hebrew|spanish|french|german|arabic))\b|תרגם|תתרגם|תרגום|לאנגלית|לעברית/i],
  ['summarization',     /\b(summari[sz]e|summary|tl;?dr|key points|recap|condense)\b|סכם|תסכם|סיכום|תקציר|נקודות עיקריות/i],
  ['extraction',        /\b(extract|parse|as json|in json|json object|respond with only|csv of|fill (in )?the fields)\b|חלץ|תחלץ/i],
  ['data_analysis',     /\b(analy[sz]e (the )?(data|numbers|sales|metrics)|dataset|spreadsheet|statistics|revenue|conversion rate|kpi|trend|forecast|average|median|correlation|report on)\b|נתונים|מכירות|הכנסות|דוח|סטטיסטיק|המרה/i],
  ['creative_writing',  /\b(poem|story|lyrics|slogan|tagline|caption|ad copy|marketing copy|blog post|product description|write (me )?a (post|story|poem)|creative)\b|שיר|סיפור|סלוגן|כיתוב|פוסט|תיאור מוצר|קופי/i],
  ['complex_reasoning', /\b(prove|proof|derive|trade-?offs?|strategy|architecture|design a system|step by step|think (hard|carefully)|reason about|pros and cons|compare .* (and|vs\.?) |optimi[sz]e|root cause|why does|what would happen)\b|אסטרטגיה|ארכיטקטורה|תנתח לעומק|יתרונות וחסרונות|למה זה|שלב אחר שלב/i],
  ['agentic',           /\b(and then|after that|step \d|multi-?step|go through (all|every)|for each|automate|set up|deploy|build (me )?(a|an|the) (site|app|store|website|tool))\b|ואז|אחר כך|תבנה לי|תקים|אוטומצי/i]
];

const ROUTER_HARD_WORDS = /\b(complex|production|architecture|concurren|race condition|security|vulnerab|proof|algorithm|distributed|scalab|from scratch|end-to-end|entire|whole (app|codebase|project)|performance|memory leak|deadlock|edge cases?|rigorous)\w*|מורכב|ארכיטקטורה|אופטימיזציה|מאפס|כל הפרויקט|ביצועים|אבטחה/gi;
const ROUTER_EASY_WORDS = /\b(quick(ly)?|simple|short|brief(ly)?|one (line|word|sentence)|just tell me|yes or no|tl;?dr)\b|בקצרה|פשוט|מהר|במשפט אחד|כן או לא/i;
const ROUTER_GREETING = /^(?:\s*(?:hi|hey|hello|yo|thanks|thank you|good (morning|evening|night)|how are you|what'?s up|שלום|היי|הי|תודה|בוקר טוב|ערב טוב|לילה טוב|מה נשמע|מה קורה|מה שלומך)[\s!.?,]*)+$/i;

/* ---------------------------------------------------------------- state */

/* Lives as long as the isolate does, and is mirrored to ROUTER_KV when one is
   bound. Everything here is either a count or a short string: nothing from a
   conversation's content is ever kept. */
const routerState = {
  loaded: false,
  stats: {},            // "task|model" → { n, ok, fail, escalated, good, bad, retries, tokens, cost }
  models: {},           // model → { n, ok, fail, tokensIn, tokensOut, cost, latencyMs }
  totals: { requests: 0, tokens: 0, cost: 0, baselineCost: 0, topCost: 0, escalations: 0 },
  recent: [],           // last ROUTER_RECENT decisions, newest first
  byId: new Map(),      // decision id → decision (bounded)
  lastModel: new Map(), // conversation fingerprint → model id (cache + thinking continuity)
  seenTurns: new Map(), // conversation+turn fingerprint → { at, id } (retry detection)
  unavailable: new Map(), // model id → until (ms) after a 404 / "no such model"
  dirty: 0, lastSave: 0
};
const ROUTER_RECENT = 200;
const ROUTER_BY_ID = 500;
let routerCtx = null;

function routerLog(event, fields) {
  /* One JSON line per event: Cloudflare's log tail and Logpush both index it
     as-is. No message content, only what the decision was made from. */
  try { console.log(JSON.stringify(Object.assign({ at: new Date().toISOString(), component: 'router', event: event }, fields || {}))); }
  catch (e) {}
}

function parseJsonEnv(value, fallback) {
  if (value === undefined || value === null || value === '') return fallback;
  if (typeof value === 'object') return value;
  try { return JSON.parse(String(value)); }
  catch (err) { routerLog('config_error', { error: 'invalid JSON in router setting: ' + String(err.message || err) }); return fallback; }
}

function routerEnabled(env) {
  return String((env && env.ROUTER) || 'on').toLowerCase() !== 'off';
}

/* ------------------------------------------------------------- registry */

function loadRegistry(env) {
  env = env || {};
  const byId = new Map(ROUTER_DEFAULT_REGISTRY.map(m => [m.id, JSON.parse(JSON.stringify(m))]));
  const overrides = parseJsonEnv(env.ROUTER_REGISTRY, []);
  for (const o of Array.isArray(overrides) ? overrides : []) {
    if (!o || typeof o.id !== 'string') continue;
    const base = byId.get(o.id);
    if (base) {
      byId.set(o.id, Object.assign({}, base, o,
        { pricing: Object.assign({}, base.pricing, o.pricing || {}),
          latency: Object.assign({}, base.latency, o.latency || {}),
          budget: Object.assign({}, base.budget, o.budget || {}),
          strengths: Object.assign({}, base.strengths, o.strengths || {}) }));
    } else {
      byId.set(o.id, Object.assign({
        name: o.id, family: 'custom', version: '?', enabled: true, contextWindow: 200000, maxOutput: 64000,
        pricing: { input: 3, output: 15, cacheRead: 0.3, cacheWrite: 3.75 }, capability: 70,
        capabilities: ['vision', 'tools', 'web_search', 'mcp'], thinking: 'none', effortLevels: [],
        recommendedTasks: [], strengths: {}, tokenizerFactor: 1, latency: { ttftMs: 800, tokensPerSec: 70 },
        budget: { minOutput: 64, maxOutput: 64000 }
      }, o));
    }
  }
  const list = [...byId.values()];
  if (env.ROUTER_MODELS) {
    const allowed = String(env.ROUTER_MODELS).split(',').map(s => s.trim()).filter(Boolean);
    for (const m of list) m.enabled = allowed.indexOf(m.id) >= 0;
  }
  return list.sort((a, b) => a.capability - b.capability);
}

function loadTaskProfiles(env) {
  const out = JSON.parse(JSON.stringify(ROUTER_TASK_PROFILES));
  const o = parseJsonEnv(env && env.ROUTER_TASKS, {});
  for (const k of Object.keys(o || {})) out[k] = Object.assign({ label: k, required: 62, output: 600, why: '' }, out[k] || {}, o[k]);
  return out;
}

function routerSettings(env) {
  env = env || {};
  const weights = Object.assign({}, ROUTER_DEFAULT_WEIGHTS, parseJsonEnv(env.ROUTER_WEIGHTS, {}));
  const minQ = Number(env.ROUTER_MIN_QUALITY);
  const suffQ = Number(env.ROUTER_SUFFICIENT_QUALITY);
  const minQuality = isFinite(minQ) && minQ > 0 && minQ < 1 ? minQ : ROUTER_DEFAULT_MIN_QUALITY;
  return {
    weights: weights,
    minQuality: minQuality,
    sufficientQuality: isFinite(suffQ) && suffQ > minQuality && suffQ <= 1 ? suffQ : Math.max(0.95, minQuality),
    budget: Object.assign({ maxCostUsd: Infinity, maxTotalTokens: Infinity }, parseJsonEnv(env.ROUTER_TOKEN_BUDGET, {}))
  };
}

function hasCap(model, cap) { return (model.capabilities || []).indexOf(cap) >= 0; }

/* ------------------------------------------------------------- analysis */

function blockText(content) {
  if (typeof content === 'string') return content;
  if (!Array.isArray(content)) return '';
  return content.map(b => {
    if (!b) return '';
    if (b.type === 'text') return b.text || '';
    if (b.type === 'tool_result') return typeof b.content === 'string' ? b.content : blockText(b.content);
    if (b.type === 'tool_use' || b.type === 'server_tool_use') { try { return JSON.stringify(b.input || {}); } catch (e) { return ''; } }
    if (b.type === 'document' && b.source && typeof b.source.data === 'string' && b.source.type === 'text') return b.source.data;
    return '';
  }).join('\n');
}

/* ~3.6 characters per token for English and code; Hebrew and other non-Latin
   scripts tokenize closer to one token per 2 characters, so the estimate is
   blended by how much of the text is non-ASCII. Estimates, not billing — the
   real counts come back in `usage` and are what the stats record. */
function estimateTokens(text) {
  const s = String(text || '');
  if (!s) return 0;
  let wide = 0;
  for (let i = 0; i < s.length; i++) if (s.charCodeAt(i) > 127) wide++;
  const ratio = wide / s.length;
  return Math.ceil(s.length / (3.6 * (1 - ratio) + 2.0 * ratio));
}

function isToolResultTurn(msg) {
  return !!(msg && msg.role === 'user' && Array.isArray(msg.content) && msg.content.length &&
            msg.content.every(b => b && b.type === 'tool_result'));
}

function lastUserText(messages) {
  for (let i = messages.length - 1; i >= 0; i--) {
    const m = messages[i];
    if (m && m.role === 'user' && !isToolResultTurn(m)) {
      const t = typeof m.content === 'string' ? m.content
        : (m.content || []).filter(b => b && b.type === 'text').map(b => b.text || '').join('\n');
      if (t.trim()) return t;
    }
  }
  return '';
}

function fnv1a(str) {
  let h = 0x811c9dc5;
  const s = String(str || '');
  for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
  return h.toString(36);
}

function conversationKey(body) {
  const msgs = (body && body.messages) || [];
  const first = msgs.find(m => m && m.role === 'user');
  return fnv1a(systemText(body && body.system).slice(0, 400) + '\u0000' + blockText(first && first.content).slice(0, 400));
}

function analyzeTask(body, env) {
  body = body || {};
  const profiles = loadTaskProfiles(env);
  const messages = Array.isArray(body.messages) ? body.messages : [];
  const userText = lastUserText(messages);
  const system = systemText(body.system);

  const systemTokens = estimateTokens(system);
  const toolsTokens = estimateTokens(JSON.stringify(body.tools || []));
  let images = 0;
  let historyTokens = 0;
  for (const m of messages) {
    historyTokens += estimateTokens(blockText(m && m.content));
    if (m && Array.isArray(m.content)) {
      for (const b of m.content) {
        if (b && b.type === 'image') images++;
        if (b && b.type === 'tool_result' && Array.isArray(b.content)) images += b.content.filter(x => x && x.type === 'image').length;
      }
    }
  }
  const inputTokens = systemTokens + toolsTokens + historyTokens + images * 1600;
  const userTokens = estimateTokens(userText);

  /* How deep into a tool loop this turn is: consecutive tool_result turns at
     the end of the conversation. */
  let toolDepth = 0;
  for (let i = messages.length - 1; i >= 0; i--) {
    const m = messages[i];
    if (!m) continue;
    if (m.role === 'assistant') continue;
    if (isToolResultTurn(m)) toolDepth++; else break;
  }

  const serverTools = (body.tools || []).filter(t => t && t.type && !t.input_schema).map(t => String(t.type));
  const needs = {
    vision: images > 0,
    tools: Array.isArray(body.tools) && body.tools.length > 0,
    webSearch: serverTools.some(t => /^web_search/.test(t)),
    mcp: Array.isArray(body.mcp_servers) && body.mcp_servers.length > 0,
    forcedToolChoice: !!(body.tool_choice && (body.tool_choice.type === 'any' || body.tool_choice.type === 'tool')),
    sampling: body.temperature !== undefined || body.top_p !== undefined || body.top_k !== undefined,
    jsonOnly: /respond with only (a )?raw json|only a raw json|return only json|respond only with json/i.test(system)
  };

  /* ---- classify: count pattern hits on the user's words; the system prompt
     only votes for the JSON-extraction case, because every chat turn carries
     the same long persona prompt and it would otherwise win every vote. */
  const signals = [];
  let type = null;
  let best = 0;
  /* A pasted document votes with its own vocabulary — a sales report is full
     of "revenue" whatever is being asked about it. Past a couple of thousand
     characters only the ends are read, which is where the instruction is. */
  const ask = userText.length > 2400 ? userText.slice(0, 800) + '\n' + userText.slice(-800) : userText;
  for (const [t, re] of ROUTER_TASK_PATTERNS) {
    const g = new RegExp(re.source, re.flags.indexOf('g') >= 0 ? re.flags : re.flags + 'g');
    const hits = (ask.match(g) || []).length;
    if (hits > best) { best = hits; type = t; }
  }
  if (needs.jsonOnly && (!type || type === 'summarization')) { type = 'extraction'; signals.push('System prompt demands raw JSON output'); }
  if (!type) {
    if (ROUTER_GREETING.test(userText)) type = 'chit_chat';
    else if (images && userTokens < 60) type = 'vision';
    else if (userTokens <= 40 && toolDepth === 0) type = 'simple_question';
    else type = 'general';
  }
  if (type === 'simple_question' && ROUTER_GREETING.test(userText)) type = 'chit_chat';
  if (inputTokens > 60000 && ['coding', 'debugging', 'complex_reasoning', 'agentic', 'extraction'].indexOf(type) < 0) type = 'long_context';
  if (toolDepth >= 3 && ['chit_chat', 'simple_question', 'general'].indexOf(type) >= 0) type = 'agentic';

  /* ---- complexity, 0..1 */
  let c = 0.3;
  const hard = (ask.match(ROUTER_HARD_WORDS) || []).length;
  if (hard) { c += Math.min(0.5, hard * 0.15); signals.push(hard + ' difficulty marker' + (hard > 1 ? 's' : '') + ' in the request'); }
  if (ROUTER_EASY_WORDS.test(userText)) { c -= 0.2; signals.push('Request asks for something short or simple'); }
  if (userTokens > 1500) { c += 0.2; signals.push('Long request (~' + userTokens + ' tokens)'); }
  else if (userTokens > 400) { c += 0.1; }
  else if (userTokens < 25 && !images) { c -= 0.1; }
  const reqLines = (userText.match(/^\s*(?:[-*•]|\d+[.)])\s+/gm) || []).length;
  if (reqLines >= 5) { c += 0.15; signals.push(reqLines + ' separate requirements listed'); }
  if (/```|\bat .+:\d+:\d+|Traceback \(most recent call last\)/.test(userText)) { c += 0.1; signals.push('Contains code or a stack trace'); }
  if (messages.length > 24) c += 0.1;
  if (toolDepth >= 6) { c += 0.2; signals.push('Deep in a tool loop (' + toolDepth + ' tool rounds)'); }
  else if (toolDepth >= 3) { c += 0.1; signals.push('In a tool loop (' + toolDepth + ' tool rounds)'); }
  if ((body.max_tokens || 0) >= 10000) { c += 0.1; signals.push('Caller allowed a long reply (' + body.max_tokens + ' tokens)'); }
  if (inputTokens > 60000) { c += 0.15; }
  if (type === 'chit_chat') c = Math.min(c, 0.2);
  c = Math.max(0, Math.min(1, c));

  const profile = profiles[type] || profiles.general;
  const required = Math.round(profile.required - 5 + c * 20);

  let output = profile.output || Math.max(150, Math.round(userTokens * 1.15));
  output = Math.round(output * (0.6 + c * 1.2));
  if (body.max_tokens) output = Math.min(output, body.max_tokens);

  return {
    type: type, label: profile.label, why: profile.why,
    complexity: Math.round(c * 100) / 100,
    required: required,
    tokens: { input: inputTokens, system: systemTokens, tools: toolsTokens, user: userTokens, output: output },
    cacheablePrefix: system.length >= 4000 ? systemTokens + toolsTokens : 0,
    images: images, toolDepth: toolDepth, needs: needs, signals: signals,
    requestedModel: typeof body.model === 'string' ? body.model : null,
    maxTokens: body.max_tokens || 4096,
    conversation: conversationKey(body),
    turn: fnv1a(userText.slice(0, 2000))
  };
}

/* ----------------------------------------------------------- estimation */

function pickEffort(model, analysis) {
  if (!hasCap(model, 'effort') || !(model.effortLevels || []).length) return null;
  const c = analysis.complexity;
  let want = c < 0.35 ? 'low' : c < 0.65 ? 'medium' : 'high';
  if (c >= 0.85 && ['coding', 'debugging', 'agentic', 'complex_reasoning'].indexOf(analysis.type) >= 0) want = 'xhigh';
  const levels = model.effortLevels;
  if (levels.indexOf(want) >= 0) return want;
  if (want === 'xhigh' && levels.indexOf('high') >= 0) return 'high';
  return levels[0];
}

const ROUTER_THINKING_OVERHEAD = { low: 0.15, medium: 0.5, high: 1.0, xhigh: 1.6, max: 2.2 };

function learnedStats(type, modelId) {
  return routerState.stats[type + '|' + modelId] || null;
}

/* Beta-smoothed success rate with a prior worth nine good outcomes and one
   bad, so a single failure nudges and a run of them moves the decision. */
function reliabilityOf(type, modelId) {
  const s = learnedStats(type, modelId);
  const prior = { a: 9, b: 1 };
  if (!s) return { value: prior.a / (prior.a + prior.b), samples: 0 };
  const good = s.ok + 2 * (s.good || 0);
  const bad = s.fail + s.escalated + 2 * (s.bad || 0) + (s.retries || 0);
  return { value: (good + prior.a) / (good + bad + prior.a + prior.b), samples: s.n };
}

function expectedQuality(model, analysis) {
  const rel = reliabilityOf(analysis.type, model.id);
  /* What has been learned shifts the model's effective capability for THIS
     task type: up to +4 for a clean record, down to -12 for a bad one. */
  const learned = Math.max(-12, Math.min(4, (rel.value - 0.9) * 40));
  const bonus = ((model.strengths || {})[analysis.type] || 0) +
                ((model.recommendedTasks || []).indexOf(analysis.type) >= 0 ? 1 : 0);
  const margin = model.capability + bonus + learned - analysis.required;
  /* 0.88 at margin 0, 0.98 ten points above, under 0.8 three below: a model
     just short of the need is already a real risk, and headroom beyond ten
     points buys almost nothing — which is what keeps the router frugal. */
  const fit = 1 / (1 + Math.exp(-(margin + 8) / 4));
  /* And the observed success rate scales it directly: a model that has been
     failing this kind of task half the time is not a 0.9 model for it,
     however much headroom its capability score suggests. At the prior (no
     history) the factor is exactly 1. */
  const q = Math.min(1, fit * rel.value / 0.9);
  return { quality: q, margin: margin, learned: learned, reliability: rel };
}

function estimateFor(model, analysis, lastModel) {
  const f = model.tokenizerFactor || 1;
  const effort = pickEffort(model, analysis);
  const thinks = model.thinking === 'always' || model.thinking === 'adaptive-default';
  const input = Math.round(analysis.tokens.input * f);
  const visible = Math.round(analysis.tokens.output * f);
  const thinking = thinks ? Math.round(visible * (ROUTER_THINKING_OVERHEAD[effort || 'high'] || 1)) : 0;
  const output = visible + thinking;

  /* Prompt caches are per model. Staying on the model that served the last
     turn reads the long system prompt back at the cache price; switching
     writes it again at the cache-write price. Priced in, so the router does
     not flip-flop between two near-equal models and pay for it every turn. */
  const prefix = Math.min(input, Math.round(analysis.cacheablePrefix * f));
  const p = model.pricing;
  const prefixRate = !prefix ? 0 : (lastModel === model.id ? p.cacheRead : p.cacheWrite);
  const cost = ((input - prefix) * p.input + prefix * prefixRate + output * p.output) / 1e6;
  const latencyMs = (model.latency.ttftMs || 800) + output / Math.max(1, model.latency.tokensPerSec || 60) * 1000;

  return { input: input, output: output, thinking: thinking, total: input + output, cost: cost,
           latencyMs: Math.round(latencyMs), effort: effort, cachedPrefix: prefix, cacheHit: !!prefix && lastModel === model.id };
}

/* Hard requirements: a model that fails any of these cannot be picked at any
   price, and the reason is kept for the explanation. */
function disqualify(model, analysis, now) {
  if (!model.enabled) return 'not enabled';
  const until = routerState.unavailable.get(model.id);
  if (until && until > now) return 'unavailable on this account (recently returned "no such model")';
  const need = analysis.tokens.input * (model.tokenizerFactor || 1) + Math.min(analysis.maxTokens, model.maxOutput);
  if (need > model.contextWindow) return 'context window too small (' + Math.round(need / 1000) + 'k > ' + Math.round(model.contextWindow / 1000) + 'k)';
  if (analysis.needs.vision && !hasCap(model, 'vision')) return 'cannot read images';
  if (analysis.needs.tools && !hasCap(model, 'tools')) return 'no tool use';
  if (analysis.needs.webSearch && !hasCap(model, 'web_search')) return 'no web search tool';
  if (analysis.needs.mcp && !hasCap(model, 'mcp')) return 'no MCP connector';
  if (analysis.needs.forcedToolChoice && !hasCap(model, 'forced_tool_choice')) return 'rejects a forced tool_choice';
  if (analysis.needs.sampling && !hasCap(model, 'sampling')) return 'rejects temperature/top_p';
  return null;
}

/* ---------------------------------------------------------------- route */

function route(body, env, opts) {
  opts = opts || {};
  const now = opts.now || Date.now();
  const registry = opts.registry || loadRegistry(env);
  const settings = routerSettings(env);
  const w = settings.weights;
  const analysis = opts.analysis || analyzeTask(body, env);
  const lastModel = routerState.lastModel.get(analysis.conversation) || null;

  const rows = registry.map(model => {
    const reason = disqualify(model, analysis, now);
    const est = estimateFor(model, analysis, lastModel);
    const q = expectedQuality(model, analysis);
    return { model: model, excluded: reason, est: est, q: q };
  });
  const usable = rows.filter(r => !r.excluded);

  const decision = {
    id: 'r_' + now.toString(36) + Math.random().toString(36).slice(2, 7),
    at: new Date(now).toISOString(),
    task: analysis.type, taskLabel: analysis.label, complexity: analysis.complexity,
    required: analysis.required, minQuality: settings.minQuality,
    tokens: analysis.tokens, conversation: analysis.conversation, turn: analysis.turn,
    requestedModel: analysis.requestedModel,
    candidates: [], model: null, effort: null, ladder: [], reasons: [], explanation: '',
    estimate: null, baseline: null, savingsPct: 0, savingsVsTopPct: 0, sticky: false
  };

  if (!usable.length) {
    decision.reasons.push('No enabled Claude model meets the hard requirements');
    for (const r of rows) decision.reasons.push(r.model.name + ': ' + r.excluded);
    decision.explanation = decision.reasons.join('\n');
    return decision;
  }

  /* Each penalty is how much worse than the best candidate a model is, as a
     ratio: 0 for the cheapest, 0.5 at twice the price, 0.9 at ten times.
     Dividing by the most expensive instead would let one pricey model squash
     the gap between two cheap ones until 3× the cost barely registered. */
  for (const r of usable) r.meets = r.q.quality >= settings.minQuality;
  /* Measured against the qualified models only: a model that cannot do the
     job is not a price anyone can actually pay, and letting it set the scale
     would flatten every difference between the ones that can. */
  const ref = usable.some(r => r.meets) ? usable.filter(r => r.meets) : usable;
  const minCost = Math.min(...ref.map(r => r.est.cost)) || 1e-9;
  const minTokens = Math.min(...ref.map(r => r.est.total)) || 1;
  const minLatency = Math.min(...ref.map(r => r.est.latencyMs)) || 1;
  const worse = (best, x) => x > best ? 1 - best / x : 0;
  for (const r of usable) {
    r.withinBudget = r.est.cost <= settings.budget.maxCostUsd && r.est.total <= settings.budget.maxTotalTokens;
    r.penalties = {
      cost: w.cost * worse(minCost, r.est.cost),
      tokens: w.tokens * worse(minTokens, r.est.total),
      latency: w.latency * worse(minLatency, r.est.latencyMs),
      risk: w.risk * (1 - r.q.reliability.value)
    };
    /* Quality counts up to "sufficient" and no further: past it, a stronger
       model is the same answer at a higher price, so it must not outbid a
       cheaper model that already does the job. */
    r.utility = w.quality * Math.min(r.q.quality, settings.sufficientQuality) - r.penalties.cost - r.penalties.tokens - r.penalties.latency - r.penalties.risk;
  }

  /* Quality is a floor, not a weight: only models expected to clear it are
     scored against each other on cost, tokens, latency and risk. If none
     clears it, the strongest one available is used — never a cheaper model
     that is expected to fail. A token/cost budget narrows the qualified set
     but cannot push the choice below the floor. */
  let pool = usable.filter(r => r.meets && r.withinBudget);
  if (!pool.length) pool = usable.filter(r => r.meets);
  let chosen;
  if (pool.length) chosen = pool.slice().sort((a, b) => (b.utility - a.utility) || (b.q.quality - a.q.quality) || (a.est.cost - b.est.cost))[0];
  else chosen = usable.slice().sort((a, b) => (b.q.quality - a.q.quality) || (a.est.cost - b.est.cost))[0];

  /* Mid tool-loop, stay on the model that started the loop when it is still
     qualified: its thinking and its prompt cache belong to this exchange, and
     a hand-off halfway through a job is where work gets repeated. */
  if (analysis.toolDepth > 0 && lastModel && lastModel !== chosen.model.id) {
    const prev = usable.find(r => r.model.id === lastModel);
    if (prev && prev.meets) { chosen = prev; decision.sticky = true; }
  }

  /* Escalation ladder: the choice, then every stronger usable model in order
     of capability, then — only as a last resort, for when the stronger ones
     are down — the weaker ones from the top. */
  const stronger = usable.filter(r => r.model.capability > chosen.model.capability).sort((a, b) => a.model.capability - b.model.capability);
  const weaker = usable.filter(r => r.model.capability < chosen.model.capability && r !== chosen).sort((a, b) => b.model.capability - a.model.capability);
  decision.ladder = [chosen].concat(stronger, weaker).map(r => r.model.id);

  decision.model = chosen.model.id;
  decision.modelName = chosen.model.name;
  decision.effort = chosen.est.effort;
  decision.estimate = { inputTokens: chosen.est.input, outputTokens: chosen.est.output, thinkingTokens: chosen.est.thinking,
                        totalTokens: chosen.est.total, costUsd: round6(chosen.est.cost), latencyMs: chosen.est.latencyMs,
                        quality: round3(chosen.q.quality), cacheHit: chosen.est.cacheHit };
  decision.candidates = rows.map(r => ({
    model: r.model.id, excluded: r.excluded || null,
    quality: round3(r.q.quality), capability: r.model.capability, learned: round3(r.q.learned),
    costUsd: round6(r.est.cost), totalTokens: r.est.total, latencyMs: r.est.latencyMs,
    utility: r.utility === undefined ? null : round3(r.utility), meets: !!r.meets,
    penalties: r.penalties ? { cost: round3(r.penalties.cost), tokens: round3(r.penalties.tokens),
                               latency: round3(r.penalties.latency), risk: round3(r.penalties.risk) } : null
  }));

  /* Savings against what would have run without the router: the model the
     page asked for, when it is in the registry, else the strongest usable. */
  const regById = new Map(registry.map(m => [m.id, m]));
  const baseModel = regById.get(analysis.requestedModel) || usable.slice().sort((a, b) => b.model.capability - a.model.capability)[0].model;
  const baseEst = estimateFor(baseModel, analysis, lastModel);
  const top = usable.slice().sort((a, b) => b.model.capability - a.model.capability)[0];
  decision.baseline = { model: baseModel.id, costUsd: round6(baseEst.cost), totalTokens: baseEst.total };
  decision.savingsPct = baseEst.cost > 0 ? Math.round((1 - chosen.est.cost / baseEst.cost) * 100) : 0;
  decision.savingsVsTopPct = top.est.cost > 0 ? Math.round((1 - chosen.est.cost / top.est.cost) * 100) : 0;
  decision.topModel = top.model.id;
  decision.topCost = round6(top.est.cost);

  decision.reasons = explainReasons(analysis, chosen, rows, decision);
  decision.explanation = formatExplanation(decision);
  return decision;
}

function round3(x) { return Math.round(x * 1000) / 1000; }
function round6(x) { return Math.round(x * 1e6) / 1e6; }
function kTokens(n) { return n >= 1000 ? (Math.round(n / 100) / 10) + 'k' : String(n); }

function explainReasons(analysis, chosen, rows, decision) {
  const out = [analysis.why];
  for (const s of analysis.signals) out.push(s);
  out.push('Context size: ' + kTokens(analysis.tokens.input) + ' tokens' +
           (analysis.cacheablePrefix ? ' (' + kTokens(analysis.cacheablePrefix) + ' cacheable prefix' + (chosen.est.cacheHit ? ', warm on this model' : '') + ')' : ''));
  if (decision.sticky) out.push('Stays on ' + chosen.model.name + ' mid tool-loop to keep its thinking and cache');
  out.push(chosen.model.name + ' meets the required capability (' + chosen.model.capability +
           (chosen.q.learned ? (chosen.q.learned > 0 ? ' +' : ' ') + round3(chosen.q.learned) + ' learned' : '') +
           ' vs ' + analysis.required + ' needed), expected quality ' + round3(chosen.q.quality));
  for (const r of rows) {
    if (r === chosen) continue;
    if (r.excluded) {
      if (r.excluded !== 'not enabled') out.push(r.model.name + ' is excluded: ' + r.excluded);
      continue;
    }
    if (r.model.capability > chosen.model.capability) {
      const x = chosen.est.cost > 0 ? r.est.cost / chosen.est.cost : 0;
      out.push(r.model.name + ' would provide ' + (r.q.quality - chosen.q.quality < 0.05 ? 'similar' : 'higher') +
               ' quality (' + round3(r.q.quality) + ') at ' + (Math.round(x * 10) / 10) + '× the cost');
    } else if (!r.meets) {
      out.push(r.model.name + ' is below the required threshold (quality ' + round3(r.q.quality) + ' < ' + decision.minQuality + ')');
    } else {
      out.push(r.model.name + ' qualifies but scores lower overall (utility ' + round3(r.utility) + ' vs ' + round3(chosen.utility) + ')');
    }
  }
  return out;
}

function formatExplanation(d) {
  return [
    'Task: ' + d.taskLabel + ' (complexity ' + d.complexity + ', needs capability ≥ ' + d.required + ')',
    'Selected model: ' + d.modelName + ' (' + d.model + ')' + (d.effort ? ', effort ' + d.effort : ''),
    'Reason:'
  ].concat(d.reasons.map(r => '- ' + r)).concat([
    'Estimated cost: $' + d.estimate.costUsd.toFixed(5) + ' vs $' + d.baseline.costUsd.toFixed(5) + ' on ' + d.baseline.model,
    'Estimated savings: ' + d.savingsPct + '%' + (d.topModel && d.topModel !== d.baseline.model ? ' (' + d.savingsVsTopPct + '% vs ' + d.topModel + ')' : ''),
    'Escalation ladder: ' + d.ladder.join(' → ')
  ]).join('\n');
}

/* ------------------------------------------------------ request shaping */

/* The page writes one request for one model. What changes per model is done
   here, on a copy: the model id, effort, room for thinking, and anything the
   target model would reject. The original body is never touched, so an
   escalation starts from exactly what the page sent. */
function adaptBodyForModel(body, model, decision) {
  const out = Object.assign({}, body, { model: model.id });
  const thinks = model.thinking === 'always' || model.thinking === 'adaptive-default';

  const effort = model.id === decision.model ? decision.effort
    : pickEffort(model, { complexity: decision.complexity, type: decision.task });
  if (effort && hasCap(model, 'effort') && !(body.output_config && body.output_config.effort)) {
    out.output_config = Object.assign({}, body.output_config || {}, { effort: effort });
  }

  /* On a thinking model max_tokens covers the thinking too, so the page's
     reply budget would be quietly eaten by it. Headroom is added in
     proportion to the effort, capped for non-streamed requests so a long
     generation cannot time out. */
  let max = body.max_tokens || 4096;
  if (thinks) {
    const room = { low: 1.25, medium: 1.5, high: 2, xhigh: 2.5, max: 3 }[effort || 'high'] || 1.5;
    max = Math.round(max * room);
    if (!body.stream) max = Math.min(max, 16000);
  }
  out.max_tokens = Math.max(1, Math.min(max, model.maxOutput, (model.budget && model.budget.maxOutput) || Infinity));

  if (!hasCap(model, 'sampling')) { delete out.temperature; delete out.top_p; delete out.top_k; }
  if (!hasCap(model, 'effort') && out.output_config) {
    const oc = Object.assign({}, out.output_config); delete oc.effort;
    if (Object.keys(oc).length) out.output_config = oc; else delete out.output_config;
  }
  if (model.thinking === 'always' && out.thinking && out.thinking.type !== 'adaptive') delete out.thinking;

  /* A model that does not think cannot be handed thinking blocks from one
     that did; strip them from the history rather than have the turn refused. */
  if (!thinks && Array.isArray(out.messages)) {
    out.messages = out.messages.map(m => {
      if (!m || m.role !== 'assistant' || !Array.isArray(m.content)) return m;
      const kept = m.content.filter(b => !b || (b.type !== 'thinking' && b.type !== 'redacted_thinking'));
      return kept.length === m.content.length || !kept.length ? m : Object.assign({}, m, { content: kept });
    });
  }
  return out;
}

/* ------------------------------------------------- quality evaluation */

/* Only what can be checked without a second model call: a refusal, an empty
   reply, a reply whose whole budget went to thinking, and a JSON-only prompt
   answered with something that is not JSON. Each is a reply the user would
   have to retry by hand, which is what escalation saves them. */
function evaluateResponse(data, analysis) {
  if (!data || typeof data !== 'object') return { ok: false, reason: 'unparseable response' };
  if (data.type === 'error') return { ok: false, reason: 'error body' };
  if (data.stop_reason === 'refusal') return { ok: false, reason: 'refusal' };
  const content = Array.isArray(data.content) ? data.content : [];
  const text = content.filter(b => b && b.type === 'text').map(b => b.text || '').join('').trim();
  const acted = content.some(b => b && (b.type === 'tool_use' || b.type === 'server_tool_use' || b.type === 'mcp_tool_use'));
  if (!text && !acted) {
    return { ok: false, reason: data.stop_reason === 'max_tokens' ? 'token budget spent before any answer' : 'empty reply' };
  }
  if (analysis && analysis.needs && analysis.needs.jsonOnly && !acted) {
    const raw = text.replace(/^```(?:json)?\s*|\s*```$/g, '');
    try { JSON.parse(raw); } catch (e) { return { ok: false, reason: 'JSON was required and the reply is not valid JSON' }; }
  }
  return { ok: true };
}

/* ------------------------------------------------------------- metrics */

function statRow(type, model) {
  const k = type + '|' + model;
  return routerState.stats[k] || (routerState.stats[k] = { n: 0, ok: 0, fail: 0, escalated: 0, good: 0, bad: 0, retries: 0, tokens: 0, cost: 0 });
}
function modelRow(model) {
  return routerState.models[model] || (routerState.models[model] = { n: 0, ok: 0, fail: 0, tokensIn: 0, tokensOut: 0, cost: 0, latencyMs: 0 });
}

function costOfUsage(model, usage) {
  if (!model || !usage) return 0;
  const p = model.pricing;
  return ((usage.input_tokens || 0) * p.input +
          (usage.cache_creation_input_tokens || 0) * p.cacheWrite +
          (usage.cache_read_input_tokens || 0) * p.cacheRead +
          (usage.output_tokens || 0) * p.output) / 1e6;
}

function remember(decision) {
  routerState.recent.unshift(decision);
  if (routerState.recent.length > ROUTER_RECENT) routerState.recent.length = ROUTER_RECENT;
  routerState.byId.set(decision.id, decision);
  if (routerState.byId.size > ROUTER_BY_ID) routerState.byId.delete(routerState.byId.keys().next().value);
}

/* Called once per model attempt. `usage` is the real count from Anthropic
   when there is one; the estimate stands in only when there is not. */
function recordAttempt(decision, model, outcome, usage, latencyMs) {
  const s = statRow(decision.task, model.id);
  const m = modelRow(model.id);
  s.n++; m.n++;
  if (outcome === 'ok') { s.ok++; m.ok++; }
  else if (outcome === 'escalated') { s.escalated++; m.fail++; }
  else { s.fail++; m.fail++; }
  if (usage) {
    const tokens = (usage.input_tokens || 0) + (usage.cache_creation_input_tokens || 0) + (usage.cache_read_input_tokens || 0) + (usage.output_tokens || 0);
    const cost = costOfUsage(model, usage);
    s.tokens += tokens; s.cost += cost;
    m.tokensIn += (usage.input_tokens || 0) + (usage.cache_creation_input_tokens || 0) + (usage.cache_read_input_tokens || 0);
    m.tokensOut += usage.output_tokens || 0;
    m.cost += cost;
    decision.actual = decision.actual || { tokens: 0, costUsd: 0 };
    decision.actual.tokens += tokens;
    decision.actual.costUsd = round6(decision.actual.costUsd + cost);
  }
  if (latencyMs) m.latencyMs += latencyMs;
  routerState.dirty++;
}

function finishDecision(decision, finalModel, outcome) {
  decision.finalModel = finalModel || null;
  decision.outcome = outcome;
  const t = routerState.totals;
  t.requests++;
  if (decision.escalations) t.escalations += decision.escalations;
  const cost = decision.actual ? decision.actual.costUsd : (decision.estimate ? decision.estimate.costUsd : 0);
  const tokens = decision.actual ? decision.actual.tokens : (decision.estimate ? decision.estimate.totalTokens : 0);
  t.cost += cost; t.tokens += tokens;
  /* The baseline is re-priced with the real token counts where there are
     some, so "savings" compares like with like instead of estimate vs bill. */
  const ratio = decision.estimate && decision.estimate.costUsd > 0 ? cost / decision.estimate.costUsd : 1;
  t.baselineCost += decision.baseline ? decision.baseline.costUsd * ratio : cost;
  t.topCost += decision.topCost ? decision.topCost * ratio : cost;
  if (finalModel) routerState.lastModel.set(decision.conversation, finalModel);
  if (routerState.lastModel.size > 1000) routerState.lastModel.delete(routerState.lastModel.keys().next().value);
  routerLog('decision', { id: decision.id, task: decision.task, complexity: decision.complexity, model: decision.model,
                          final: finalModel, outcome: outcome, escalations: decision.escalations || 0,
                          est_cost: decision.estimate && decision.estimate.costUsd, actual: decision.actual || null,
                          savings_pct: decision.savingsPct });
  scheduleSave();
}

/* The same question asked again within three minutes, in the same
   conversation, means the last answer did not land. Counted against the
   model that gave it. */
function noteRetry(decision, now) {
  const key = decision.conversation + ':' + decision.turn;
  const prev = routerState.seenTurns.get(key);
  routerState.seenTurns.set(key, { at: now, id: decision.id });
  if (routerState.seenTurns.size > 1000) routerState.seenTurns.delete(routerState.seenTurns.keys().next().value);
  if (!prev || now - prev.at > 3 * 60 * 1000 || prev.id === decision.id) return false;
  const old = routerState.byId.get(prev.id);
  if (old && old.finalModel) { statRow(old.task, old.finalModel).retries++; decision.retryOf = old.id; return true; }
  return false;
}

function recordFeedback(id, rating) {
  const d = routerState.byId.get(id);
  if (!d || !d.finalModel) return { ok: false, error: 'unknown decision id' };
  const s = statRow(d.task, d.finalModel);
  const good = rating === 'good' || rating === 'up' || rating === 1 || rating === true || Number(rating) > 0;
  if (good) s.good++; else s.bad++;
  d.feedback = good ? 'good' : 'bad';
  routerState.dirty++;
  routerLog('feedback', { id: id, task: d.task, model: d.finalModel, rating: d.feedback });
  scheduleSave();
  return { ok: true, id: id, model: d.finalModel, task: d.task, rating: d.feedback };
}

async function loadRouterState(env) {
  if (routerState.loaded) return;
  routerState.loaded = true;
  if (!env || !env.ROUTER_KV) return;
  try {
    const saved = await env.ROUTER_KV.get('router-state', 'json');
    if (saved && saved.stats) {
      routerState.stats = Object.assign(saved.stats, routerState.stats);
      routerState.models = Object.assign(saved.models || {}, routerState.models);
      routerState.totals = Object.assign({}, saved.totals || {}, routerState.totals.requests ? routerState.totals : {});
    }
  } catch (err) { routerLog('kv_error', { op: 'get', error: String(err && err.message || err) }); }
  routerState.env = env;
}

function scheduleSave() {
  const env = routerState.env;
  if (!env || !env.ROUTER_KV) return;
  const now = Date.now();
  if (routerState.dirty < 20 && now - routerState.lastSave < 60 * 1000) return;
  routerState.dirty = 0; routerState.lastSave = now;
  const snapshot = JSON.stringify({ stats: routerState.stats, models: routerState.models, totals: routerState.totals, savedAt: new Date(now).toISOString() });
  const p = env.ROUTER_KV.put('router-state', snapshot).catch(err => routerLog('kv_error', { op: 'put', error: String(err && err.message || err) }));
  if (routerCtx && typeof routerCtx.waitUntil === 'function') routerCtx.waitUntil(p);
}

function routerStatsReport(env) {
  const t = routerState.totals;
  const saved = t.baselineCost - t.cost;
  return {
    enabled: routerEnabled(env),
    totals: {
      requests: t.requests, escalations: t.escalations,
      tokens: t.tokens, costUsd: round6(t.cost),
      baselineCostUsd: round6(t.baselineCost), savedUsd: round6(saved),
      savingsPct: t.baselineCost > 0 ? Math.round(saved / t.baselineCost * 100) : 0,
      vsStrongestPct: t.topCost > 0 ? Math.round((1 - t.cost / t.topCost) * 100) : 0
    },
    models: routerState.models,
    byTask: routerState.stats,
    recent: routerState.recent.slice(0, 50).map(d => ({
      id: d.id, at: d.at, task: d.task, complexity: d.complexity, model: d.model, final: d.finalModel,
      effort: d.effort, outcome: d.outcome, escalations: d.escalations || 0, savingsPct: d.savingsPct,
      estimate: d.estimate, actual: d.actual || null, feedback: d.feedback || null, explanation: d.explanation
    }))
  };
}

function resetRouterState() {
  routerState.stats = {}; routerState.models = {};
  routerState.totals = { requests: 0, tokens: 0, cost: 0, baselineCost: 0, topCost: 0, escalations: 0 };
  routerState.recent = []; routerState.byId.clear(); routerState.lastModel.clear();
  routerState.seenTurns.clear(); routerState.unavailable.clear();
  routerState.dirty = 0; routerState.loaded = false; routerState.env = null;
}

/* ------------------------------------------------ streaming usage tap */

/* Streams are handed to the page untouched, byte for byte; this only reads
   the usage figures as they pass so that a streamed reply is metered as
   accurately as a buffered one. */
function tapUsage(stream, onDone) {
  const decoder = new TextDecoder();
  let buf = '';
  const usage = {};
  let stop = null;
  let text = 0;
  const scan = (line) => {
    if (line.indexOf('data:') !== 0) return;
    let ev; try { ev = JSON.parse(line.slice(5).trim()); } catch (e) { return; }
    if (ev.type === 'message_start' && ev.message && ev.message.usage) Object.assign(usage, ev.message.usage);
    else if (ev.type === 'message_delta') { if (ev.usage) Object.assign(usage, ev.usage); if (ev.delta && ev.delta.stop_reason) stop = ev.delta.stop_reason; }
    else if (ev.type === 'content_block_delta' && ev.delta && ev.delta.type === 'text_delta') text += (ev.delta.text || '').length;
  };
  let done = false;
  const finish = () => { if (done) return; done = true; try { onDone(usage, stop, text); } catch (e) {} };
  return stream.pipeThrough(new TransformStream({
    transform(chunk, controller) {
      controller.enqueue(chunk);
      buf += decoder.decode(chunk, { stream: true });
      let nl;
      while ((nl = buf.indexOf('\n')) >= 0) { scan(buf.slice(0, nl).trim()); buf = buf.slice(nl + 1); }
    },
    flush() { if (buf.trim()) scan(buf.trim()); finish(); }
  }));
}

/* ------------------------------------------------------ the routed call */

function escalatable(attempt, detail) {
  /* A rejected key or an empty balance is the account, not the model: every
     rung would fail the same way, so the turn goes straight back to the
     engine chain, which fails over to the next vendor. */
  if (attempt.reason === 'key_rejected' || attempt.reason === 'no_credit') return false;
  if (attempt.retriable) return true;
  if (attempt.reason === 'no_such_model') return true;
  /* A 400 that is about this model's surface — a parameter it does not take —
     is a reason to try the next model, not to give up on the turn. */
  return /not supported|does not support|unsupported|thinking|effort|tool_choice|temperature|top_p|sampling|retention|not available/i.test(detail || '');
}

async function callAnthropicRouted(engine, body, env, request, announce, describedBy) {
  await loadRouterState(env);
  routerState.env = env;
  const registry = loadRegistry(env);
  const started = Date.now();
  let decision, analysis;
  try {
    analysis = analyzeTask(body, env);
    decision = route(body, env, { registry: registry, now: started, analysis: analysis });
  } catch (err) {
    /* A routing bug must never cost the user an answer: fall straight back
       to exactly what the page asked for. */
    routerLog('route_error', { error: String(err && err.stack || err).slice(0, 400) });
    return await callEngine(Object.assign({}, engine, { routed: true }), body, env, request, announce, describedBy);
  }
  if (!decision.model) {
    routerLog('no_candidate', { reasons: decision.reasons });
    return await callEngine(Object.assign({}, engine, { routed: true }), body, env, request, announce, describedBy);
  }
  noteRetry(decision, started);
  remember(decision);
  decision.escalations = 0;
  decision.attempts = [];

  const byId = new Map(registry.map(m => [m.id, m]));
  let last = null;

  for (let i = 0; i < decision.ladder.length; i++) {
    const model = byId.get(decision.ladder[i]);
    const shaped = adaptBodyForModel(body, model, decision);
    const t0 = Date.now();
    const sub = Object.assign({}, engine, { model: model.id, label: 'anthropic/' + model.id, routed: true });
    /* `announce` stays the chain's own: climbing from one Claude model to the
       next is not a vendor fallback and must not be reported to the page as
       one. That is what X-Jarvis-Escalated is for. */
    const attempt = await callEngine(sub, shaped, env, request, announce, describedBy);
    const elapsed = Date.now() - t0;

    if (!attempt.ok) {
      let detail = '';
      try { detail = JSON.stringify(await attempt.response.clone().json()); } catch (e) {}
      if (attempt.reason === 'no_such_model' || /not_found|no such model|model.*(not found|does not exist)|retention/i.test(detail)) {
        routerState.unavailable.set(model.id, Date.now() + 60 * 60 * 1000);
      }
      const next = escalatable(attempt, detail) && i < decision.ladder.length - 1;
      recordAttempt(decision, model, next ? 'escalated' : 'fail', null, elapsed);
      decision.attempts.push({ model: model.id, outcome: 'http_' + (attempt.reason || 'error') });
      routerLog('attempt_failed', { id: decision.id, model: model.id, reason: attempt.reason, escalate: next });
      last = attempt;
      if (!next) break;
      decision.escalations++;
      continue;
    }

    const res = attempt.response;
    const type = res.headers.get('Content-Type') || '';
    if (type.includes('text/event-stream') && res.body) {
      /* Once a stream has started it belongs to the page; it cannot be taken
         back and re-asked. It is metered on the way through and judged after
         the fact, so a streamed failure still teaches the next decision. */
      const headers = new Headers(res.headers);
      addRouteHeaders(headers, decision, model);
      const tapped = tapUsage(res.body, (usage, stop, textChars) => {
        const failed = stop === 'refusal' || (!textChars && stop === 'max_tokens');
        recordAttempt(decision, model, failed ? 'fail' : 'ok', usage, Date.now() - t0);
        finishDecision(decision, model.id, failed ? 'streamed_' + stop : 'ok');
      });
      return Object.assign({}, attempt, { response: new Response(tapped, { status: res.status, headers: headers }) });
    }

    const raw = await res.text();
    let data = null;
    try { data = JSON.parse(raw); } catch (e) {}
    const verdict = evaluateResponse(data, analysis);
    const usage = data && data.usage;
    if (!verdict.ok && i < decision.ladder.length - 1 && byId.get(decision.ladder[i + 1]).capability > model.capability) {
      recordAttempt(decision, model, 'escalated', usage, elapsed);
      decision.attempts.push({ model: model.id, outcome: 'inadequate: ' + verdict.reason });
      decision.escalations++;
      routerLog('escalate', { id: decision.id, from: model.id, to: decision.ladder[i + 1], reason: verdict.reason });
      last = Object.assign({}, attempt, { response: new Response(raw, { status: res.status, headers: res.headers }) });
      continue;
    }
    recordAttempt(decision, model, verdict.ok ? 'ok' : 'fail', usage, elapsed);
    decision.attempts.push({ model: model.id, outcome: verdict.ok ? 'ok' : 'accepted: ' + verdict.reason });
    finishDecision(decision, model.id, verdict.ok ? 'ok' : 'inadequate');
    const headers = new Headers(res.headers);
    addRouteHeaders(headers, decision, model);
    return Object.assign({}, attempt, { response: new Response(raw, { status: res.status, headers: headers }) });
  }

  finishDecision(decision, null, 'failed');
  return last || { ok: false, retriable: true, reason: 'other', engine: engine,
                   response: json({ error: 'no Claude model answered' }, 502, env, request) };
}

function addRouteHeaders(headers, decision, model) {
  headers.set('X-Jarvis-Route', decision.id);
  headers.set('X-Jarvis-Model', model.id);
  headers.set('X-Jarvis-Task', decision.task);
  if (decision.escalations) headers.set('X-Jarvis-Escalated', String(decision.escalations));
}

/* ------------------------------------------------------------ endpoints */

async function handleRouter(request, env, path) {
  await loadRouterState(env);
  if (path === '/router/stats') return json(routerStatsReport(env), 200, env, request);
  if (path === '/router/registry') {
    return json({ enabled: routerEnabled(env), settings: routerSettings(env), models: loadRegistry(env), tasks: loadTaskProfiles(env) }, 200, env, request);
  }
  if (request.method !== 'POST') return json({ error: 'use POST' }, 405, env, request);
  const body = await request.json().catch(() => null);
  if (!body) return json({ error: 'invalid JSON body' }, 400, env, request);
  if (path === '/router/explain') {
    const msg = typeof body.prompt === 'string' ? { messages: [{ role: 'user', content: body.prompt }], model: body.model, max_tokens: body.max_tokens } : body;
    const d = route(msg, env);
    return json(d, 200, env, request);
  }
  if (path === '/router/feedback') {
    const out = recordFeedback(String(body.id || ''), body.rating);
    return json(out, out.ok ? 200 : 404, env, request);
  }
  return json({ error: 'not found: ' + path }, 404, env, request);
}

export const __router = {
  ROUTER_DEFAULT_REGISTRY, ROUTER_TASK_PROFILES, loadRegistry, routerSettings, analyzeTask, route,
  estimateFor, expectedQuality, adaptBodyForModel, evaluateResponse, recordAttempt, finishDecision,
  recordFeedback, remember, routerStatsReport, resetRouterState, estimateTokens, formatExplanation,
  state: routerState
};
