/* Unit tests for the Claude model router's decision logic.
   Run: node --test tests/                                       */
import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { __router as R } from '../jarvis-worker.js';
import { chat, PERSONA } from '../tools/router-scenarios.mjs';

const quiet = console.log;
beforeEach(() => { R.resetRouterState(); console.log = () => {}; });
process.on('exit', () => { console.log = quiet; });

const route = (body, env = {}) => R.route(body, env);

/* ------------------------------------------------------------- registry */

test('registry is sorted weakest to strongest, with the defaults enabled', () => {
  const reg = R.loadRegistry({});
  const caps = reg.map(m => m.capability);
  assert.deepEqual(caps, caps.slice().sort((a, b) => a - b));
  assert.deepEqual(reg.filter(m => m.enabled).map(m => m.id),
    ['claude-haiku-4-5', 'claude-sonnet-5', 'claude-opus-5', 'claude-fable-5-1']);
});

test('ROUTER_MODELS decides availability, and the router only uses what is available', () => {
  const env = { ROUTER_MODELS: 'claude-sonnet-5,claude-opus-5' };
  assert.deepEqual(R.loadRegistry(env).filter(m => m.enabled).map(m => m.id), ['claude-sonnet-5', 'claude-opus-5']);
  const d = route(chat('hi'), env);
  assert.equal(d.model, 'claude-sonnet-5', 'without Haiku the cheapest qualified model is Sonnet');
});

test('a new model added through ROUTER_REGISTRY is used with no code change', () => {
  const env = { ROUTER_REGISTRY: JSON.stringify([{ id: 'claude-sonnet-6', name: 'Claude Sonnet 6', capability: 88,
    pricing: { input: 1.5, output: 7.5, cacheRead: 0.15, cacheWrite: 1.9 }, contextWindow: 1000000, maxOutput: 128000,
    capabilities: ['vision', 'tools', 'web_search', 'mcp', 'effort', 'thinking'], thinking: 'adaptive-default',
    effortLevels: ['low', 'medium', 'high', 'xhigh', 'max'] }]) };
  const d = route(chat('Write a JavaScript function that groups orders by customer and sums totals.'), env);
  assert.equal(d.model, 'claude-sonnet-6', 'stronger and cheaper than Sonnet 5, so it should win coding');
});

test('ROUTER_REGISTRY can re-price an existing model and the choice follows', () => {
  const env = { ROUTER_REGISTRY: JSON.stringify([{ id: 'claude-opus-5', pricing: { input: 0.2, output: 1.0 } }]) };
  const d = route(chat('Write a JavaScript function that groups orders by customer and sums totals.'), env);
  assert.equal(d.model, 'claude-opus-5');
  assert.equal(R.loadRegistry(env).find(m => m.id === 'claude-opus-5').pricing.cacheRead, 0.5, 'unspecified price fields keep their defaults');
});

test('a malformed registry override is ignored rather than breaking routing', () => {
  const d = route(chat('hi'), { ROUTER_REGISTRY: '{not json' });
  assert.equal(d.model, 'claude-haiku-4-5');
});

/* ------------------------------------------------------------- analysis */

test('task analysis recognises each task type, in English and Hebrew', () => {
  const cases = [
    ['hello!', 'chit_chat'], ['שלום, מה נשמע?', 'chit_chat'],
    ['What year did the Berlin wall fall?', 'simple_question'],
    ['תתרגם לאנגלית: המשלוח יצא היום', 'translation'],
    ['Summarize this thread for me', 'summarization'], ['תסכם לי את השיחה', 'summarization'],
    ['Write a python script that renames files', 'coding'], ['תכתוב לי קוד שמחשב הנחה', 'coding'],
    ['I get TypeError: undefined is not a function when I click buy', 'debugging'], ['יש לי שגיאה בקופה, זה לא עובד', 'debugging'],
    ['Analyze the sales data and give me the trend', 'data_analysis'],
    ['Write a poem about the sea', 'creative_writing'],
    ['What are the trade-offs between these two strategy options, step by step?', 'complex_reasoning']
  ];
  for (const [text, type] of cases) assert.equal(R.analyzeTask(chat(text)).type, type, text);
});

test('the persona system prompt does not vote on the task type', () => {
  const a = R.analyzeTask({ system: 'You write code and debug errors and translate and summarize.', messages: [{ role: 'user', content: 'hi' }] });
  assert.equal(a.type, 'chit_chat');
});

