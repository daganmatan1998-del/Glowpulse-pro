# Glowpulse Pro

A fast, minimal, mobile-first Shopify theme built on **Online Store 2.0**, designed for
single-product and small-catalogue (dropshipping) stores.

Everything a merchant normally wants to change — images, text, products, colours, spacing,
buttons, header and footer — is editable from **Online Store → Themes → Customize**.
Nothing is hardcoded.

## Installing

1. Download this repository as a ZIP (or `git archive`), making sure the ZIP contains the
   `assets/`, `config/`, `layout/`, `locales/`, `sections/`, `snippets/` and `templates/`
   folders **at its root** — not nested inside another folder.
2. In your Shopify admin go to **Online Store → Themes → Add theme → Upload zip file**.
3. Publish it, or click **Customize** to preview first.

Using the Shopify CLI instead:

```bash
shopify theme push --unpublished
```

## First-run checklist

1. **Online Store → Navigation** — create a menu for the hamburger drawer with:
   `Contact Us`, `Reviews`, `Product Specifications`, and a `Policies` parent link whose
   children are your Shipping / Refund / Privacy / Terms pages.
   Then pick it in **Customize → Header → Hamburger menu**.
2. **Settings → Policies** — fill these in; the footer's *Store policies* block lists them
   automatically.
3. **Customize → Featured product** — pick the product you want on the homepage.
4. **Customize → Hero** — upload your image(s). Each Image block takes a separate desktop
   and mobile image so nothing is cropped badly on phones.
5. **Theme settings → Colors / Typography** — set your brand palette and fonts.

## Structure

```
assets/     base.css, theme.js          (one stylesheet, one script, no libraries)
config/     settings_schema.json, settings_data.json
layout/     theme.liquid, password.liquid
locales/    en.default.json, en.default.schema.json
sections/   29 sections + header-group.json / footer-group.json
snippets/   16 shared partials
templates/  12 JSON templates + gift card + customer account pages
```

### Homepage sections

Each one can be added, removed, reordered, hidden and restyled in the theme editor:

| Section | What it does |
| --- | --- |
| Hero | Heading, text and CTA above, below, or overlaid on one or more images |
| Image gallery | A grid of editable images with captions and links |
| Featured product | Full buy box for a picked product, with gallery and variants |
| Product benefits | Icon + title + text columns |
| Image with text | Two-column story block with bullet points |
| Reviews | Star ratings, quotes, avatars and photos; swipeable on mobile |
| FAQ | Accordion with optional FAQ structured data for search engines |
| Call to action | Boxed or plain closing CTA |
| Shipping information | Delivery / returns columns with icons |
| Product specifications | Label + value spec table |
| Trust badges | Payment / guarantee badges, image or icon |
| Featured collection | Product grid from a chosen collection |
| Rich text, Newsletter, Contact form, Announcement bar | — |

### Product page

Built from reorderable blocks: vendor, title, star rating, price (with compare-at and a
saving badge), short text, variant picker (buttons or dropdowns), quantity + add to cart +
Buy it now, highlight lines, full description, collapsible rows (shipping, returns, specs),
SKU, custom Liquid, and app blocks. A sticky add-to-cart bar appears once the main buy
button scrolls away.

## Design and performance notes

- **Mobile first.** Every layout starts at 390px and scales up; the desktop breakpoint is
  750px, with a wide layout at 990px.
- **No horizontal scroll.** `overflow-x: clip` on `body` plus `minmax(0, …)` grid tracks.
  Verified at 390 / 768 / 1440px.
- **Images never dominate.** Hero and product galleries have a *Maximum image width*
  setting and separate desktop/mobile aspect ratios, so no full-bleed viewport banners.
- **Separate desktop and mobile controls** for font sizes, image ratios, section padding,
  column counts, image area width and header height.
- **No JavaScript frameworks.** `theme.js` is a handful of custom elements
  (`product-form`, `variant-picker`, `product-gallery`, `quantity-input`, `cart-items`)
  plus delegated listeners, so section re-renders need no re-initialisation.
- **Lazy loading** everywhere except the first hero and product image, which are `eager`
  with `fetchpriority="high"` for LCP. All images go through `image_url` with `srcset`
  and `sizes`, and carry explicit `width`/`height` to avoid layout shift.
- **Accessibility.** Semantic landmarks, one `h1` per page, focus-visible rings, focus
  trapping and Escape handling in drawers, ARIA labels on icon buttons, 44px tap targets,
  and `prefers-reduced-motion` respected.
- **SEO.** Open Graph and Twitter tags, Product and Organization JSON-LD, optional FAQ
  structured data, canonical URLs and alt-text fields on every image picker.

## Validation

The theme passes Shopify's official `theme-check` with zero offenses across all 82 enabled
checks, and its rendering was verified in Chromium at mobile, tablet and desktop widths.
