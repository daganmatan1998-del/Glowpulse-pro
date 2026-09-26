import { STORE } from '../data/store.js';
import { txt } from '../utils/html.js';
import { layout, pageHead, breadcrumbSchema } from '../components/layout.js';

const CRUMBS = [
  { name: 'Home', path: '/' },
  { name: 'Track order', path: '/track/' },
];

export function trackPage() {
  const body = `
${pageHead({ crumbs: CRUMBS, eyebrow: 'Orders', title: 'Track your order', lead: 'Enter your order number and the email you used at checkout. If your order has a tracking number, you will also find it in your shipping confirmation email.' })}
<section class="wrap" style="padding:clamp(40px,6vw,80px) var(--gutter) var(--section)">
  <form data-track novalidate style="max-width:560px">
    <div class="field"><label for="t-order">Order number</label><input class="input" id="t-order" name="order" required autocomplete="off" placeholder="e.g. #1001"></div>
    <div class="field"><label for="t-email">Email</label><input class="input" id="t-email" name="email" type="email" required autocomplete="email"></div>
    <button class="btn" type="submit">Track order</button>
    <p class="form-msg" data-track-msg role="status" hidden></p>
  </form>
  <div class="prose" style="padding-bottom:0">
    <h2>Understanding delivery times</h2>
    <p><strong>Processing time</strong> is how long we take to prepare and hand your order to the carrier. <strong>Shipping time</strong> is how long the carrier takes after that. Your <strong>estimated delivery</strong> is the two added together — see the <a href="/policies/shipping/">Shipping Policy</a> for each region.</p>
    <p>Something wrong with a delivery? Email ${txt(STORE.supportEmail)} with your order number.</p>
  </div>
</section>`;
  return layout({
    title: 'Track your order',
    description: `Track a ${STORE.brand} order with your order number and email.`,
    path: '/track/',
    body,
    schema: [breadcrumbSchema(CRUMBS)],
    scripts: ['forms'],
  });
}
