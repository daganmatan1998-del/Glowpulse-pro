#!/usr/bin/env node
/* Command line for the Claude model router in jarvis-worker.js.

     node tools/router-cli.mjs simulate            run the scenario suite, show every decision
     node tools/router-cli.mjs simulate --explain  ...with the full explanation of each
     node tools/router-cli.mjs explain "prompt"    route one prompt and explain it
     node tools/router-cli.mjs registry            the model registry as the router sees it
     node tools/router-cli.mjs stats --url https://your-worker.workers.dev --token <session token>
                                                   live dashboard from a deployed worker

   Router settings are read from the environment exactly as the worker reads
   them, so `ROUTER_MODELS=claude-haiku-4-5,claude-opus-5 node tools/router-cli.mjs simulate`
   shows what would change if Sonnet were switched off. */

import { __router as R } from '../jarvis-worker.js';
import { SCENARIOS, chat } from './router-scenarios.mjs';

const env = {};
for (const k of ['ROUTER', 'ROUTER_SUFFICIENT_QUALITY', 'ROUTER_MODELS', 'ROUTER_REGISTRY', 'ROUTER_TASKS', 'ROUTER_WEIGHTS', 'ROUTER_MIN_QUALITY', 'ROUTER_TOKEN_BUDGET']) {
  if (process.env[k] !== undefined) env[k] = process.env[k];
}

const args = process.argv.slice(2);
const cmd = args[0] || 'simulate';
const flag = (name) => { const i = args.indexOf('--' + name); return i >= 0 ? (args[i + 1] && !args[i + 1].startsWith('--') ? args[i + 1] : true) : null; };

const pad = (s, n) => String(s).length >= n ? String(s).slice(0, n) : String(s) + ' '.repeat(n - String(s).length);
const lpad = (s, n) => String(s).length >= n ? String(s) : ' '.repeat(n - String(s).length) + String(s);
const usd = (x) => '$' + Number(x || 0).toFixed(5);

function table(rows, cols) {
  const widths = cols.map(c => Math.max(c.title.length, ...rows.map(r => String(c.get(r)).length)));
  const line = (vals) => vals.map((v, i) => cols[i].right ? lpad(v, widths[i]) : pad(v, widths[i])).join('  ');
  console.log(line(cols.map(c => c.title)));
  console.log(widths.map(w => '─'.repeat(w)).join('  '));
  for (const r of rows) console.log(line(cols.map(c => c.get(r))));
}

