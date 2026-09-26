import { POLICIES, ACCESSIBILITY } from '../legal/policies.js';
import { layout, pageHead, breadcrumbSchema, LEGAL_LINKS } from '../components/layout.js';

// Wrap [PLACEHOLDER] tokens in text content (never inside tags) so they stand out.
const mark = (html) =>
  html.replace(/>([^<]*)</g, (m, t) => '>' + t.replace(/\[([^\]]+)\]/g, '<mark class="ph">[$1]</mark>') + '<');

function legalPage(p) {
  const crumbs = [
    { name: 'Home', path: '/' },
    { name: p.title, path: p.path },
  ];
  const nav = `<nav class="legal-nav" aria-label="Policies"><ul>${LEGAL_LINKS.map(
    (l) => `<li><a href="${l.href}"${l.href === p.path ? ' aria-current="page"' : ''}>${l.label}</a></li>`,
  ).join('')}</ul></nav>`;
  const body = `
${pageHead({ crumbs, eyebrow: 'Policies', title: p.title })}
<div class="wrap legal-layout">${nav}<article class="prose">${mark('<div>' + p.html + '</div>')}</article></div>`;
  return layout({ title: p.title, description: p.description, path: p.path, body, schema: [breadcrumbSchema(crumbs)] });
}

export const legalPages = () => [...POLICIES, ACCESSIBILITY].map((p) => ({ path: p.path, html: legalPage(p) }));
