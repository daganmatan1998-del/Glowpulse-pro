#!/usr/bin/env node
/**
 * Render theme/templates/index.json to a static HTML preview.
 *
 * The Liquid sections are the source of truth; this mirrors their markup so
 * the storefront can be reviewed before it reaches a Shopify store. Same
 * stylesheet, same script, same copy — only the Liquid tags are resolved here
 * in JS instead of by Shopify.
 *
 * Usage: node scripts/build-preview.mjs [--out preview]
 */

import { mkdir, readFile, writeFile, copyFile } from 'node:fs/promises';
import { join } from 'node:path';

const ROOT = new URL('../', import.meta.url).pathname;
const outArg = process.argv.indexOf('--out');
const OUT = join(ROOT, outArg === -1 ? 'preview' : process.argv[outArg + 1]);

const esc = (s = '') =>
  String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

const blocks = (section) => (section.block_order || []).map((id) => section.blocks[id]);

/* Icon set — kept byte-identical to snippets/gp-icon.liquid. */
const ICONS = {
  sparkles: '<path d="M12 3l1.9 4.6L18.5 9.5 13.9 11.4 12 16l-1.9-4.6L5.5 9.5l4.6-1.9z"/><path d="M19 15l.8 2 2 .8-2 .8-.8 2-.8-2-2-.8 2-.8z"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  shield: '<path d="M12 3l7 3v5.5c0 4.3-2.9 8-7 9.5-4.1-1.5-7-5.2-7-9.5V6z"/><path d="M9.5 12.2l1.8 1.8 3.4-3.6"/>',
  truck: '<path d="M3 6h11v9H3z"/><path d="M14 9h4l3 3v3h-7z"/><circle cx="7" cy="18" r="1.8"/><circle cx="17.5" cy="18" r="1.8"/>',
  leaf: '<path d="M20 4c0 8-4.5 13-10.5 13A5.5 5.5 0 0 1 4 11.5C4 6 9.5 4 20 4z"/><path d="M4 20c3-5 7-8 12-10"/>',
  battery: '<rect x="2.5" y="8" width="16" height="9" rx="2.5"/><path d="M21.5 11.5v2"/><path d="M6.5 12.5h5"/>',
  award: '<circle cx="12" cy="9" r="5.5"/><path d="M8.5 13.8L7 21l5-2.6L17 21l-1.5-7.2"/>',
  undo: '<path d="M3 9h11a5.5 5.5 0 0 1 0 11h-4"/><path d="M6.5 5.5L3 9l3.5 3.5"/>',
  chevron: '<path d="M6 9l6 6 6-6"/>',
};

const icon = (name, cls = '') =>
  `<svg class="${cls}" viewBox="0 0 24 24" aria-hidden="true" focusable="false">${ICONS[name] || ''}</svg>`;

const CHECK =
  '<svg viewBox="0 0 20 20" aria-hidden="true" focusable="false"><path d="M10 1.7a8.3 8.3 0 100 16.6 8.3 8.3 0 000-16.6zm4.1 6.2l-4.8 5a.9.9 0 01-1.3 0L5.9 10.8a.9.9 0 111.3-1.3l1.5 1.6 4.1-4.4a.9.9 0 111.3 1.2z"/></svg>';
const CROSS =
  '<svg viewBox="0 0 20 20" aria-hidden="true" focusable="false"><path d="M10 1.7a8.3 8.3 0 100 16.6 8.3 8.3 0 000-16.6zm3 10.1a.9.9 0 11-1.2 1.2L10 11.2l-1.8 1.8A.9.9 0 117 11.8L8.8 10 7 8.2A.9.9 0 118.2 7L10 8.8 11.8 7A.9.9 0 1113 8.2L11.2 10z"/></svg>';

const STAR_FULL =
  '<svg viewBox="0 0 20 20" aria-hidden="true" focusable="false"><path d="M10 1.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L10 14.9l-5.2 2.7 1-5.8L1.5 7.7l5.9-.9z"/></svg>';
const STAR_EMPTY =
  '<svg viewBox="0 0 20 20" aria-hidden="true" focusable="false" style="fill:none;stroke:currentColor;stroke-width:1.4;opacity:.45"><path d="M10 1.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L10 14.9l-5.2 2.7 1-5.8L1.5 7.7l5.9-.9z"/></svg>';

