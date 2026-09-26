/**
 * Legal page templates.
 *
 * These are starting points, not legal advice. Having these pages does not by
 * itself make the store compliant with any law. Every [PLACEHOLDER] must be
 * completed, and the finished text reviewed by a qualified lawyer for each
 * country you sell into, before launch.
 */
import { STORE, SHIPPING, RETURNS, MARKETS } from '../data/store.js';

const S = STORE;
const brand = S.brand;
const updated = '[LAST UPDATED DATE]';
const contactBlock = `<p>${S.legalName}<br>${S.address}<br>Email: ${S.supportEmail}${S.phone ? `<br>Phone: ${S.phone}` : ''}</p>`;

export const POLICIES = [
  {
    slug: 'shipping',
    path: '/policies/shipping/',
    title: 'Shipping Policy',
    description: `How ${brand} processes and ships orders, delivery times by region, costs, customs duties and what to do if a parcel is delayed.`,
    html: `
<p><em>Last updated: ${updated}</em></p>
<h2>1. Where we ship</h2>
<p>We currently ship to ${MARKETS.shippingCountries}. If your country is not listed at checkout, we are not able to deliver there yet.</p>
<h2>2. Processing time</h2>
<p>Orders are processed within ${SHIPPING.processingTime} after payment is confirmed, excluding weekends and public holidays in ${S.country}. Processing time is the time it takes us to prepare your order and hand it to the carrier.</p>
<h2>3. Shipping time, costs and methods</h2>
<p>Shipping time is the time the carrier takes to deliver after collecting the parcel. It is an estimate provided by the carrier and is not guaranteed.</p>
<table><thead><tr><th>Region</th><th>Method</th><th>Estimated transit</th><th>Cost</th></tr></thead><tbody>
${SHIPPING.zones.map((z) => `<tr><td>${z.region}</td><td>${z.method}</td><td>${z.transit}</td><td>${z.cost}</td></tr>`).join('')}
</tbody></table>
${SHIPPING.freeShippingThreshold ? `<p>Orders of ${SHIPPING.freeShippingThreshold} ${MARKETS.defaultCurrency} or more (after discounts, before taxes) ship free using our standard method to eligible destinations.</p>` : ''}
<h2>4. Estimated delivery</h2>
<p>Your estimated delivery date is processing time plus shipping time. The estimate shown at checkout and in your confirmation email is a guide, not a guaranteed date.</p>
<h2>5. Carriers and tracking</h2>
<p>We ship with ${SHIPPING.carriers}. You will receive a shipping confirmation email once your order has been dispatched${SHIPPING.tracked ? ', including a tracking number. You can also use our <a href="/track/">order tracking page</a>' : '. [STATE WHICH SHIPPING METHODS INCLUDE TRACKING.]'}.</p>
<h2>6. Customs, duties and taxes</h2>
<p>${MARKETS.dutiesNotice}</p>
<p>[STATE WHETHER PRICES SHOWN INCLUDE VAT/SALES TAX FOR EACH REGION.]</p>
<h2>7. Address accuracy</h2>
<p>Please check your delivery address carefully. If you notice a mistake, contact us at ${S.supportEmail} as soon as possible. We can update the address only before the order has been dispatched. [STATE WHO BEARS THE COST IF A PARCEL IS RETURNED DUE TO AN INCORRECT ADDRESS.]</p>
<h2>8. Delayed, lost or damaged parcels</h2>
<p>If your tracking has not updated for [NUMBER] business days, or your parcel arrives damaged, contact us at ${S.supportEmail} with your order number and, for damage, photos of the parcel and contents. We will work with the carrier to resolve it. [DESCRIBE YOUR REMEDY: REPLACEMENT, REFUND, TIMEFRAMES.]</p>
<h2>9. Contact</h2>
${contactBlock}`,
  },
  {
    slug: 'returns',
    path: '/policies/returns/',
    title: 'Returns & Refund Policy',
    description: `How to return a ${brand} order, the return window, condition requirements, refunds, exchanges and warranty.`,
    html: `
<p><em>Last updated: ${updated}</em></p>
<h2>1. Return window</h2>
<p>You may return items within ${RETURNS.window} of delivery. This policy is in addition to, and does not limit, any rights you have under the consumer law of your country.</p>
<p class="note">[IF YOU SELL TO CONSUMERS IN THE EU OR UK: those customers have a statutory right to cancel a distance purchase within 14 days of delivery without giving a reason. Make sure your return window and process meet that minimum, and provide the model withdrawal form where required. Have this section reviewed for each market you sell into.]</p>
<h2>2. Condition of returned items</h2>
<p>To receive a full refund, the watch should be unworn, undamaged, with any protective films in place, and returned with its presentation box and all included items. [STATE ANY DEDUCTION FOR DIMINISHED VALUE, IF PERMITTED BY LAW.]</p>
<h2>3. How to start a return</h2>
<ol>
<li>Email ${S.supportEmail} with your order number and the item(s) you want to return.</li>
<li>We will reply with return instructions [AND A RETURN LABEL, IF PROVIDED].</li>
<li>Pack the item securely in its original packaging and send it to:<br>${RETURNS.returnAddress}</li>
</ol>
<p>Please do not send returns without contacting us first, as unannounced parcels may be delayed.</p>
<h2>4. Return shipping costs</h2>
<p>${RETURNS.returnShippingPaidBy}. If an item arrived faulty or we sent the wrong item, we cover return shipping.</p>
<h2>5. Refunds</h2>
<p>Once we receive and inspect your return, we will email you to confirm. Approved refunds are issued to the original payment method within ${RETURNS.refundTime}. Your bank or card issuer may take additional time to post the refund. [STATE WHETHER ORIGINAL SHIPPING CHARGES ARE REFUNDED.]</p>
<h2>6. Exchanges</h2>
<p>[DESCRIBE WHETHER YOU OFFER EXCHANGES, OR ASK CUSTOMERS TO RETURN AND REORDER.]</p>
<h2>7. Gifts</h2>
<p>[DESCRIBE HOW GIFT RECIPIENTS CAN RETURN AN ITEM AND HOW THEY ARE REFUNDED, e.g. store credit or refund to the purchaser.]</p>
<h2>8. Faulty items and warranty</h2>
<p>${RETURNS.warranty}</p>
<p>If your watch develops a fault, contact ${S.supportEmail} with your order number, a description and photos or a short video. Warranty coverage does not affect your statutory rights.</p>
<h2>9. Order cancellation</h2>
<p>See our <a href="/policies/cancellation/">Order Cancellation Policy</a>.</p>
<h2>10. Contact</h2>
${contactBlock}`,
  },
  {
    slug: 'cancellation',
    path: '/policies/cancellation/',
    title: 'Order Cancellation Policy',
    description: `How to cancel or change a ${brand} order before it ships, and what happens after dispatch.`,
    html: `
<p><em>Last updated: ${updated}</em></p>
<h2>1. Before dispatch</h2>
<p>You can cancel your order free of charge at any point before it has been dispatched. Email ${S.supportEmail} with your order number as soon as possible. If the order has not yet been handed to the carrier, we will cancel it and issue a full refund to your original payment method within ${RETURNS.refundTime}.</p>
<h2>2. After dispatch</h2>
<p>Once an order has been dispatched it can no longer be cancelled, but you can return it under our <a href="/policies/returns/">Returns &amp; Refund Policy</a>.</p>
<h2>3. Changing an order</h2>
<p>We can change the delivery address or quantity only before dispatch. Contact us as early as possible.</p>
<h2>4. Cancellations by us</h2>
<p>We may cancel an order if an item is unavailable, if there is an obvious pricing error, or if we suspect fraud. If this happens we will tell you and refund any payment in full.</p>
<h2>5. Statutory rights</h2>
<p>Nothing in this policy limits any right to cancel that you have under the law of your country.</p>
<h2>6. Contact</h2>
${contactBlock}`,
  },
  {
    slug: 'terms',
    path: '/policies/terms/',
    title: 'Terms of Service',
    description: `The terms that apply when you use the ${brand} website and buy from us.`,
    html: `
<p><em>Last updated: ${updated}</em></p>
<h2>1. Who we are</h2>
<p>This website is operated by ${S.legalName} (“${brand}”, “we”, “us”), registered in ${S.country} under number ${S.registration}, with its address at ${S.address}. ${S.vatNumber}.</p>
<h2>2. Using this website</h2>
<p>By using this website or placing an order, you agree to these Terms. If you do not agree, please do not use the site. You must be at least [MINIMUM AGE] or have the permission of a parent or guardian to place an order.</p>
<h2>3. Products and descriptions</h2>
<p>We make every effort to describe and photograph our products accurately. Product images may be styled or digitally produced to show the product in use; colours can vary slightly between screens. Specifications are published only once confirmed by our manufacturer.</p>
<h2>4. Prices and payment</h2>
<p>Prices are shown in the currency selected on the site. [STATE WHETHER PRICES INCLUDE TAX FOR EACH REGION.] Shipping costs, taxes and any duties are shown before you pay. Payment is processed by [PAYMENT PROVIDER]; we do not store full card details.</p>
<p>If a product is listed at an obviously incorrect price due to an error, we may cancel the order and refund you in full.</p>
<h2>5. Orders and contract</h2>
<p>Your order is an offer to buy. [STATE WHEN THE CONTRACT IS FORMED, e.g. when we send the dispatch confirmation email.] We may decline an order for any lawful reason, in which case we will refund any payment.</p>
<h2>6. Delivery</h2>
<p>Delivery is governed by our <a href="/policies/shipping/">Shipping Policy</a>. Risk in the goods passes to you on delivery [CONFIRM FOR EACH MARKET].</p>
<h2>7. Returns, cancellation and warranty</h2>
<p>See our <a href="/policies/returns/">Returns &amp; Refund Policy</a> and <a href="/policies/cancellation/">Order Cancellation Policy</a>. Nothing in these Terms affects your statutory rights as a consumer.</p>
<h2>8. Promotions and discount codes</h2>
<p>Promotions are subject to the terms stated with them. Unless stated otherwise, discount codes cannot be combined, have no cash value and may be withdrawn at the end of the stated period.</p>
<h2>9. Intellectual property</h2>
<p>All content on this website — including the ${brand} name and logo, text, graphics, photographs, product designs and page layout — is owned by or licensed to ${S.legalName} and is protected by copyright and other intellectual-property laws. You may view and print pages for personal, non-commercial use. You may not copy, reproduce, republish or use any content for commercial purposes without our written permission. [CONFIRM TRADEMARK REGISTRATION STATUS BEFORE USING ® OR ™.]</p>
<h2>10. Acceptable use</h2>
<p>You agree not to misuse the site, including attempting to gain unauthorised access, interfering with its operation, or using automated means to collect content.</p>
<h2>11. Limitation of liability</h2>
<p>[LIMITATION OF LIABILITY CLAUSE — MUST BE DRAFTED FOR YOUR JURISDICTION. Consumer law in many countries restricts how far liability can be limited; do not copy a clause from another business.]</p>
<h2>12. Governing law</h2>
<p>These Terms are governed by the laws of [GOVERNING LAW JURISDICTION]. If you are a consumer, you may also benefit from mandatory protections of the law of the country where you live.</p>
<h2>13. Changes</h2>
<p>We may update these Terms from time to time. The version that applies to your order is the one published when you placed it.</p>
<h2>14. Contact</h2>
${contactBlock}`,
  },
  {
    slug: 'privacy',
    path: '/policies/privacy/',
    title: 'Privacy Policy',
    description: `What personal data ${brand} collects, why, how long it is kept, who it is shared with and how to exercise your privacy rights.`,
    html: `
<p><em>Last updated: ${updated}</em></p>
<h2>1. Who is responsible for your data</h2>
<p>${S.legalName}, ${S.address}, is the controller of personal data collected through this website. Privacy questions: ${S.privacyEmail}. [DATA PROTECTION OFFICER / EU OR UK REPRESENTATIVE, IF REQUIRED.]</p>
<h2>2. What we collect</h2>
<ul>
<li><strong>Order information</strong> — name, email, delivery and billing address, phone number (optional), items purchased.</li>
<li><strong>Payment information</strong> — processed directly by [PAYMENT PROVIDER]. We receive confirmation of payment and limited details (such as card type and last four digits), never the full card number.</li>
<li><strong>Messages</strong> — what you send us through the contact form or by email.</li>
<li><strong>Marketing preferences</strong> — your email address, if you choose to subscribe.</li>
<li><strong>Technical data</strong> — information stored in your browser to run the cart and remember your cookie choice; analytics data only if you consent.</li>
</ul>
<h2>3. Why we use it and our legal basis</h2>
<table><thead><tr><th>Purpose</th><th>Legal basis [CONFIRM FOR YOUR JURISDICTION]</th></tr></thead><tbody>
<tr><td>Processing and delivering your order, customer service, returns</td><td>Performance of a contract</td></tr>
<tr><td>Accounting, tax and legal record-keeping</td><td>Legal obligation</td></tr>
<tr><td>Fraud prevention and site security</td><td>Legitimate interests</td></tr>
<tr><td>Marketing emails</td><td>Consent (withdraw at any time)</td></tr>
<tr><td>Analytics and marketing cookies</td><td>Consent (withdraw at any time)</td></tr>
</tbody></table>
<h2>4. Who we share it with</h2>
<p>We share data only with service providers who help us run the store, under contracts that require them to protect it: [LIST PROVIDERS, e.g. hosting, e-commerce platform, payment provider, shipping carriers, email service, analytics]. We do not sell your personal data.</p>
<h2>5. International transfers</h2>
<p>[DESCRIBE WHETHER DATA IS TRANSFERRED OUTSIDE YOUR COUNTRY/REGION AND THE SAFEGUARDS USED.]</p>
<h2>6. How long we keep it</h2>
<p>[RETENTION PERIODS, e.g. order records for the period required by tax law; marketing data until you unsubscribe; support messages for X months.]</p>
<h2>7. Your rights</h2>
<p>Depending on where you live, you may have the right to access, correct, delete or export your data, to object to or restrict certain processing, and to withdraw consent at any time. To make a request, email ${S.privacyEmail}. You may also complain to your local data-protection authority [NAME THE RELEVANT AUTHORITY].</p>
<h2>8. Cookies</h2>
<p>See our <a href="/policies/cookies/">Cookie Policy</a>. You can change your choice at any time using “Cookie settings” in the footer.</p>
<h2>9. Children</h2>
<p>This site is not directed at children under [AGE] and we do not knowingly collect their data.</p>
<h2>10. Security</h2>
<p>The site is served over HTTPS and payments are handled by [PAYMENT PROVIDER] [CONFIRM THE PROVIDER'S PCI-DSS COMPLIANCE]. [DESCRIBE OTHER MEASURES.]</p>
<h2>11. Changes</h2>
<p>We will post any changes to this policy on this page with a new “last updated” date.</p>`,
  },
  {
    slug: 'cookies',
    path: '/policies/cookies/',
    title: 'Cookie Policy',
    description: `Which cookies and browser storage ${brand} uses, why, and how to change your choice.`,
    html: `
<p><em>Last updated: ${updated}</em></p>
<h2>1. What this covers</h2>
<p>Cookies and similar technologies (such as your browser's local storage) are small pieces of data stored on your device. This policy explains which ones this website uses.</p>
<h2>2. Your choice</h2>
<p>Optional cookies are <strong>off</strong> until you switch them on. Rejecting them is as easy as accepting them, and it does not affect your ability to shop. <button class="linklike" type="button" data-consent-open style="color:var(--accent);text-decoration:underline">Open cookie settings</button></p>
<h2>3. Strictly necessary (always on)</h2>
<table><thead><tr><th>Name</th><th>Type</th><th>Purpose</th><th>Duration</th></tr></thead><tbody>
<tr><td>norrval_cart</td><td>Local storage</td><td>Keeps the items in your cart</td><td>Until cleared</td></tr>
<tr><td>norrval_consent</td><td>Local storage</td><td>Remembers your cookie choice</td><td>12 months</td></tr>
<tr><td>norrval_region</td><td>Local storage</td><td>Remembers your country and currency</td><td>Until cleared</td></tr>
<tr><td>[PAYMENT / PLATFORM COOKIES]</td><td>Cookie</td><td>[SET BY YOUR CHECKOUT PROVIDER — LIST THEM]</td><td>[DURATION]</td></tr>
</tbody></table>
<h2>4. Analytics (optional)</h2>
<p>[LIST ANALYTICS TOOL, COOKIE NAMES, PURPOSE, DURATION AND PROVIDER — OR STATE THAT NONE ARE USED.] Loaded only if you switch on Analytics.</p>
<h2>5. Marketing (optional)</h2>
<p>[LIST ADVERTISING PIXELS, COOKIE NAMES, PURPOSE, DURATION AND PROVIDER — OR STATE THAT NONE ARE USED.] Loaded only if you switch on Marketing.</p>
<h2>6. Managing cookies in your browser</h2>
<p>You can also delete or block cookies in your browser settings. Blocking strictly necessary storage may stop the cart from working.</p>
<h2>7. Contact</h2>
<p>Questions: ${S.privacyEmail}.</p>`,
  },
];

