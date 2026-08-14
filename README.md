# GlowPulse

A complete, ready-to-publish Shopify Online Store 2.0 theme built for dropshipping stores that import products through DSers. Everything on the storefront is in English.

**Setup guide:** [`docs/SETUP.md`](docs/SETUP.md) · **מדריך בעברית:** [`docs/SETUP-HE.md`](docs/SETUP-HE.md)

---

## What is in the box

**Pages** — homepage, product page, collection page, cart, reviews page, FAQ page, about page, contact page, policy page layout, search, 404, blog, article, collections list, password page, gift card, and the full set of customer account pages.

**Built for imported products.** AliExpress listings come with sparse variant matrices, a photo per colour and enormous HTML descriptions. The product page handles all three: colour options become image swatches taken from the variant photos, combinations that do not exist are struck through rather than silently broken, options past a configurable count collapse into a dropdown, and long descriptions sit in a collapsible row instead of burying the buy button.

**Reviews without an app.** Reviews are section blocks you edit in the theme editor — rating, headline, body, name, location, date, verified tag, customer photo. The reviews page calculates its average and star distribution from those blocks, so the summary always matches what a visitor can actually read. Install Judge.me, Loox or Okendo later and the theme picks up the standard `reviews.rating` metafields automatically, with no changes needed.

**Conversion sections.** Rotating announcement bar, animated trust ticker, benefit columns, comparison table, how-it-works steps, countdown banners, free shipping progress bar, sticky add-to-cart on mobile, low stock indicator, delivery estimate, trust icons, FAQ with search-engine structured data.

**Animation, tastefully.** Scroll reveals in four styles, staggered grids, hover lift, image zoom, gradient drift, marquee scroll, count-up numbers, animated rating bars. Speed and style are theme settings, and every effect switches off automatically under `prefers-reduced-motion`.

**No build step.** Plain Liquid, one CSS file, one JS file, zero dependencies. Edit and push.

---

## Quick start

```bash
# Package for the Shopify admin uploader
./scripts/package.sh

# Or develop against a live preview
npm install -g @shopify/cli @shopify/theme
shopify theme dev --store your-store.myshopify.com
```

Then follow [`docs/SETUP.md`](docs/SETUP.md) to create the menus, pages and policies.

---

## Structure

```
assets/          base.css, animations.css, theme.js
config/          theme settings schema and defaults
layout/          theme.liquid, password.liquid
locales/         en.default.json
sections/        29 sections, including header/footer groups
snippets/        product card, price, stars, cart line, icons, swatches
templates/       JSON templates for every page type
docs/            setup guides and policy copy to paste into Shopify
scripts/         package.sh
```

---

## Theme settings

Brand · Colors · Typography · Layout & shape · Buttons · Animations · Product cards · Cart · Social media · Sharing image.

The colour tokens drive everything: two accent colours form the gradient used across buttons, the newsletter block and highlighted headings, so the whole theme re-skins from two colour pickers.

---

## A note on the placeholder copy

The theme ships with realistic English copy so the storefront never looks half-built. It is written to be edited, not published as-is. Two things in particular are yours to set honestly: the delivery estimates on the product page must match what your supplier really delivers, and the fallback review count should stay at zero until you have reviews you actually earned.

The policy files in `docs/policies/` are a starting point, not legal advice. Every placeholder looks like `{{ this }}` — search for `{{` before you publish.