test('a raw-JSON system prompt makes the task structured extraction', () => {
  const a = R.analyzeTask({ system: 'Respond with ONLY a raw JSON object.', messages: [{ role: 'user', content: 'Transcripts: ...' }] });
  assert.equal(a.type, 'extraction');
  assert.equal(a.needs.jsonOnly, true);
});

test('images, tool loops, sampling and forced tool_choice are detected', () => {
  const a = R.analyzeTask({ temperature: 0.2, tool_choice: { type: 'any' }, tools: [{ name: 'x', input_schema: {} }], messages: [
    { role: 'user', content: [{ type: 'image', source: { type: 'base64', media_type: 'image/png', data: 'x' } }, { type: 'text', text: 'what is it' }] },
    { role: 'assistant', content: [{ type: 'tool_use', id: 'a', name: 'x', input: {} }] },
    { role: 'user', content: [{ type: 'tool_result', tool_use_id: 'a', content: 'ok' }] }
  ] });
  assert.equal(a.needs.vision, true);
  assert.equal(a.needs.sampling, true);
  assert.equal(a.needs.forcedToolChoice, true);
  assert.equal(a.toolDepth, 1);
});

test('complexity rises with difficulty markers and falls with "briefly"', () => {
  const easy = R.analyzeTask(chat('Briefly, write a function that adds two numbers')).complexity;
  const hard = R.analyzeTask(chat('Write a production-grade, concurrent, secure function handling every edge case from scratch')).complexity;
  assert.ok(hard > easy + 0.3, easy + ' vs ' + hard);
});

test('token estimates weigh Hebrew heavier than English per character', () => {
  const en = R.estimateTokens('a'.repeat(360));
  const he = R.estimateTokens('א'.repeat(360));
  assert.equal(en, 100);
  assert.equal(he, 180);
});

/* --------------------------------------------------------------- scoring */

test('never picks a model below the quality floor, however cheap', () => {
  const d = route(chat('Design the architecture for a distributed, secure, scalable inventory system; walk through the trade-offs step by step.'));
  const haiku = d.candidates.find(c => c.model === 'claude-haiku-4-5');
  assert.equal(haiku.meets, false);
  assert.notEqual(d.model, 'claude-haiku-4-5');
  assert.ok(d.estimate.quality >= d.minQuality);
});

test('among qualified models the cheaper one wins — the strongest is not the default', () => {
  const d = route(chat('What is 12% of 250?'));
  assert.equal(d.model, 'claude-haiku-4-5');
  const opus = d.candidates.find(c => c.model === 'claude-opus-5');
  assert.equal(opus.meets, true, 'Opus would also do it...');
  assert.ok(opus.costUsd > d.estimate.costUsd, '...but costs more, so it is not chosen');
});

test('when nothing clears the floor the strongest available model is used', () => {
  const d = route(chat('Prove rigorously that our distributed concurrent algorithm has no deadlock or race condition in any edge case, with security and performance trade-offs across the entire architecture'),
                  { ROUTER_MODELS: 'claude-haiku-4-5,claude-sonnet-5' });
  assert.equal(d.model, 'claude-sonnet-5');
  assert.ok(d.candidates.every(c => c.excluded || !c.meets));
});

test('context window is a hard requirement', () => {
  const big = 'lorem ipsum dolor sit amet '.repeat(30000); // ~200k tokens
  const d = route(chat('Summarize this: ' + big));
  const haiku = d.candidates.find(c => c.model === 'claude-haiku-4-5');
  assert.match(haiku.excluded, /context window/);
  assert.notEqual(d.model, 'claude-haiku-4-5');
});

test('temperature excludes the models that reject sampling parameters', () => {
  const d = route(chat('Write a JavaScript function that sums an array', { temperature: 0.3 }));
  assert.match(d.candidates.find(c => c.model === 'claude-sonnet-5').excluded, /temperature/);
  assert.equal(d.model, 'claude-haiku-4-5', 'the only enabled model that still accepts it');
});

test('a forced tool_choice excludes Fable 5.1', () => {
  const d = route(chat('hi', { tool_choice: { type: 'tool', name: 'remember' } }));
  assert.match(d.candidates.find(c => c.model === 'claude-fable-5-1').excluded, /tool_choice/);
});

