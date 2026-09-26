# NORRVAL — storefront for the Nocturne watch

A complete, static, English-language storefront for **NORRVAL**, a fictional
direct-to-consumer watch brand built around one product: **Nocturne**, the
all-black mesh watch in the reference photo.

- No framework and no runtime dependencies. Pages are pre-rendered HTML;
  JavaScript is ~15 KB (minified) of vanilla code, deferred and split per page.
- Every price, claim, policy value and contact detail lives in `data/store.js`.
- Nothing is invented: no reviews, no specs, no discounts, no stock counts, no
  countdowns. Anything the business must supply is a `[PLACEHOLDER]`,
  highlighted in yellow on the page so you can't miss it.

## Quick start

```bash
cd norrval-store
npm install               # esbuild, for minification only
bash scripts/fetch-images.sh   # downloads the Higgsfield shots + builds AVIF/WebP/JPEG
npm run build             # renders everything into dist/
npm run check             # lists every placeholder left + broken links
npm run serve             # http://localhost:4173
```

`dist/` is a plain static site. Deploy it to any static host (Cloudflare Pages,
Netlify, Vercel, S3 + CloudFront). Configure the host to serve `404.html` for
unknown paths.

## Structure

```
data/        store.js (the one config file), images.js (image slots + prompts), reviews.js
components/  layout (head/SEO, header, footer, cart drawer, consent, region), picture, icons
pages/       home, product, about, contact, faq, track, cart, checkout, legal, 404
legal/       policy templates (shipping, returns, cancellation, terms, privacy, cookies, accessibility)
styles/      main.css — the whole design system
assets/js/   app.js (every page) + home.js, product.js, checkout.js, forms.js
assets/      source/ (original generations), img/ (responsive output), favicon.svg
scripts/     build.mjs, images.py, fetch-images.sh, check.mjs
```

Clean URLs: `/`, `/products/nocturne/`, `/about/`, `/faq/`, `/contact/`,
`/track/`, `/cart/`, `/checkout/`, `/accessibility/`, `/policies/{shipping,
returns, cancellation, terms, privacy, cookies}/`.

## Images

`data/images.js` defines ten slots. Each slot has its own aspect ratio, alt
text, focal point and **the exact Higgsfield prompt** used (or to use) for it.
Every shot is generated for its container. None is a crop of another.

| Slot | Ratio | Where | Status |
|---|---|---|---|
| hero-desktop | 16:9 | Home hero (≥768px) | Generated |
| hero-mobile | 4:5 | Home hero (<768px) | Generated |
| product | 1:1 | Product gallery, showcase, cart thumbnails | Generated |
| lifestyle | 4:5 | Lifestyle scroller, About, product page | Generated |
| gift | 4:5 | Gifting section, product gallery | Generated |
| closeup | 4:5 | "Built to stand out", showcase, gallery | Prompt ready |
| business | 4:5 | Lifestyle scroller, gallery | Prompt ready |
| side | 1:1 | Showcase, gallery | Prompt ready |
| wrist | 4:5 | Lifestyle scroller, product page, gallery | Prompt ready |
| ad | 1:1 | Paid social; also the Open Graph image | Prompt ready |

A slot with no image is left out of the gallery and the lifestyle scroller. In
a section that needs an image, it falls back to a *different* real shot
(`fallback` in the registry) and uses that shot's own alt text. You never get a
broken image or a stretched crop. When you add a file, the slot switches on
automatically.

**To add or replace an image:** put the PNG/JPG at `assets/source/<slot>.png`,
then `npm run images && npm run build`. `scripts/images.py` produces 480–2000px
AVIF, WebP and JPEG versions at the original aspect ratio (it never crops),
and the page serves them through `<picture>` with `srcset`, `sizes`, explicit
`width`/`height`, lazy loading below the fold and `fetchpriority="high"` on
the hero.

**Generating the remaining five in Higgsfield:** use the prompt from
`data/images.js`, the aspect ratio listed, and as references the original
product photo plus the generated shots in `REFERENCE_JOBS` (so the watch stays
identical). The supplier's box lid carries someone else's logo. Every prompt
asks for a plain lid; check that each output has one.

