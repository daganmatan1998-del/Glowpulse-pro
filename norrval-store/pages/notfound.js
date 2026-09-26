import { PRODUCT } from '../data/store.js';
import { ICONS } from '../components/icons.js';
import { layout } from '../components/layout.js';

export function notFoundPage() {
  const body = `
<section class="nf">
  <div>
    <svg class="nf__dial" viewBox="0 0 100 100" aria-hidden="true"><circle cx="50" cy="50" r="46" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5"/><g class="hand"><path d="M50 50 50 16" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></g><path d="M50 50 70 58" stroke="#eceef0" stroke-width="2" stroke-linecap="round"/><circle cx="50" cy="50" r="3" fill="#eceef0"/></svg>
    <p class="eyebrow">Error 404</p>
    <h1 class="h2">Lost track of time.</h1>
    <p class="lead" style="margin:20px auto 36px">The page you were looking for isn't here. It may have moved, or the link may be mistyped.</p>
    <div class="btn-row" style="justify-content:center">
      <a class="btn" href="/">Back to home</a>
      <a class="btn btn--ghost" href="/products/${PRODUCT.slug}/">Shop Nocturne ${ICONS.arrow}</a>
    </div>
  </div>
</section>`;
  return layout({ title: 'Page not found', description: 'This page could not be found.', path: '/404.html', body, noindex: true });
}