async function main() {
  if (cmd === 'simulate') {
    R.resetRouterState();
    const rows = SCENARIOS.map(s => {
      const d = R.route(s.body, env);
      return { s, d, pass: s.accept.includes(d.model) && (!s.expectType || s.expectType === d.task) };
    });
    table(rows, [
      { title: 'Scenario', get: r => r.s.name },
      { title: 'Task', get: r => r.d.task },
      { title: 'Cx', get: r => r.d.complexity.toFixed(2), right: true },
      { title: 'Model', get: r => r.d.model || '—' },
      { title: 'Effort', get: r => r.d.effort || '—' },
      { title: 'Est. tokens', get: r => r.d.estimate ? r.d.estimate.totalTokens : 0, right: true },
      { title: 'Est. cost', get: r => r.d.estimate ? usd(r.d.estimate.costUsd) : '—', right: true },
      { title: 'Baseline', get: r => r.d.baseline ? usd(r.d.baseline.costUsd) : '—', right: true },
      { title: 'Saved', get: r => r.d.savingsPct + '%', right: true },
      { title: 'OK', get: r => r.pass ? '✓' : '✗ want ' + r.s.accept.join('|') }
    ]);
    const cost = rows.reduce((a, r) => a + (r.d.estimate ? r.d.estimate.costUsd : 0), 0);
    const base = rows.reduce((a, r) => a + (r.d.baseline ? r.d.baseline.costUsd : 0), 0);
    const top = rows.reduce((a, r) => a + (r.d.topCost || 0), 0);
    const tok = rows.reduce((a, r) => a + (r.d.estimate ? r.d.estimate.totalTokens : 0), 0);
    const baseTok = rows.reduce((a, r) => a + (r.d.baseline ? r.d.baseline.totalTokens : 0), 0);
    console.log('\nRouted: ' + usd(cost) + ' / ' + tok + ' tokens   ·   everything on ' + rows[0].d.baseline.model + ': ' + usd(base) + ' / ' + baseTok + ' tokens' +
                '   ·   everything on the strongest model: ' + usd(top));
    console.log('Overall: ' + Math.round((1 - cost / base) * 100) + '% vs the page default, ' + Math.round((1 - cost / top) * 100) + '% vs always-strongest');
    /* The overall figure mixes two different things, so they are shown apart:
       requests moved to a cheaper model, and requests moved UP because the
       page's default sits below that task's quality floor. */
    const cheaper = rows.filter(r => r.d.estimate && r.d.estimate.costUsd <= r.d.baseline.costUsd);
    const upgraded = rows.filter(r => r.d.estimate && r.d.estimate.costUsd > r.d.baseline.costUsd);
    const sum = (list, f) => list.reduce((a, r) => a + f(r), 0);
    if (cheaper.length) {
      const c = sum(cheaper, r => r.d.estimate.costUsd), b = sum(cheaper, r => r.d.baseline.costUsd);
      console.log('  ' + cheaper.length + ' routed cheaper: ' + usd(c) + ' instead of ' + usd(b) + ' (' + Math.round((1 - c / b) * 100) + '% saved)');
    }
    if (upgraded.length) {
      const c = sum(upgraded, r => r.d.estimate.costUsd), b = sum(upgraded, r => r.d.baseline.costUsd);
      console.log('  ' + upgraded.length + ' upgraded for quality: ' + usd(c) + ' instead of ' + usd(b) +
                  ' — the default model is below the floor for: ' + upgraded.map(r => r.s.name).join(', '));
    }
    const failed = rows.filter(r => !r.pass).length;
    console.log(failed ? '\n' + failed + ' scenario(s) routed outside their acceptable models.' : '\nEvery scenario routed to an acceptable model.');
    if (flag('explain')) for (const r of rows) console.log('\n── ' + r.s.name + '\n' + r.d.explanation);
    process.exitCode = failed ? 1 : 0;
    return;
  }
  if (cmd === 'explain') {
    const prompt = args.slice(1).filter(a => !a.startsWith('--')).join(' ');
    if (!prompt) { console.error('usage: router-cli.mjs explain "your prompt"'); process.exitCode = 2; return; }
    const d = R.route(chat(prompt, { max_tokens: Number(flag('max-tokens')) || 3000 }), env);
    console.log(d.explanation);
    if (flag('json')) console.log(JSON.stringify(d.candidates, null, 2));
    return;
  }
  if (cmd === 'registry') {
    const reg = R.loadRegistry(env);
    table(reg, [
      { title: 'Model', get: m => m.id },
      { title: 'On', get: m => m.enabled ? 'yes' : 'no' },
      { title: 'Cap', get: m => m.capability, right: true },
      { title: 'Context', get: m => Math.round(m.contextWindow / 1000) + 'k', right: true },
      { title: 'In $/M', get: m => m.pricing.input.toFixed(2), right: true },
      { title: 'Out $/M', get: m => m.pricing.output.toFixed(2), right: true },
      { title: 'Thinking', get: m => m.thinking },
      { title: 'TTFT', get: m => m.latency.ttftMs + 'ms', right: true },
      { title: 'Tasks', get: m => (m.recommendedTasks || []).join(',') }
    ]);
    return;
  }
  if (cmd === 'stats') {
    const url = flag('url') || process.env.JARVIS_URL;
    const token = flag('token') || process.env.JARVIS_TOKEN;
    if (!url || !token) { console.error('usage: router-cli.mjs stats --url <worker url> --token <session token>'); process.exitCode = 2; return; }
    const res = await fetch(String(url).replace(/\/+$/, '') + '/router/stats', { headers: { 'X-Jarvis-Token': String(token) } });
    if (!res.ok) { console.error('worker answered ' + res.status + ': ' + (await res.text()).slice(0, 200)); process.exitCode = 1; return; }
    const s = await res.json();
    const t = s.totals;
    console.log('Router ' + (s.enabled ? 'ON' : 'OFF') + ' · ' + t.requests + ' requests · ' + t.tokens + ' tokens · ' + usd(t.costUsd) +
                ' spent · ' + usd(t.savedUsd) + ' saved (' + t.savingsPct + '% vs page default, ' + t.vsStrongestPct + '% vs strongest) · ' + t.escalations + ' escalations\n');
    table(Object.entries(s.models).map(([id, m]) => Object.assign({ id }, m)), [
      { title: 'Model', get: m => m.id },
      { title: 'Calls', get: m => m.n, right: true },
      { title: 'OK', get: m => m.ok, right: true },
      { title: 'Failed', get: m => m.fail, right: true },
      { title: 'Tokens in', get: m => m.tokensIn, right: true },
      { title: 'Tokens out', get: m => m.tokensOut, right: true },
      { title: 'Cost', get: m => usd(m.cost), right: true },
      { title: 'Avg latency', get: m => m.n ? Math.round(m.latencyMs / m.n) + 'ms' : '—', right: true }
    ]);
    console.log('');
    table(s.recent.slice(0, 20), [
      { title: 'When', get: d => d.at.slice(11, 19) },
      { title: 'Task', get: d => d.task },
      { title: 'Chosen', get: d => d.model },
      { title: 'Answered', get: d => d.final || '—' },
      { title: 'Outcome', get: d => d.outcome || '…' },
      { title: 'Tokens', get: d => d.actual ? d.actual.tokens : '—', right: true },
      { title: 'Saved', get: d => d.savingsPct + '%', right: true },
      { title: 'Feedback', get: d => d.feedback || '' }
    ]);
    if (flag('explain') && s.recent[0]) console.log('\nLatest decision:\n' + s.recent[0].explanation);
    return;
  }
  console.error('unknown command: ' + cmd + '\ncommands: simulate [--explain] | explain "prompt" | registry | stats --url U --token T');
  process.exitCode = 2;
}

main().catch(err => { console.error(err && err.stack || err); process.exitCode = 1; });
