import { STORE, MARKETS, SHIPPING } from '../data/store.js';
import { txt, esc } from '../utils/html.js';
import { ICONS } from '../components/icons.js';
import { layout } from '../components/layout.js';

export function checkoutPage() {
  const connected = Boolean(STORE.integrations.checkoutUrl);
  const body = `
<div class="wrap">
  <div class="checkout checkout--pay">
    <form data-checkout novalidate>
      <p class="eyebrow" style="margin-top:8px">Secure checkout</p>
      <h1 class="h2" style="margin-bottom:40px">Checkout</h1>
      <fieldset>
        <legend><span>1</span>Contact</legend>
        <div class="field"><label for="co-email">Email</label><input class="input" id="co-email" name="email" type="email" autocomplete="email" required></div>
        <label class="check"><input type="checkbox" name="marketing"> <span>Email me about new releases and offers. (Optional — you can unsubscribe any time.)</span></label>
      </fieldset>
      <fieldset>
        <legend><span>2</span>Delivery</legend>
        <div class="field"><label for="co-country">Country / region</label><select class="input" id="co-country" name="country" autocomplete="country" required data-country-select></select></div>
        <div class="field-row">
          <div class="field"><label for="co-first">First name</label><input class="input" id="co-first" name="firstName" autocomplete="given-name" required></div>
          <div class="field"><label for="co-last">Last name</label><input class="input" id="co-last" name="lastName" autocomplete="family-name" required></div>
        </div>
        <div class="field"><label for="co-addr">Address</label><input class="input" id="co-addr" name="address1" autocomplete="address-line1" required></div>
        <div class="field"><label for="co-addr2">Apartment, suite, etc. <span class="muted">(optional)</span></label><input class="input" id="co-addr2" name="address2" autocomplete="address-line2"></div>
        <div class="field-row">
          <div class="field"><label for="co-city">City</label><input class="input" id="co-city" name="city" autocomplete="address-level2" required></div>
          <div class="field"><label for="co-zip">Postal code</label><input class="input" id="co-zip" name="zip" autocomplete="postal-code" required></div>
        </div>
        <div class="field"><label for="co-phone">Phone <span class="muted">(for delivery updates, optional)</span></label><input class="input" id="co-phone" name="phone" type="tel" autocomplete="tel"></div>
        <p class="muted small">Processing: ${txt(SHIPPING.processingTime)}. Shipping cost and transit time for your country are shown at the payment step. ${txt(MARKETS.dutiesNotice)}</p>
      </fieldset>
      <fieldset>
        <legend><span>3</span>Payment</legend>
        <p class="muted">You will enter payment details on our payment provider's secure page. Accepted: ${esc(STORE.payments.accepted.join(', '))}.</p>
        <label class="check"><input type="checkbox" name="terms" required> <span>I have read and agree to the <a href="/policies/terms/">Terms of Service</a>, <a href="/policies/returns/">Returns Policy</a> and <a href="/policies/privacy/">Privacy Policy</a>.</span></label>
        ${
          connected
            ? ''
            : `<div class="notice" role="note"><strong>Online payment is not available yet.</strong> Our checkout is being connected. To order now, email ${txt(STORE.supportEmail)} and we will reply with a secure payment link.</div>`
        }
        <button class="btn btn--block" type="submit"${connected ? '' : ' disabled aria-disabled="true"'}>${ICONS.lock.replace('<svg', '<svg width="16" height="16"')} Continue to secure payment</button>
        <p class="form-msg" data-checkout-msg role="status" hidden></p>
      </fieldset>
    </form>
    <aside class="checkout__summary">
      <div class="summary-box">
        <h2 class="label" style="display:block;margin-bottom:8px">Order summary</h2>
        <div data-checkout-lines></div>
        <div data-checkout-totals></div>
        <p class="secure-note">${ICONS.lock}<span>${txt(STORE.payments.note)}</span></p>
        <p class="secure-note">${ICONS.returns}<span>Changed your mind? See our <a href="/policies/returns/">returns</a> and <a href="/policies/cancellation/">cancellation</a> policies.</span></p>
      </div>
    </aside>
  </div>
</div>`;
  return layout({
    title: 'Checkout',
    description: 'Secure checkout.',
    path: '/checkout/',
    body,
    noindex: true,
    scripts: ['checkout'],
  });
}