export const ACCESSIBILITY = {
  path: '/accessibility/',
  title: 'Accessibility Statement',
  description: `${brand}'s commitment to an accessible website, the measures we take, known limitations and how to report a barrier.`,
  html: `
<p><em>Last updated: ${updated}</em></p>
<h2>Our commitment</h2>
<p>We want everyone to be able to browse and buy from ${brand}. We aim to meet the Web Content Accessibility Guidelines (WCAG) 2.2 at level AA. This is our goal, not a certification; we have not yet had an independent audit. [UPDATE IF AN AUDIT IS COMPLETED.]</p>
<h2>What we do</h2>
<ul>
<li>Semantic HTML with a logical heading structure and landmarks.</li>
<li>A “skip to content” link and full keyboard navigation, with visible focus indicators.</li>
<li>Descriptive alternative text for product and editorial images.</li>
<li>Text and interface colours chosen for strong contrast against the dark background.</li>
<li>Animations are reduced or switched off when your device is set to “reduce motion”.</li>
<li>Form fields have visible labels, and status messages are announced to screen readers.</li>
</ul>
<h2>Known limitations</h2>
<p>[LIST ANY KNOWN ISSUES, e.g. third-party checkout or embedded content you do not control.]</p>
<h2>Feedback and help</h2>
<p>If you find a barrier on this site, or need information in a different format, please email ${S.supportEmail}${S.phone ? ` or call ${S.phone}` : ''}. We aim to respond ${S.responseTime} and to fix issues as quickly as we can. We can also take your order by email if the checkout does not work for you.</p>`,
};
