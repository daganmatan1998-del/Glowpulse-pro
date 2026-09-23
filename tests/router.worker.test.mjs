/* End-to-end: the real worker, driven through worker.fetch with the Anthropic
   API stubbed. Proves the routed model is what actually goes on the wire, that
   escalation keeps the original request, and that the endpoints answer. */
import { test, beforeEach, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import worker, { __router as R, __test } from '../jarvis-worker.js';
import { chat } from '../tools/router-scenarios.mjs';

const ENV = { JARVIS_PIN: '1234', JARVIS_TOKEN_SECRET: 'test-secret-test-secret', ANTHROPIC_API_KEY: 'sk-ant-test' };
const realFetch = globalThis.fetch;
let calls;
let replies; // model id → function(body) → Response

beforeEach(() => {
  R.resetRouterState(); __test.resetCooldown(); console.log = () => {};
  calls = []; replies = {};
  globalThis.fetch = async (url, init) => {
    const body = JSON.parse(init.body);
    calls.push({ url: String(url), body });
    const reply = replies[body.model] || replies['*'];
    if (!reply) return new Response(JSON.stringify({ type: 'error', error: { message: 'no stub' } }), { status: 500 });
    return reply(body);
  };
});
afterEach(() => { globalThis.fetch = realFetch; });

const ok = (text, usage = { input_tokens: 100, output_tokens: 20 }) => () =>
  new Response(JSON.stringify({ type: 'message', role: 'assistant', content: text === null ? [] : [{ type: 'text', text }], stop_reason: 'end_turn', usage }),
               { status: 200, headers: { 'Content-Type': 'application/json' } });

async function token() {
  const res = await worker.fetch(new Request('https://w/auth/pin', { method: 'POST', body: JSON.stringify({ pin: '1234' }) }), ENV);
  return (await res.json()).token;
}
async function post(path, body, env = ENV) {
  return worker.fetch(new Request('https://w' + path, { method: 'POST', headers: { 'X-Jarvis-Token': await token() }, body: JSON.stringify(body) }), env);
}
async function get(path, env = ENV) {
  return worker.fetch(new Request('https://w' + path, { headers: { 'X-Jarvis-Token': await token() } }), env);
}

test('a greeting goes out as Haiku, a coding request as Sonnet 5 with effort', async () => {
  replies['*'] = ok('fine');
  let res = await post('/v1/messages', chat('hi', { stream: false }));
  assert.equal(res.status, 200);
  assert.equal(calls[0].body.model, 'claude-haiku-4-5');
  assert.equal(calls[0].body.output_config, undefined);
  assert.equal(res.headers.get('X-Jarvis-Model'), 'claude-haiku-4-5');
  assert.equal(res.headers.get('X-Jarvis-Task'), 'chit_chat');
  assert.match(res.headers.get('X-Jarvis-Route'), /^r_/);

  res = await post('/v1/messages', chat('Write a JavaScript function that groups orders by email', { stream: false }));
  assert.equal(calls[1].body.model, 'claude-sonnet-5');
  assert.equal(calls[1].body.output_config.effort, 'low');
});

test('ROUTER=off sends exactly the model the page asked for', async () => {
  replies['*'] = ok('fine');
  await post('/v1/messages', chat('hi', { stream: false }), Object.assign({}, ENV, { ROUTER: 'off' }));
  assert.equal(calls[0].body.model, 'claude-sonnet-4-6');
  assert.equal(calls[0].body.output_config, undefined);
});

test('an overloaded model escalates to the next one with the same conversation', async () => {
  replies['claude-haiku-4-5'] = () => new Response('{"type":"error","error":{"type":"overloaded_error"}}', { status: 529 });
  replies['claude-sonnet-5'] = ok('answered by sonnet');
  const body = chat('hi', { stream: false });
  const res = await post('/v1/messages', body);
  assert.equal(res.status, 200);
  assert.deepEqual(calls.map(c => c.body.model), ['claude-haiku-4-5', 'claude-sonnet-5']);
  assert.deepEqual(calls[1].body.messages, body.messages, 'context preserved');
  assert.equal(calls[1].body.system, calls[0].body.system.length ? calls[1].body.system : body.system);
  assert.equal(res.headers.get('X-Jarvis-Escalated'), '1');
  const stats = await (await get('/router/stats')).json();
  assert.equal(stats.totals.escalations, 1);
  assert.equal(stats.byTask['chit_chat|claude-haiku-4-5'].escalated, 1);
});

test('an empty reply is judged inadequate and escalated; the good one is returned', async () => {
  replies['claude-haiku-4-5'] = ok(null);
  replies['claude-sonnet-5'] = ok('real answer');
  const res = await post('/v1/messages', chat('What is the capital of Peru?', { stream: false }));
  const data = await res.json();
  assert.equal(data.content[0].text, 'real answer');
  assert.equal(res.headers.get('X-Jarvis-Model'), 'claude-sonnet-5');
});

test('a JSON-only task answered with prose escalates to a model that returns JSON', async () => {
  replies['claude-haiku-4-5'] = ok('Sure! Here is the memory: profile...');
  replies['claude-sonnet-5'] = ok('Sure! Here is the memory: profile...');
  replies['claude-opus-5'] = ok('{"profile":{"about":["sells skincare"]}}');
  const res = await post('/v1/messages', { model: 'claude-sonnet-4-6', max_tokens: 1200,
    system: 'Respond with ONLY a raw JSON object, no markdown fences.', messages: [{ role: 'user', content: 'Transcripts: I sell skincare.' }] });
  assert.deepEqual(calls.map(c => c.body.model).slice(-2), ['claude-sonnet-5', 'claude-opus-5']);
  assert.equal(JSON.parse((await res.json()).content[0].text).profile.about[0], 'sells skincare');
});

test('a model the account does not have is skipped and remembered as unavailable', async () => {
  replies['claude-haiku-4-5'] = () => new Response('{"type":"error","error":{"type":"not_found_error","message":"model: claude-haiku-4-5"}}', { status: 404 });
  replies['claude-sonnet-5'] = ok('hi');
  await post('/v1/messages', chat('hi', { stream: false }));
  await post('/v1/messages', chat('hello', { stream: false }));
  assert.deepEqual(calls.map(c => c.body.model), ['claude-haiku-4-5', 'claude-sonnet-5', 'claude-sonnet-5']);
});

test('a rejected key does not burn through every model', async () => {
  replies['*'] = () => new Response('{"type":"error","error":{"type":"authentication_error"}}', { status: 401 });
  const res = await post('/v1/messages', chat('hi', { stream: false }));
  assert.equal(calls.length, 1);
  assert.equal(res.status, 502);
});

test('a 400 that is not about the model is returned, not escalated', async () => {
  replies['*'] = () => new Response('{"type":"error","error":{"type":"invalid_request_error","message":"messages: roles must alternate"}}', { status: 400 });
  const res = await post('/v1/messages', chat('hi', { stream: false }));
  assert.equal(calls.length, 1);
  assert.equal(res.status, 400);
});

test('a stream passes through byte for byte and is metered from its usage events', async () => {
  const sse = [
    'event: message_start', 'data: {"type":"message_start","message":{"usage":{"input_tokens":1200,"cache_read_input_tokens":800,"output_tokens":1}}}', '',
    'event: content_block_delta', 'data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}', '',
    'event: message_delta', 'data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":42}}', '', ''
  ].join('\n');
  replies['*'] = () => new Response(sse, { status: 200, headers: { 'Content-Type': 'text/event-stream' } });
  const res = await post('/v1/messages', chat('hi'));
  assert.equal(await res.text(), sse);
  const stats = await (await get('/router/stats')).json();
  const m = stats.models['claude-haiku-4-5'];
  assert.equal(m.tokensOut, 42);
  assert.equal(m.tokensIn, 2000);
  assert.ok(m.cost > 0);
  assert.equal(stats.recent[0].outcome, 'ok');
  assert.ok(stats.totals.savedUsd > 0, 'Haiku instead of Sonnet 4.6 is a saving');
});

test('/router/explain routes without calling any model', async () => {
  const res = await post('/router/explain', { prompt: 'Fix this TypeError in my checkout script' });
  const d = await res.json();
  assert.equal(calls.length, 0);
  assert.equal(d.task, 'debugging');
  assert.match(d.explanation, /Selected model:/);
});

test('/router/feedback records against the model that answered', async () => {
  replies['*'] = ok('Lima');
  const res = await post('/v1/messages', chat('What is the capital of Peru?', { stream: false }));
  const id = res.headers.get('X-Jarvis-Route');
  const fb = await (await post('/router/feedback', { id, rating: 'bad' })).json();
  assert.equal(fb.ok, true);
  assert.equal(fb.model, 'claude-haiku-4-5');
  assert.equal((await post('/router/feedback', { id: 'nope', rating: 'good' })).status, 404);
});

test('/router/* needs the session token', async () => {
  const res = await worker.fetch(new Request('https://w/router/stats'), ENV);
  assert.equal(res.status, 401);
});

test('/router/registry reflects ROUTER_MODELS', async () => {
  const env = Object.assign({}, ENV, { ROUTER_MODELS: 'claude-haiku-4-5' });
  const r = await (await get('/router/registry', env)).json();
  assert.deepEqual(r.models.filter(m => m.enabled).map(m => m.id), ['claude-haiku-4-5']);
});

test('/health reports the router and the models it may use', async () => {
  const h = await (await worker.fetch(new Request('https://w/health'), ENV)).json();
  assert.equal(h.router.enabled, true);
  assert.deepEqual(h.router.models, ['claude-haiku-4-5', 'claude-sonnet-5', 'claude-opus-5', 'claude-fable-5-1']);
  assert.equal(h.version, '2.4.0');
});

test('CORS exposes the routing headers to the page', async () => {
  replies['*'] = ok('hi');
  const res = await post('/v1/messages', chat('hi', { stream: false }));
  assert.match(res.headers.get('Access-Control-Expose-Headers'), /X-Jarvis-Model/);
});

test('when every Claude model fails, the chain falls through to the next vendor', async () => {
  replies['*'] = () => new Response('{"error":"down"}', { status: 503 });
  const env = Object.assign({}, ENV, { FALLBACK_API_KEY: 'gsk_test' });
  const orig = globalThis.fetch;
  globalThis.fetch = async (url, init) => {
    if (/groq/.test(String(url))) {
      calls.push({ url: String(url), body: JSON.parse(init.body) });
      return new Response(JSON.stringify({ choices: [{ message: { role: 'assistant', content: 'groq here' }, finish_reason: 'stop' }] }), { status: 200 });
    }
    return orig(url, init);
  };
  const res = await post('/v1/messages', chat('hi', { stream: false }), env);
  assert.equal(res.status, 200);
  assert.ok(calls.some(c => /groq/.test(c.url)));
  assert.equal(calls.filter(c => /anthropic/.test(c.url)).length, 4, 'each enabled Claude model tried once');
});
