/**
 * NORRVAL — single source of truth for the store.
 *
 * Every price, claim, policy value and contact detail on the site is read from
 * this file at build time. Change it here, run `npm run build`, and every page,
 * the structured data and the sitemap update together.
 *
 * RULES FOR EDITING
 * - A value of `null` hides the element that would show it. Leave a spec, an
 *   offer or a threshold as `null` until it is true and you can stand behind it.
 * - Anything in [SQUARE BRACKETS] is a placeholder the business must replace
 *   before launch. `npm run check` lists every one that is still left.
 */

export const STORE = {
  brand: 'NORRVAL',
  tagline: 'Modern watches, made to be worn.',
  // Used for canonical URLs, Open Graph, sitemap and structured data.
  siteUrl: 'https://www.example.com',
  locale: 'en',

  legalName: '[LEGAL BUSINESS NAME]',
  address: '[BUSINESS ADDRESS]',
  country: '[COUNTRY OF OPERATION]',
  registration: '[COMPANY REGISTRATION NUMBER]',
  vatNumber: '[TAX / VAT NUMBER, IF APPLICABLE]',
  contactEmail: '[CONTACT EMAIL]',
  supportEmail: '[SUPPORT EMAIL]',
  privacyEmail: '[PRIVACY CONTACT EMAIL]',
  phone: null, // e.g. '+1 555 000 0000' — leave null to hide
  supportHours: '[SUPPORT HOURS, e.g. Mon–Fri, 9:00–17:00 CET]',
  responseTime: '[SUPPORT RESPONSE TIME, e.g. within 1 business day]',
  socials: [
    // { name: 'Instagram', url: 'https://instagram.com/…' },
  ],

  // Third-party endpoints. Leave null until connected; the UI says so honestly.
  integrations: {
    // Hosted checkout URL (Shopify, Stripe Payment Links, etc.). `{items}` is
    // replaced with the cart as `variantId:qty,variantId:qty`; `{email}` and
    // `{country}` with what the shopper entered, so the provider can prefill.
    checkoutUrl: null,
    // Form backend (Formspree, Basin, your own API) for contact + newsletter.
    contactFormEndpoint: null,
    newsletterEndpoint: null,
    // Carrier or tracking-page URL. `{order}` and `{email}` are substituted.
    trackingUrl: null,
    // Analytics loads ONLY after the visitor accepts analytics cookies.
    analyticsScriptUrl: null,
  },

  payments: {
    // List only methods your payment provider actually has enabled.
    accepted: ['Visa', 'Mastercard', 'American Express', 'PayPal', 'Apple Pay', 'Google Pay'],
    note: 'Payment is processed by [PAYMENT PROVIDER]. We never see or store your full card number.',
  },
};

export const MARKETS = {
  // Only currencies with a price you have set appear in the selector. Prices
  // are never auto-converted.
  defaultCurrency: 'USD',
  currencies: {
    USD: { symbol: '$', locale: 'en-US' },
    // EUR: { symbol: '€', locale: 'en-IE' },
    // GBP: { symbol: '£', locale: 'en-GB' },
  },
  shippingCountries: '[SHIPPING COUNTRIES]',
  dutiesNotice:
    'Orders shipped outside [COUNTRY OF OPERATION] may be subject to import duties and taxes, which are set by the destination country. [STATE WHETHER THESE ARE COLLECTED AT CHECKOUT (DDP) OR PAID BY THE CUSTOMER ON DELIVERY (DDU).]',
};

export const SHIPPING = {
  // Orders at or above this amount ship free. null = no free-shipping claim anywhere.
  freeShippingThreshold: null,
  processingTime: '[PROCESSING TIME, e.g. 1–2 business days]',
  // Set true only if EVERY order ships with a tracking number.
  tracked: false,
  zones: [
    { region: '[REGION 1, e.g. Domestic]', method: '[METHOD]', transit: '[TRANSIT TIME]', cost: '[COST]' },
    { region: '[REGION 2, e.g. Europe]', method: '[METHOD]', transit: '[TRANSIT TIME]', cost: '[COST]' },
    { region: '[REGION 3, e.g. Rest of world]', method: '[METHOD]', transit: '[TRANSIT TIME]', cost: '[COST]' },
  ],
  carriers: '[CARRIERS]',
};

export const RETURNS = {
  window: '[RETURN WINDOW, e.g. 30 days]',
  returnAddress: '[RETURN ADDRESS]',
  returnShippingPaidBy: '[WHO PAYS RETURN SHIPPING]',
  refundTime: '[REFUND PROCESSING TIME]',
  warranty: '[WARRANTY TERMS]',
};

