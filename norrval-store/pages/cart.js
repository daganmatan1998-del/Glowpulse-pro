import { layout, pageHead } from '../components/layout.js';

export function cartPage() {
  const body = `
${pageHead({ eyebrow: 'Cart', title: 'Your cart' })}
<div class="wrap">
  <div class="checkout">
    <div data-cart-page-lines aria-live="polite"></div>
    <aside class="checkout__summary"><div class="summary-box" data-cart-page-foot></div></aside>
  </div>
</div>`;
  return layout({
    title: 'Cart',
    description: 'Review the items in your cart.',
    path: '/cart/',
    body,
    noindex: true,
  });
}