test('a token budget narrows the qualified set but never breaks the floor', () => {
  const body = chat('Design the architecture for a distributed, secure, scalable inventory system; walk through the trade-offs step by step.');
  const free = route(body);
  const tight = route(body, { ROUTER_TOKEN_BUDGET: JSON.stringify({ maxCostUsd: 0.000001 }) });
  assert.equal(tight.model, free.model, 'nothing fits the budget, so the floor wins over the budget');
  assert.ok(tight.estimate.quality >= tight.minQuality);
});

test('weights are configurable: pricing cost at zero lets quality win', () => {
  const d = route(chat('What is the capital of France?'), { ROUTER_WEIGHTS: JSON.stringify({ cost: 0, tokens: 0, latency: 0 }) });
  assert.notEqual(d.model, 'claude-haiku-4-5');
});

test('the escalation ladder climbs from the choice through every stronger model', () => {
  const d = route(chat('Write a JavaScript function that sums an array'));
  assert.deepEqual(d.ladder, ['claude-sonnet-5', 'claude-opus-5', 'claude-fable-5-1', 'claude-haiku-4-5']);
});

test('every decision carries a readable explanation with savings', () => {
  const d = route(chat('My checkout throws TypeError: cannot read price of undefined, fix it'));
  assert.match(d.explanation, /^Task: Code debugging/m);
  assert.match(d.explanation, /^Selected model: Claude Sonnet 5/m);
  assert.match(d.explanation, /Requires code reasoning/);
  assert.match(d.explanation, /Context size: [\d.]+k? tokens/);
  assert.match(d.explanation, /meets the required capability/);
  assert.match(d.explanation, /Claude Opus 5 would provide .* quality .* the cost/);
  assert.match(d.explanation, /Claude Haiku 4.5 is below the required threshold/);
  assert.match(d.explanation, /^Estimated savings: -?\d+%/m);
});

/* -------------------------------------------------------------- learning */

test('repeated failures of a model on a task type move that task up a tier', () => {
  const body = chat('What is the capital of Peru?');
  assert.equal(route(body).model, 'claude-haiku-4-5');
  const haiku = R.loadRegistry({}).find(m => m.id === 'claude-haiku-4-5');
  for (let i = 0; i < 6; i++) {
    const d = route(body);
    R.recordAttempt(d, haiku, 'escalated', null, 0);
  }
  assert.equal(route(body).model, 'claude-sonnet-5');
});

test('negative user feedback counts against the model that answered', () => {
  const body = chat('What is the capital of Peru?');
  for (let i = 0; i < 4; i++) {
    const d = route(body);
    R.remember(d);
    R.finishDecision(d, 'claude-haiku-4-5', 'ok');
    assert.equal(R.recordFeedback(d.id, 'bad').ok, true);
  }
  assert.equal(route(body).model, 'claude-sonnet-5');
  assert.equal(R.recordFeedback('nope', 'bad').ok, false);
});

test('a strong track record lifts a model\'s effective capability, a little', () => {
  const a = R.analyzeTask(chat('Write a product description for a candle'));
  const haiku = R.loadRegistry({}).find(m => m.id === 'claude-haiku-4-5');
  const before = R.expectedQuality(haiku, a).quality;
  const d = route(chat('Write a product description for a candle'));
  for (let i = 0; i < 40; i++) R.recordAttempt(d, haiku, 'ok', null, 0);
  const after = R.expectedQuality(haiku, a);
  assert.ok(after.quality > before);
  assert.ok(after.learned <= 4);
});

test('the prompt cache makes the router stay on the model that served the last turn', () => {
  const body = chat('Summarize briefly: sales up, returns down.');
  const d1 = route(body);
  R.finishDecision(d1, 'claude-sonnet-5', 'ok');
  const d2 = route(body);
  const sonnet = d2.candidates.find(c => c.model === 'claude-sonnet-5');
  const cold = d1.candidates.find(c => c.model === 'claude-sonnet-5');
  assert.ok(sonnet.costUsd < cold.costUsd, 'a warm cache is priced cheaper');
});