## Configuration checklist (before launch)

Edit `data/store.js`, rebuild, then run `npm run check` until it reports zero
placeholders.

1. **Business identity:** legal name, address, registration, VAT, emails, hours.
2. **Price:** `PRODUCT.price.USD` is set to **149 as an example**. Set your
   real price. Only set `compareAtPrice` if the watch has genuinely sold at that
   price.
3. **Specifications:** fill `null` rows (diameter, material, movement, water
   resistance…) **only** with figures confirmed by the manufacturer.
4. **What's in the box:** confirm the beaded bracelet and any other contents.
5. **Shipping:** countries, processing time, zones, carriers. Set `tracked`
   to `true` only if every order ships with tracking. Set
   `freeShippingThreshold` only if you offer it (the announcement bar and cart
   meter appear automatically).
6. **Returns & warranty:** window, address, who pays, refund time, warranty.
7. **Payments:** list only methods your provider has enabled; set
   `integrations.checkoutUrl` to your hosted checkout (Shopify cart permalink,
   Stripe Payment Link, etc.). Until then, checkout says plainly that online
   payment isn't connected and offers email ordering.
8. **Forms:** `contactFormEndpoint` (until set, the contact form opens the
   visitor's email app), `newsletterEndpoint`, `trackingUrl`.
9. **Analytics:** `analyticsScriptUrl`. It loads only after the visitor opts
   in. List the cookies it sets in the Cookie Policy.
10. **Domain:** `siteUrl` (canonical URLs, Open Graph, sitemap, JSON-LD).
11. **Brand:** "NORRVAL" and "Nocturne" were chosen to avoid existing watch
    brands, but **run a trademark search** in each market before launch.
12. **Legal review:** the policies are templates. Having them does not make
    the store compliant. Have a lawyer review them for every country you sell
    into (EU/UK consumer withdrawal rights are flagged inline).

## Offers

`OFFERS` in `data/store.js` is off by default. Turn on `launch` (headline,
body, code, end date shown as text, never as a countdown) or `bundle`
(`quantity` + `percentOff`, applied in the cart automatically) only when the
terms are real.

## Reviews

`data/reviews.js` is empty on purpose. Add real reviews (with permission) and
the homepage and product page show them. Until then the homepage says "Join
the first generation of NORRVAL owners."

## What's built in

- **SEO:** unique titles and descriptions, canonical URLs, Open Graph and
  Twitter cards, JSON-LD for Organization, WebSite, Product (price and
  availability from config; no rating unless you add one), BreadcrumbList and
  FAQPage (answers still holding placeholders are left out). Also
  `sitemap.xml`, `robots.txt` (cart and checkout disallowed and `noindex`) and
  one H1 per page.
- **Motion:** a line-reveal hero with a slow scale-in and light sweep; mask,
  blur and fade scroll reveals; a sticky showcase with three steps driven by
  scroll progress (scale, rotation, moving light, scene crossfades); a pinned
  horizontal lifestyle scroller; parallax; magnetic buttons; card glow; cart
  count bump; drawer and line animations; scroll-progress bar. All
  transform/opacity, one rAF loop, passive listeners.
  `prefers-reduced-motion` turns all of it off and falls back to static
  layouts.
- **Accessibility:** skip link, landmarks, labelled forms, focus-visible
  styles, focus trap and Escape for the drawer and menu, `aria-live` for the
  cart and form status, reduced-motion support.
- **Consent:** the banner offers Reject, Customise and Accept with equal
  weight. Optional categories are off by default and nothing is pre-ticked.
  The choice is stored for 12 months and can be changed from the footer. The
  newsletter and checkout marketing boxes are unticked and optional.
- **Mobile:** its own 4:5 hero, full-screen menu, a sticky add-to-cart bar on
  the product page, order summary first in checkout, 44px+ touch targets, and
  a swipeable scroller instead of the pinned one. Tested at 375, 390, 414,
  768, 1024 and 1440px with no horizontal overflow.