export const PRODUCT = {
  id: 'nocturne-black',
  sku: '[SKU]',
  gtin: null,
  slug: 'nocturne',
  name: 'Nocturne',
  fullName: 'NORRVAL Nocturne',
  subtitle: 'All-black mesh watch',
  // Price in the default currency. Set to your real retail price.
  price: { USD: 149 },
  // Only set if the product has genuinely been sold at this price before.
  compareAtPrice: null,
  availability: 'InStock', // InStock | PreOrder | OutOfStock
  shortDescription:
    'A matte black watch with a clean black dial, teal-accented hands and a fine black mesh bracelet. Quiet from across the room, sharp up close.',
  description: [
    'Nocturne is built around one idea: a watch that looks considered in every setting without asking for attention. The case, dial and bracelet are all finished in black, so the only colour is the teal on the hands and markers — enough to read at a glance, restrained enough to wear every day.',
    'The dial is kept deliberately clean, with slim markers and a single small sub-dial for balance. A fine black mesh bracelet follows the wrist closely and moves easily from a t-shirt to a tailored cuff.',
    'Every Nocturne arrives in a black presentation box, ready to keep or to give.',
  ],
  variants: [
    { id: 'nocturne-black-mesh', name: 'Black / Black mesh', available: true },
  ],
  // Visible, verified characteristics only. Add a row once a figure is confirmed
  // by the manufacturer; rows set to null are not rendered.
  specifications: [
    ['Case colour', 'Black'],
    ['Dial', 'Black, with teal-accented hands and markers'],
    ['Sub-dial', 'One small sub-dial'],
    ['Bracelet', 'Black mesh'],
    ['Presentation', 'Black gift box included'],
    ['Case diameter', null], // e.g. '41 mm'
    ['Case thickness', null],
    ['Case material', null],
    ['Crystal', null],
    ['Movement', null],
    ['Water resistance', null],
    ['Bracelet width', null],
    ['Weight', null],
  ],
  inTheBox: ['Nocturne watch', 'Black presentation box', 'Black beaded bracelet', '[CONFIRM ANY OTHER CONTENTS, e.g. care card]'],
  features: [
    {
      icon: 'dial',
      title: 'Minimalist design',
      body: 'Black on black, with teal only where it helps you read the time. Nothing on the dial that does not need to be there.',
    },
    {
      icon: 'layers',
      title: 'Everyday versatility',
      body: 'The mesh bracelet and matte finish sit as comfortably with a hoodie as they do with a suit.',
    },
    {
      icon: 'wrist',
      title: 'Statement wrist presence',
      body: 'A dark, graphic silhouette that reads clearly on the wrist without shouting about it.',
    },
    {
      icon: 'gift',
      title: 'Gift-ready presentation',
      body: 'Arrives in a black presentation box with a matching beaded bracelet. No extra wrapping needed.',
    },
  ],
};

/**
 * Promotions. Every block is off by default. Turn one on only when the terms
 * are real; the homepage offer section and product page read these.
 */
export const OFFERS = {
  launch: {
    enabled: false,
    label: 'Launch pricing',
    // e.g. 'Save 20% on your first order with code FIRST20. Ends [END DATE].'
    headline: null,
    body: null,
    code: null,
    endsOn: null, // ISO date. Shown as text only — never a countdown.
  },
  bundle: {
    enabled: false,
    // e.g. { quantity: 2, percentOff: 10 } — applied in the cart automatically.
    quantity: 2,
    percentOff: null,
  },
};

export const FAQ = [
  {
    q: 'What is included with the watch?',
    a: 'Each Nocturne comes in a black presentation box together with a black beaded bracelet. [CONFIRM CONTENTS.]',
  },
  {
    q: 'What are the case size and materials?',
    a: 'We only publish specifications that have been confirmed by our manufacturer. Confirmed figures appear in the Specifications section of the product page. If a figure you need is not listed yet, email [SUPPORT EMAIL] and we will answer directly.',
  },
  {
    q: 'Is the watch water resistant?',
    a: 'We do not currently make a water-resistance claim for Nocturne. Until one is published on the product page, please keep the watch away from water.',
  },
  {
    q: 'Can I adjust the bracelet?',
    a: '[DESCRIBE HOW THE MESH BRACELET CLASP ADJUSTS, e.g. sliding clasp, tool-free.]',
  },
  {
    q: 'How long does shipping take?',
    a: 'Orders are processed within [PROCESSING TIME]. Transit time depends on the destination — see the Shipping Policy for each region. Your estimated delivery is processing time plus transit time.',
  },
  {
    q: 'Do you ship internationally?',
    a: 'We currently ship to [SHIPPING COUNTRIES]. Import duties and taxes may apply outside [COUNTRY OF OPERATION]; see the Shipping Policy for details.',
  },
  {
    q: 'What is your return policy?',
    a: 'You can return an unworn watch in its original packaging within [RETURN WINDOW] of delivery. Full details are in the Returns & Refund Policy.',
  },
  {
    q: 'Is there a warranty?',
    a: '[WARRANTY TERMS]',
  },
  {
    q: 'Is checkout secure?',
    a: 'Payments are handled by [PAYMENT PROVIDER] over an encrypted connection. Card details go directly to the payment provider and are never stored by us.',
  },
  {
    q: 'Can I send it as a gift?',
    a: 'Yes. Every watch ships in its presentation box. You can enter a different delivery address at checkout. [STATE WHETHER PRICES ARE HIDDEN ON THE PACKING SLIP.]',
  },
];
