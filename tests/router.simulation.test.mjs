/* Simulation tests: the router's decisions across every task type, compared
   with sending everything to one model. Scenarios live in
   tools/router-scenarios.mjs, shared with `node tools/router-cli.mjs simulate`. */
import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { __router as R } from '../jarvis-worker.js';
import { SCENARIOS, chat } from '../tools/router-scenarios.mjs';

beforeEach(() => { R.resetRouterState(); console.log = () => {}; });

for (const s of SCENARIOS) {
  test('scenario: ' + s.name + ' → ' + s.accept.join(' | '), () => {
    const d = R.route(s.body, {});
    assert.equal(d.task, s.expectType, 'task type');
    assert.ok(s.accept.includes(d.model), 'routed to ' + d.model + '\n' + d.explanation);
    assert.ok(d.estimate.quality >= d.minQuality || d.candidates.every(c => c.excluded || !c.meets),
      'below the quality floor while a qualified model existed');
  });
}

test('models climb with difficulty: every tier is used, weakest for the easiest', () => {
  const tier = { 'claude-haiku-4-5': 0, 'claude-sonnet-5': 1, 'claude-opus-5': 2, 'claude-fable-5-1': 3 };
  const used = new Set(SCENARIOS.map(s => R.route(s.body, {}).model));
  assert.deepEqual([...used].sort((a, b) => tier[a] - tier[b]), Object.keys(tier));
  const easy = R.route(chat('thanks!'), {});
  const hard = R.route(SCENARIOS.find(s => s.name === 'Extreme reasoning').body, {});
  assert.ok(tier[easy.model] < tier[hard.model]);
});

test('where the page default was good enough, routing is cheaper and uses fewer tokens', () => {
  let routed = 0, base = 0, routedTok = 0, baseTok = 0, count = 0;
  for (const s of SCENARIOS) {
    const d = R.route(s.body, {});
    const baseline = d.candidates.find(c => c.model === d.baseline.model);
    if (baseline.quality < d.minQuality) continue; // the default would have under-served this one
    routed += d.estimate.costUsd; base += d.baseline.costUsd;
    routedTok += d.estimate.totalTokens; baseTok += d.baseline.totalTokens;
    count++;
  }
  assert.ok(count >= 8, 'enough comparable scenarios (' + count + ')');
  const saved = 1 - routed / base;
  assert.ok(saved >= 0.2, 'saved ' + Math.round(saved * 100) + '%');
  assert.ok(routedTok <= baseTok * 1.1, 'tokens: ' + routedTok + ' vs ' + baseTok);
});

test('routing costs far less than always using the strongest model', () => {
  let routed = 0, top = 0;
  for (const s of SCENARIOS) { const d = R.route(s.body, {}); routed += d.estimate.costUsd; top += d.topCost; }
  assert.ok(routed < top * 0.5, routed + ' vs ' + top);
});

test('the router never uses a stronger model than the cheapest qualified one unless it scores better overall', () => {
  for (const s of SCENARIOS) {
    const d = R.route(s.body, {});
    if (d.sticky) continue;
    const qualified = d.candidates.filter(c => !c.excluded && c.meets);
    if (!qualified.length) continue;
    const best = Math.max(...qualified.map(c => c.utility));
    assert.equal(d.candidates.find(c => c.model === d.model).utility, best, s.name);
  }
});

test('availability changes re-route without any code change', () => {
  const noFable = { ROUTER_MODELS: 'claude-haiku-4-5,claude-sonnet-5,claude-opus-5' };
  const extreme = SCENARIOS.find(s => s.name === 'Extreme reasoning').body;
  assert.equal(R.route(extreme, {}).model, 'claude-fable-5-1');
  assert.equal(R.route(extreme, noFable).model, 'claude-opus-5');
  const opus55 = { ROUTER_MODELS: 'claude-haiku-4-5,claude-sonnet-5,claude-opus-5,claude-opus-5-5,claude-fable-5-1' };
  const arch = SCENARIOS.find(s => s.name === 'Architecture design').body;
  assert.equal(R.route(arch, opus55).model, 'claude-opus-5-5', 'stronger and cheaper than Opus 5, so it takes its place');
});

test('a Hebrew-speaking day: mostly small talk and errands stays mostly on the cheapest tier', () => {
  const day = ['בוקר טוב', 'מה מזג האוויר מחר בתל אביב?', 'תודה!', 'תתרגם לאנגלית: המבצע נגמר ביום ראשון',
               'תסכם לי בקצרה את המיילים מהבוקר', 'כמה זה 15% מ-240?', 'לילה טוב'];
  const models = day.map(t => R.route(chat(t), {}).model);
  assert.ok(models.filter(m => m === 'claude-haiku-4-5').length >= 6, models.join(', '));
});