test('mid tool-loop the router keeps the model that started the loop', () => {
  const messages = [{ role: 'user', content: 'Go through all my orders and flag late ones' }];
  const body = { model: 'claude-sonnet-4-6', max_tokens: 3000, system: PERSONA, messages };
  const first = route(body);
  R.finishDecision(first, 'claude-opus-5', 'ok');
  const next = route(Object.assign({}, body, { messages: messages.concat([
    { role: 'assistant', content: [{ type: 'tool_use', id: 'a', name: 'run_code', input: {} }] },
    { role: 'user', content: [{ type: 'tool_result', tool_use_id: 'a', content: '[]' }] }]) }));
  assert.equal(next.model, 'claude-opus-5');
  assert.equal(next.sticky, true);
});

/* -------------------------------------------------------- request shaping */

test('adaptBodyForModel sets the model and effort without touching the original', () => {
  const body = chat('Write a function', { max_tokens: 3000 });
  const frozen = JSON.stringify(body);
  const d = route(body);
  const sonnet = R.loadRegistry({}).find(m => m.id === 'claude-sonnet-5');
  const out = R.adaptBodyForModel(body, sonnet, d);
  assert.equal(out.model, 'claude-sonnet-5');
  assert.equal(out.output_config.effort, d.effort);
  assert.ok(out.max_tokens > 3000, 'thinking headroom added');
  assert.equal(JSON.stringify(body), frozen);
});

test('Haiku gets no effort, no thinking blocks, and keeps sampling', () => {
  const body = { model: 'x', max_tokens: 2000, temperature: 0.5, messages: [
    { role: 'user', content: 'hi' },
    { role: 'assistant', content: [{ type: 'thinking', thinking: '', signature: 's' }, { type: 'text', text: 'hello' }] },
    { role: 'user', content: 'thanks' }] };
  const haiku = R.loadRegistry({}).find(m => m.id === 'claude-haiku-4-5');
  const out = R.adaptBodyForModel(body, haiku, route(body));
  assert.equal(out.output_config, undefined);
  assert.equal(out.temperature, 0.5);
  assert.deepEqual(out.messages[1].content.map(b => b.type), ['text']);
  assert.equal(out.max_tokens, 2000);
});

test('models that reject sampling have it stripped, and max_tokens is clamped', () => {
  const fable = R.loadRegistry({}).find(m => m.id === 'claude-fable-5-1');
  const body = { model: 'x', max_tokens: 60000, stream: true, temperature: 1, top_p: 0.9, messages: [{ role: 'user', content: 'x' }] };
  const out = R.adaptBodyForModel(body, fable, route(body));
  assert.equal(out.temperature, undefined);
  assert.equal(out.top_p, undefined);
  assert.ok(out.max_tokens <= fable.budget.maxOutput);
});

test('non-streamed thinking requests stay under 16k so they cannot time out', () => {
  const opus = R.loadRegistry({}).find(m => m.id === 'claude-opus-5');
  const body = { model: 'x', max_tokens: 8000, messages: [{ role: 'user', content: 'x' }] };
  assert.ok(R.adaptBodyForModel(body, opus, route(body)).max_tokens <= 16000);
});

/* ---------------------------------------------------- quality evaluation */

test('evaluateResponse flags refusals, empty replies, spent budgets and bad JSON', () => {
  const jsonTask = R.analyzeTask({ system: 'Respond with ONLY a raw JSON object.', messages: [{ role: 'user', content: 'x' }] });
  assert.equal(R.evaluateResponse({ content: [{ type: 'text', text: 'Hi!' }], stop_reason: 'end_turn' }).ok, true);
  assert.equal(R.evaluateResponse({ content: [{ type: 'tool_use', id: 'a', name: 'x', input: {} }], stop_reason: 'tool_use' }).ok, true);
  assert.equal(R.evaluateResponse({ content: [], stop_reason: 'refusal' }).reason, 'refusal');
  assert.equal(R.evaluateResponse({ content: [], stop_reason: 'end_turn' }).reason, 'empty reply');
  assert.match(R.evaluateResponse({ content: [{ type: 'thinking', thinking: '' }], stop_reason: 'max_tokens' }).reason, /budget/);
  assert.equal(R.evaluateResponse({ content: [{ type: 'text', text: 'Sure! {"a":1}' }] }, jsonTask).ok, false);
  assert.equal(R.evaluateResponse({ content: [{ type: 'text', text: '```json\n{"a":1}\n```' }] }, jsonTask).ok, true);
  assert.equal(R.evaluateResponse(null).ok, false);
});
