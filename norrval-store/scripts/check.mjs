// Pre-launch report: lists every [PLACEHOLDER] still in the built site, every
// internal link that points nowhere, and images missing alt text.
// Run after `npm run build`:  npm run check
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const DIST = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../dist');
const files = [];
(function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    if (fs.statSync(p).isDirectory()) walk(p);
    else if (p.endsWith('.html')) files.push(p);
  }
})(DIST);

const placeholders = new Map();
const broken = [];
const noAlt = [];
for (const f of files) {
  const rel = '/' + path.relative(DIST, f).replace(/index\.html$/, '');
  const h = fs.readFileSync(f, 'utf8');
  for (const m of h.matchAll(/\[([A-Z][A-Z0-9 ,.\/&'’()–:;-]{2,}[^\]]*)\]/g)) {
    const k = m[1];
    if (!placeholders.has(k)) placeholders.set(k, new Set());
    placeholders.get(k).add(rel);
  }
  for (const m of h.matchAll(/href="(\/[^"#?]*)/g)) {
    const u = m[1];
    const target = u.endsWith('/') ? path.join(DIST, u, 'index.html') : path.join(DIST, u);
    if (!fs.existsSync(target)) broken.push(`${rel} → ${u}`);
  }
  for (const m of h.matchAll(/src="(\/assets\/[^"]+)"/g)) {
    if (!fs.existsSync(path.join(DIST, m[1]))) broken.push(`${rel} → ${m[1]}`);
  }
  for (const m of h.matchAll(/<img\b[^>]*>/g)) if (!/\balt="/.test(m[0])) noAlt.push(`${rel}: ${m[0].slice(0, 80)}`);
}

console.log(`\n${placeholders.size} distinct placeholders to fill in:\n`);
for (const [k, pages] of [...placeholders].sort()) console.log(`  [${k}]\n      ${[...pages].join(', ')}`);
console.log(`\nBroken internal links / assets: ${broken.length}`);
broken.forEach((b) => console.log('  ' + b));
console.log(`Images without alt: ${noAlt.length}`);
noAlt.forEach((b) => console.log('  ' + b));
process.exitCode = broken.length || noAlt.length ? 1 : 0;
