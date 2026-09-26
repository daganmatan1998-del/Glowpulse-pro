import { STORE, FAQ } from '../data/store.js';
import { txt, esc } from '../utils/html.js';
import { layout, pageHead, breadcrumbSchema } from '../components/layout.js';

const CRUMBS = [
  { name: 'Home', path: '/' },
  { name: 'FAQ', path: '/faq/' },
];

export function faqPage() {
  const body = `
${pageHead({ crumbs: CRUMBS, eyebrow: 'Help centre', title: 'Frequently asked questions', lead: 'Straight answers about the watch, delivery, returns and payment.' })}
<section class="wrap" style="padding:clamp(40px,6vw,80px) var(--gutter) var(--section)">
  <div class="acc" style="max-width:860px">
    ${FAQ.map((f) => `<details><summary>${esc(f.q)}</summary><div class="acc__body"><p>${txt(f.a)}</p></div></details>`).join('')}
  </div>
  <p class="lead" style="margin-top:48px">Still need help? <a href="/contact/">Contact us</a>.</p>
</section>`;
  // FAQ structured data only includes answers without unresolved placeholders.
  const ready = FAQ.filter((f) => !/\[[^\]]+\]/.test(f.a));
  const schema = [breadcrumbSchema(CRUMBS)];
  if (ready.length) {
    schema.push({
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: ready.map((f) => ({ '@type': 'Question', name: f.q, acceptedAnswer: { '@type': 'Answer', text: f.a } })),
    });
  }
  return layout({
    title: 'FAQ',
    description: `Answers to common questions about ${STORE.brand} Nocturne: what's included, shipping, returns, warranty and secure payment.`,
    path: '/faq/',
    body,
    schema,
  });
}
