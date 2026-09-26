import { STORE } from '../data/store.js';
import { txt, esc } from '../utils/html.js';
import { layout, pageHead, breadcrumbSchema } from '../components/layout.js';

const CRUMBS = [
  { name: 'Home', path: '/' },
  { name: 'Contact', path: '/contact/' },
];

export function contactPage() {
  const body = `
${pageHead({ crumbs: CRUMBS, eyebrow: 'Customer support', title: 'How can we help?', lead: `Questions about an order, sizing, returns or a gift — write to us and a person will reply ${STORE.responseTime}.` })}
<section class="section" style="padding-top:clamp(40px,6vw,80px)">
  <div class="wrap grid-2" style="align-items:start">
    <form data-contact novalidate>
      <div class="field-row">
        <div class="field"><label for="c-name">Name</label><input class="input" id="c-name" name="name" autocomplete="name" required></div>
        <div class="field"><label for="c-email">Email</label><input class="input" id="c-email" name="email" type="email" autocomplete="email" required></div>
      </div>
      <div class="field-row">
        <div class="field"><label for="c-topic">Topic</label>
          <select class="input" id="c-topic" name="topic">
            <option>Order question</option><option>Shipping &amp; delivery</option><option>Returns &amp; refunds</option><option>Product question</option><option>Warranty</option><option>Something else</option>
          </select></div>
        <div class="field"><label for="c-order">Order number <span class="muted">(optional)</span></label><input class="input" id="c-order" name="order" autocomplete="off"></div>
      </div>
      <div class="field"><label for="c-msg">Message</label><textarea class="input" id="c-msg" name="message" required></textarea></div>
      <p class="muted small">We use your details only to answer this message. See our <a href="/policies/privacy/">Privacy Policy</a>.</p>
      <button class="btn" type="submit">Send message</button>
      <p class="form-msg" data-contact-msg role="status" hidden></p>
    </form>
    <div class="cards" style="grid-template-columns:1fr">
      <div class="card"><h3>Email</h3><p>${txt(STORE.supportEmail)}</p></div>
      ${STORE.phone ? `<div class="card"><h3>Phone</h3><p>${esc(STORE.phone)}</p></div>` : ''}
      <div class="card"><h3>Hours</h3><p>${txt(STORE.supportHours)}</p></div>
      <div class="card"><h3>Company</h3><p>${txt(STORE.legalName)}<br>${txt(STORE.address)}<br>${txt(STORE.registration)}</p></div>
      <div class="card"><h3>Order already placed?</h3><p><a href="/track/">Track your order</a> · <a href="/policies/returns/">Start a return</a> · <a href="/policies/cancellation/">Cancel an order</a></p></div>
    </div>
  </div>
</section>`;
  return layout({
    title: 'Contact & support',
    description: `Contact ${STORE.brand} customer support about orders, shipping, returns and product questions.`,
    path: '/contact/',
    body,
    schema: [breadcrumbSchema(CRUMBS)],
    scripts: ['forms'],
  });
}