const stars = (rating = 5, label = '') =>
  `<span class="gp-stars" role="img" aria-label="${esc(label || `${rating} out of 5 stars`)}">` +
  Array.from({ length: 5 }, (_, i) => (i < rating ? STAR_FULL : STAR_EMPTY)).join('') +
  '</span>';

const pad = (s) => `padding-block: ${s.padding_top ?? 96}px ${s.padding_bottom ?? 96}px;`;
const delay = (i) => `data-gp-delay="${i * 60}"`;
const head = (s) => `
      <div class="gp-head gp-reveal">
        ${s.eyebrow ? `<p class="gp-eyebrow">${esc(s.eyebrow)}</p>` : ''}
        <h2 class="gp-h2">${esc(s.heading)}</h2>
        ${s.subheading ? `<p class="gp-lede">${esc(s.subheading)}</p>` : ''}
      </div>`;

/* ------------------------------------------------------------- renderers */

const RENDER = {
  'gp-hero': (sec) => {
    const s = sec.settings;
    return `
  <section class="gp-section gp-section--dark gp-hero" style="${pad(s)}">
    <div class="gp-wrap">
      <div class="gp-hero__inner">
        <div>
          <p class="gp-eyebrow">${esc(s.eyebrow)}</p>
          <h1 class="gp-h1">${esc(s.heading)}</h1>
          <div class="gp-rating">${stars(5, s.rating_text)}<span>${esc(s.rating_text)}</span></div>
          <p class="gp-lede">${esc(s.subheading)}</p>
          <div class="gp-price">
            <span class="gp-price__now">$249.00</span>
            <span class="gp-price__was">$329.00</span>
            <span class="gp-badge">Save 24%</span>
          </div>
          <div class="gp-hero__actions">
            <a class="gp-btn gp-btn--primary" href="#gp-offer">${esc(s.cta_label)}</a>
            <a class="gp-btn gp-btn--ghost" href="${esc(s.cta2_link || '#gp-how')}">${esc(s.cta2_label)}</a>
          </div>
          <p class="gp-hero__note">${esc(s.note)}</p>
        </div>
        <div>
          <div class="gp-media" role="img" aria-label="GlowPulse Pro LED therapy mask">
            <svg viewBox="0 0 400 500" aria-hidden="true" style="background:#26211d">
              <defs>
                <radialGradient id="glow" cx="50%" cy="45%" r="55%">
                  <stop offset="0%" stop-color="#e8564a" stop-opacity=".95"/>
                  <stop offset="55%" stop-color="#a1341f" stop-opacity=".5"/>
                  <stop offset="100%" stop-color="#26211d" stop-opacity="0"/>
                </radialGradient>
              </defs>
              <rect width="400" height="500" fill="#26211d"/>
              <ellipse cx="200" cy="230" rx="180" ry="210" fill="url(#glow)"/>
              <path d="M200 90c58 0 96 38 96 104 0 78-44 148-96 176-52-28-96-98-96-176 0-66 38-104 96-104z"
                    fill="none" stroke="#f0a08f" stroke-width="2.5" opacity=".85"/>
              <g fill="#ffd9c9" opacity=".9">
                ${Array.from({ length: 96 }, (_, i) => {
                  const col = i % 8;
                  const row = Math.floor(i / 8);
                  const cx = 132 + col * 19.5;
                  const cy = 128 + row * 19.5;
                  const dx = (cx - 200) / 92;
                  const dy = (cy - 235) / 148;
                  if (dx * dx + dy * dy > 1) return '';
                  if ((cy > 178 && cy < 205) && ((cx > 148 && cx < 182) || (cx > 218 && cx < 252))) return '';
                  return `<circle cx="${cx.toFixed(1)}" cy="${cy.toFixed(1)}" r="3.1"/>`;
                }).join('')}
              </g>
            </svg>
          </div>
        </div>
      </div>
    </div>
  </section>`;
  },

  'gp-trustbar': (sec) => `
  <section class="gp-section gp-section--muted" style="padding-block: 40px;">
    <div class="gp-wrap">
      <div class="gp-trust">
        ${blocks(sec)
          .map(
            (b, i) => `
        <div class="gp-trust__item gp-reveal" ${delay(i)}>
          ${icon(b.settings.icon, 'gp-trust__icon')}
          <span class="gp-trust__label">${esc(b.settings.label)}</span>
          <span class="gp-trust__sub">${esc(b.settings.sublabel)}</span>
        </div>`
          )
          .join('')}
      </div>
    </div>
  </section>`,

  'gp-benefits': (sec) => `
  <section class="gp-section" id="gp-benefits" style="${pad(sec.settings)}">
    <div class="gp-wrap">${head(sec.settings)}
      <div class="gp-grid">
        ${blocks(sec)
          .map(
            (b, i) => `
        <article class="gp-card gp-benefit gp-reveal" ${delay(i)}>
          ${icon(b.settings.icon, 'gp-benefit__icon')}
          <h3 class="gp-h3">${esc(b.settings.title)}</h3>
          <p class="gp-body">${esc(b.settings.text)}</p>
        </article>`
          )
          .join('')}
      </div>
    </div>
  </section>`,

  'gp-how-it-works': (sec) => `
  <section class="gp-section gp-section--muted gp-steps" id="gp-how" style="${pad(sec.settings)}">
    <div class="gp-wrap">${head(sec.settings)}
      <ol class="gp-grid" style="list-style:none;margin:0;padding:0;">
        ${blocks(sec)
          .map(
            (b, i) => `
        <li class="gp-card gp-reveal" data-gp-delay="${i * 80}">
          <span class="gp-step__num" aria-hidden="true">${i + 1}</span>
          <h3 class="gp-h3">${esc(b.settings.title)}</h3>
          <p class="gp-body" style="color:var(--gp-secondary)">${esc(b.settings.text)}</p>
        </li>`
          )
          .join('')}
      </ol>
    </div>
  </section>`,

  'gp-reviews': (sec) => {
    const s = sec.settings;
    return `
  <section class="gp-section" id="gp-reviews" style="${pad(s)}">
    <div class="gp-wrap">
      <div class="gp-head gp-reveal">
        <p class="gp-eyebrow">${esc(s.eyebrow)}</p>
        <h2 class="gp-h2">${esc(s.heading)}</h2>
        <div class="gp-rating" style="justify-content:center">${stars(5, s.aggregate)}<span>${esc(s.aggregate)}</span></div>
      </div>
      <div class="gp-grid">
        ${blocks(sec)
          .map(
            (b, i) => `
        <figure class="gp-card gp-review gp-reveal" ${delay(i)} style="margin:0">
          ${stars(b.settings.rating)}
          <blockquote class="gp-review__text" style="margin:var(--gp-space-2) 0;border:0;padding:0">${esc(b.settings.quote)}</blockquote>
          <figcaption class="gp-review__who">
            <span class="gp-review__avatar" aria-hidden="true"></span>
            <span>
              <span class="gp-review__name">${esc(b.settings.name)}</span><br>
              <span class="gp-review__verified">${CHECK}Verified purchase</span>
            </span>
          </figcaption>
        </figure>`
          )
          .join('')}
      </div>
    </div>
  </section>`;
  },

  'gp-comparison': (sec) => {
    const s = sec.settings;
    const cell = (v) =>
      v === 'yes'
        ? `<span class="gp-table__mark gp-table__mark--yes">${CHECK}Yes</span>`
        : v === 'no'
        ? `<span class="gp-table__mark gp-table__mark--no">${CROSS}No</span>`
        : esc(v);
    return `
  <section class="gp-section gp-section--muted" style="${pad(s)}">
    <div class="gp-wrap">${head(s)}
      <div class="gp-table-scroll gp-reveal">
        <table class="gp-table">
          <thead>
            <tr>
              <th scope="col">${esc(s.col_feature)}</th>
              <th scope="col">${esc(s.col_us)}</th>
              <th scope="col">${esc(s.col_them_1)}</th>
              <th scope="col">${esc(s.col_them_2)}</th>
            </tr>
          </thead>
          <tbody>
            ${blocks(sec)
              .map(
                (b) => `
            <tr>
              <th scope="row" style="font-weight:600">${esc(b.settings.feature)}</th>
              <td>${cell(b.settings.us)}</td>
              <td>${cell(b.settings.them_1)}</td>
              <td>${cell(b.settings.them_2)}</td>
            </tr>`
              )
              .join('')}
          </tbody>
        </table>
      </div>
    </div>
  </section>`;
  },

  'gp-faq': (sec) => `
  <section class="gp-section" id="gp-faq" style="${pad(sec.settings)}">
    <div class="gp-wrap gp-wrap--narrow">
      <div class="gp-head gp-reveal">
        <p class="gp-eyebrow">${esc(sec.settings.eyebrow)}</p>
        <h2 class="gp-h2">${esc(sec.settings.heading)}</h2>
      </div>
      <div class="gp-faq gp-reveal">
        ${blocks(sec)
          .map(
            (b, i) => `
        <div class="gp-faq__item">
          <h3 style="margin:0">
            <button class="gp-faq__q" type="button" id="faq-q-${i}" aria-expanded="false" aria-controls="faq-a-${i}">
              <span>${esc(b.settings.question)}</span>
              ${icon('chevron', 'gp-faq__chevron')}
            </button>
          </h3>
          <div class="gp-faq__a" id="faq-a-${i}" role="region" aria-labelledby="faq-q-${i}" hidden>${b.settings.answer}</div>
        </div>`
          )
          .join('')}
      </div>
    </div>
  </section>`,

  'gp-offer': (sec) => {
    const s = sec.settings;
    return `
  <section class="gp-section gp-section--dark" id="gp-offer" style="${pad(s)}">
    <div class="gp-wrap">
      <div class="gp-offer">
        <div>
          <p class="gp-eyebrow">${esc(s.eyebrow)}</p>
          <h2 class="gp-h2">${esc(s.heading)}</h2>
          <p class="gp-lede">${esc(s.subheading)}</p>
          <ul class="gp-guarantee-list">
            ${blocks(sec)
              .map((b) => `<li>${CHECK}<span>${esc(b.settings.text)}</span></li>`)
              .join('')}
          </ul>
        </div>
        <div class="gp-card gp-reveal" data-gp-delay="100" style="background:rgba(255,255,255,.06);border-color:rgba(255,255,255,.18);color:#fff">
          <div class="gp-price">
            <span class="gp-price__now">$249.00</span>
            <span class="gp-price__was">$329.00</span>
          </div>
          <button type="button" class="gp-btn gp-btn--primary gp-btn--block">${esc(s.cta_label)}</button>
          <p class="gp-hero__note" style="margin-top:var(--gp-space-2);text-align:center">${esc(s.note)}</p>
        </div>
      </div>
    </div>
  </section>`;
  },
};

