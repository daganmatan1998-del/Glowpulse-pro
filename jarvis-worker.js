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
   ===================================================================== */

const WORKER_VERSION = '2.3.0';
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
  async fetch(request, env) {
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
      if (path.indexOf('/mcp/') === 0)             return await handleMcpProxy(request, env, path);
      if (path === '/calendar/upcoming')           return await handleCalendarUpcoming(request, env, url);
      if (path === '/calendar/create')             return await handleCalendarCreate(request, env);
      if (path === '/shopify/query')               return await handleShopify(request, env);
      if (path === '/fallback/test')               return await handleFallbackTest(env, request);
      if (path === '/session')                     return json({ ok: true }, 200, env, request);

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
    voice: !!(env.CARTESIA_API_KEY || env.AI),
    voice_via: env.CARTESIA_API_KEY ? 'cartesia' : (env.AI ? 'workers-ai' : false),
    model3d: !!env.MESHY_API_KEY,
    images: !!env.AI,
    stt: !!env.AI,
    stt_language_hint: true,   // present only on workers that accept ?language=
    fallback: chain.length > 1 ? chain[1].model : false,
    fallback_via: chain.length > 1 ? chain[1].vendor : false,
    mcp: Object.keys(env)
      .filter(k => /^MCP_[A-Z0-9_]+_URL$/.test(k))
      .map(k => k.slice(4, -4).toLowerCase()),
    shopify: shopifyStores(env).map(s => s.name),
    calendar: !!(env.GOOGLE_CLIENT_ID && env.GOOGLE_CLIENT_SECRET && env.GOOGLE_REFRESH_TOKEN),
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
  if (hasImage) {
    const seeing = chain.filter(e => e.vendor === 'anthropic' || engineSeesImages(e, env));
    if (seeing.length) {
      chain = seeing.concat(chain.filter(e => seeing.indexOf(e) < 0));
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
      const attempt = await callEngine(engine, body, env, request, group !== awake || i > 0);
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

async function callEngine(engine, body, env, request, announce) {
  /* Workers AI is a binding, not an endpoint: no fetch, no key, no streaming
     to convert. Handled up front so the HTTP path below stays untouched. */
  if (engine.vendor === 'workers-ai') return await callWorkersAI(engine, body, env, request, announce);
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
          body: JSON.stringify(body)
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
        return await callEngine(engine, body, env, request, announce);
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
async function callWorkersAI(engine, body, env, request, announce) {
  const messages = [];
  if (body.system) messages.push({ role: 'system', content: String(body.system) });
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

async function handleModel3d(request, env) {
  if (request.method !== 'POST') return json({ error: 'POST only' }, 405, env, request);
  if (!env.MESHY_API_KEY) {
    return json({ error: '3D generation is not configured: add MESHY_API_KEY to the worker' }, 503, env, request);
  }
  const body = await request.json().catch(() => ({}));
  const prompt = String((body && body.prompt) || '').trim().slice(0, 600);
  if (!prompt) return json({ error: 'no prompt' }, 400, env, request);

  const res = await fetch(MESHY_BASE, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ' + env.MESHY_API_KEY
    },
    body: JSON.stringify({
      mode: 'preview',
      prompt: prompt,
      art_style: body.style === 'sculpture' ? 'sculpture' : 'realistic',
      should_remesh: true
    })
  });

  const text = await res.text();
  if (!res.ok) {
    return json({ error: 'meshy ' + res.status + ': ' + text.slice(0, 300) }, 502, env, request);
  }
  let data; try { data = JSON.parse(text); } catch (err) { data = {}; }
  const taskId = data.result || data.id;
  if (!taskId) return json({ error: 'meshy did not return a task id: ' + text.slice(0, 200) }, 502, env, request);
  return json({ taskId: taskId }, 200, env, request);
}

async function handleModel3dStatus(request, env) {
  if (!env.MESHY_API_KEY) {
    return json({ error: '3D generation is not configured' }, 503, env, request);
  }
  const id = new URL(request.url).searchParams.get('id');
  if (!id) return json({ error: 'missing id' }, 400, env, request);

  const res = await fetch(MESHY_BASE + '/' + encodeURIComponent(id), {
    headers: { 'Authorization': 'Bearer ' + env.MESHY_API_KEY }
  });
  const text = await res.text();
  if (!res.ok) {
    return json({ error: 'meshy ' + res.status + ': ' + text.slice(0, 300) }, 502, env, request);
  }
  let data; try { data = JSON.parse(text); } catch (err) { data = {}; }

  const status = String(data.status || '').toUpperCase();
  const urls = data.model_urls || {};
  return json({
    status: status,
    progress: typeof data.progress === 'number' ? data.progress : 0,
    glb: status === 'SUCCEEDED' ? (urls.glb || null) : null,
    thumbnail: data.thumbnail_url || null,
    error: status === 'FAILED'
      ? ((data.task_error && data.task_error.message) || 'generation failed')
      : null
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
      else if (block.type === 'image') pieces.push('[the user attached an image, which this model cannot view]');
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

function engineSeesImages(provider, env) {
  const vendor = (provider && provider.vendor) || '';
  const extra = String((env && env.VISION_ENGINES) || '')
    .split(',').map(x => x.trim().toLowerCase()).filter(Boolean);
  return vendor === 'google' || extra.indexOf(vendor) >= 0;
}

function toOpenAIRequest(body, env, provider) {
  provider = provider || fallbackProvider(env);
  const allowImages = engineSeesImages(provider, env);
  const messages = [];
  if (body.system) messages.push({ role: 'system', content: String(body.system) });

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
          body: JSON.stringify({ model: 'claude-sonnet-4-6', max_tokens: 16,
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

  /* A language hint, when the caller supplies one. Nothing was sent before,
     so Whisper guessed from a few seconds of audio — and language detection
     on a short utterance is unreliable. Guessing English on Hebrew speech
     produces confident nonsense, which is the failure being reported.

     A hint, not a lock: Whisper still transcribes English words inside a
     Hebrew sentence, which matters because that is how this user actually
     speaks. */
  const url = new URL(request.url);
  const hint = url.searchParams.get('language');
  const lang = (hint === 'he' || hint === 'en') ? hint : null;
  const withLang = (input) => lang ? { ...input, language: lang } : input;

  const attempts = [
    { model: WHISPER_MODELS[0], input: withLang({ audio: b64 }) },
    { model: WHISPER_MODELS[1], input: withLang({ audio: b64 }) },
    { model: WHISPER_MODELS[2], input: withLang({ audio: [...bytes] }) },
    { model: WHISPER_MODELS[1], input: { audio: [...bytes] } }   // last resort: no hint
  ];

  const tried = [];
  for (const attempt of attempts) {
    try {
      const out = await env.AI.run(attempt.model, attempt.input);
      const text = (out && (out.text || out.transcription || '')) || '';
      if (text.trim()) {
        return json({ ok: true, text: text.trim(), model: attempt.model }, 200, env, request);
      }
      tried.push(attempt.model + ': empty result');
    } catch (err) {
      tried.push(attempt.model + ': ' + String((err && err.message) || err).slice(0, 140));
    }
  }
  // Silence is a legitimate outcome, not a failure — the caller just ignores it.
  return json({ ok: true, text: '', tried: tried }, 200, env, request);
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
  if (containsMutation(query)) {
    return json({ error: 'mutations are not allowed through this endpoint' }, 400, env, request);
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
