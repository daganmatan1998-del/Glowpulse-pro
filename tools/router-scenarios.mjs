/* The simulation suite for the Claude model router: realistic requests of every
   task type, each with the models that are acceptable for it. Shared by the
   CLI (`node tools/router-cli.mjs simulate`) and by tests/router.simulation.test.mjs,
   so what the CLI shows is exactly what the tests hold the router to.

   `accept` lists every model that is a correct answer, cheapest first. A
   router that picks something outside it is either overspending (a stronger
   model than needed) or under-serving (one below the task's floor). */

/* Stands in for the page's persona prompt: long enough (> 4000 chars) that
   the worker marks it for prompt caching, as the real one is. */
export const PERSONA = ('You are J.A.R.V.I.S., a personal assistant for one user who runs Shopify stores. ' +
  'Be concise, direct and warm. Answer in the language the user writes in. ').repeat(30);

const TOOLS = [
  { name: 'run_code', description: 'Run JavaScript in a sandbox and return stdout.', input_schema: { type: 'object', properties: { code: { type: 'string' } }, required: ['code'] } },
  { name: 'remember', description: 'Save a durable note to long-term memory.', input_schema: { type: 'object', properties: { notes: { type: 'array', items: { type: 'string' } } }, required: ['notes'] } },
  { type: 'web_search_20250305', name: 'web_search', max_uses: 6 }
];

export function chat(text, extra = {}) {
  return Object.assign({
    model: 'claude-sonnet-4-6', max_tokens: 3000, stream: true, system: PERSONA, tools: TOOLS,
    messages: [{ role: 'user', content: text }]
  }, extra);
}

const HAIKU = 'claude-haiku-4-5', SONNET = 'claude-sonnet-5', OPUS = 'claude-opus-5', FABLE = 'claude-fable-5-1';

const bigDoc = 'Quarterly review. '.repeat(1) + ('Revenue rose in the north region while returns fell; the team shipped the loyalty programme and cut support backlog. ').repeat(2600);

const toolLoop = (rounds) => {
  const messages = [{ role: 'user', content: 'Go through all my open orders and flag any that shipped late, then draft an apology email for each.' }];
  for (let i = 0; i < rounds; i++) {
    messages.push({ role: 'assistant', content: [{ type: 'tool_use', id: 't' + i, name: 'run_code', input: { code: 'orders.slice(' + i * 10 + ')' } }] });
    messages.push({ role: 'user', content: [{ type: 'tool_result', tool_use_id: 't' + i, content: '[{"id":' + i + ',"late":true}]' }] });
  }
  return messages;
};

export const SCENARIOS = [
  { name: 'Greeting (Hebrew)',            expectType: 'chit_chat',         accept: [HAIKU],          body: chat('היי מה נשמע?') },
  { name: 'Greeting (English)',           expectType: 'chit_chat',         accept: [HAIKU],          body: chat('thanks!') },
  { name: 'Simple fact',                  expectType: 'simple_question',   accept: [HAIKU],          body: chat('What is the capital of Australia?') },
  { name: 'Translation',                  expectType: 'translation',       accept: [HAIKU, SONNET],  body: chat('Translate to English: המוצר יגיע תוך שלושה ימי עסקים, ואפשר להחזיר אותו תוך 30 יום.') },
  { name: 'Short summary',                expectType: 'summarization',     accept: [HAIKU, SONNET],  body: chat('Summarize briefly: our return rate fell from 9% to 6% after we added size charts, but support tickets about shipping doubled in the same month.') },
  { name: 'JSON memory extraction',       expectType: 'extraction',        accept: [HAIKU, SONNET],  body: {
      model: 'claude-sonnet-4-6', max_tokens: 1200,
      system: 'You maintain the long-term memory of an assistant. Respond with ONLY a raw JSON object, no markdown fences, matching {"profile":{"about":[]}}.',
      messages: [{ role: 'user', content: 'Transcripts:\nUser: I sell skincare on Shopify and I want to launch in Germany next month.' }] } },
  { name: 'Product description',          expectType: 'creative_writing',  accept: [HAIKU, SONNET],  body: chat('Write a product description for a rose-quartz face roller, warm tone, 80 words.') },
  { name: 'Sales data analysis',          expectType: 'data_analysis',     accept: [SONNET],         body: chat('Analyze the sales data: Jan 12,400 / Feb 9,800 / Mar 15,100 / Apr 14,900 revenue, conversion rate 1.8% → 2.4%. What is the trend, and what forecast would you give for May?') },
  { name: 'Write a function',             expectType: 'coding',            accept: [SONNET],         body: chat('Write a JavaScript function that groups an array of orders by customer email and sums the totals.') },
  { name: 'Debug an error',               expectType: 'debugging',         accept: [SONNET, OPUS],   body: chat('My checkout script crashes with TypeError: Cannot read properties of undefined (reading "price")\n```js\nconst total = cart.items.map(i => i.variant.price).reduce((a,b)=>a+b)\n```\nWhy, and how do I fix it?') },
  { name: 'Hard debugging (race)',        expectType: 'debugging',         accept: [OPUS, FABLE],    body: chat('Production bug: intermittent double-charges. Two webhook workers process the same order concurrently; we suspect a race condition in our idempotency check and a deadlock under load. Here is the stack trace:\n```\nError: deadlock detected\n    at Queue.process (worker.js:88:13)\n```\nFind the root cause and give a rigorous fix that handles every edge case.') },
  { name: 'Architecture design',          expectType: 'complex_reasoning', accept: [OPUS, FABLE],    body: chat('Design a system architecture for syncing inventory across 5 Shopify stores and 2 warehouses. Walk through the trade-offs step by step: consistency vs latency, scalability, security, and failure modes.') },
  { name: 'Extreme reasoning',            expectType: 'complex_reasoning', accept: [FABLE],          body: chat('Prove, rigorously and step by step, that our distributed, concurrent reservation algorithm is free of deadlock and race conditions under every edge case, then derive its worst-case performance and the security trade-offs of each design choice in the entire architecture.', { max_tokens: 20000 }) },
  { name: 'Long-context document',        expectType: 'long_context',      accept: [SONNET],         body: chat('Here is the report. What changed?\n\n' + bigDoc) },
  { name: 'Agentic, mid tool-loop',       expectType: 'agentic',           accept: [SONNET, OPUS],   body: Object.assign(chat(''), { messages: toolLoop(4) }) },
  { name: 'Image question',               expectType: 'vision',            accept: [HAIKU, SONNET],  body: chat('', { messages: [{ role: 'user', content: [
      { type: 'image', source: { type: 'base64', media_type: 'image/png', data: 'iVBORw0KGgo=' } },
      { type: 'text', text: 'What is this?' }] }] }) }
];
