# Kalda — Bloom Thermal Pour-Over Set

A single-product storefront for a fictional DTC coffee-equipment brand. Static
HTML, CSS and vanilla JavaScript — no build step, no dependencies, no network
calls. Open `index.html` or serve the folder and it runs.

```
python3 -m http.server 8000
```

## The product

**Kalda Bloom** — a thermal pour-over set: a double-walled stoneware cone with
28 spiral ribs, a vacuum-sealed borosilicate carafe, a reusable 18/8 steel
micro-mesh filter and a walnut dosing scoop. $89, down from $124. Four
colourways: Clay, Bone, Basalt, Sage.

The problem it solves: home pour-over stalls when the water cools mid-brew,
which is what makes the cup taste thin and sour. The cone holds brew
temperature within 2°C from first pour to last drop.

## Imagery

There is no photography and no external assets. Every product shot is a
hand-built SVG "studio render" declared once as a `<symbol>` and placed with
`<use>`. The ceramic fills read CSS custom properties, so selecting a
colourway recolours the hero, the gallery, all six thumbnails, the cart
thumbnails and the final CTA in one repaint — no image swapping.

Six renders: the full set, the cone in detail, the brewed carafe, the steel
filter, a counter scene, and the four-colourway lineup.

## What actually works

Cart state lives in `localStorage` and is shared between the storefront and
checkout via a `kalda:change` event.

- Add to cart, quantity stepper, Buy Now, and two optional add-ons that become
  their own cart lines
- Cart drawer with per-line quantity control and removal
- Variant selection wired to per-colourway stock (Basalt is deliberately low)
- Wishlist toggle, persisted, with a nav badge
- Discount codes — `BLOOM10` (10% off), `MORNING15` (15% over $150),
  `FREESHIP` — validated with real minimum-spend rules
- Free-shipping progress meter, at $95, in both the buy box and the drawer
- Gallery: pointer-tracked magnifier on desktop, snapping horizontal rail on
  mobile, full-screen lightbox with scroll/pinch zoom and drag-to-pan
- Checkout: validated address and card fields (Luhn check on the card number),
  standard vs express delivery, tax, order confirmation with a generated order
  number and a real business-day delivery estimate, then the cart clears

Checkout is a demonstration — card details are format-checked only and nothing
is transmitted, stored or charged. The page says so where a buyer would look.

## Files

```
index.html              storefront — includes the SVG symbol library
checkout.html           checkout, with lightweight versions of the same symbols
assets/css/styles.css   design tokens, layout, responsive rules, motion
assets/css/checkout.css checkout-only styles
assets/js/store.js      cart, wishlist, discounts, persistence
assets/js/main.js       storefront interactions
assets/js/checkout.js   checkout validation and order flow
```

## Design notes

Warm stone ground (`#FAF7F2`), espresso ink (`#1C1512`), bronze CTA
(`#A16207`). Display type is a system serif stack (Iowan Old Style → Palatino →
Georgia); body is the system sans stack. No webfonts, so nothing blocks paint
and the page works offline.

Verified in Chromium at 375, 768, 1024 and 1440 px: no horizontal page
overflow at any width, no console errors, and the full purchase flow completes.
Animation is scroll-reveal, hover and state feedback only, and all of it is
disabled under `prefers-reduced-motion`.