/* ------------------------------------------------------------------ build */

const template = JSON.parse(await readFile(join(ROOT, 'theme/templates/index.json'), 'utf8'));

const body = template.order
  .map((key) => {
    const sec = template.sections[key];
    const render = RENDER[sec.type];
    if (!render) throw new Error(`No preview renderer for section type "${sec.type}"`);
    return render(sec);
  })
  .join('\n');

const html = `<title>GlowPulse Pro</title>
<link rel="stylesheet" href="glowpulse.css">
<noscript><style>.gp-reveal { opacity: 1 !important; transform: none !important; }</style></noscript>
<style>
  body { margin: 0; background: var(--gp-background); }
  .gp-preview-note {
    font-family: var(--gp-font-body);
    background: #1c1917;
    color: rgba(255,255,255,.72);
    font-size: .8125rem;
    text-align: center;
    padding: 10px 16px;
    border-bottom: 1px solid rgba(255,255,255,.12);
  }
</style>
<p class="gp-preview-note">Static preview of the GlowPulse Pro theme — rendered from theme/templates/index.json</p>
<main>
${body}
</main>
<script src="glowpulse.js"></script>
`;

await mkdir(OUT, { recursive: true });
await writeFile(join(OUT, 'index.html'), html);
await copyFile(join(ROOT, 'theme/assets/glowpulse.css'), join(OUT, 'glowpulse.css'));
await copyFile(join(ROOT, 'theme/assets/glowpulse.js'), join(OUT, 'glowpulse.js'));

console.log(`Preview written to ${OUT}/index.html (${html.length} bytes, ${template.order.length} sections)`);
