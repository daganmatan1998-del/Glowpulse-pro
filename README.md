# GlowPulse Pro

A single-product Shopify storefront for **GlowPulse Pro**, a red light therapy
LED face mask. The theme is pushed to a store over the Admin GraphQL API — no
ZIP export, no manual *Add theme* step in the admin.

## Layout

```
theme/                    Shopify theme files (uploaded verbatim)
  assets/glowpulse.css    Design system — tokens, components, responsive rules
  assets/glowpulse.js     Scroll reveal + FAQ accordion, no dependencies
  sections/gp-*.liquid    Eight storefront sections, each with a schema
  snippets/gp-*.liquid    Shared partials: asset loader, star rating, icon set
  templates/index.json    Homepage — section order and all copy
scripts/
  seed-product.mjs        Creates the product the storefront sells
  push-theme.mjs          Duplicates the live theme, uploads theme/ into it
  build-preview.mjs       Renders index.json to a static HTML preview
design-system/            Generated design tokens and rationale
preview/                  Build output (git-ignored)
```

## Page structure

Hero → trust bar → benefits → how it works → reviews → comparison → FAQ → offer.

Every section is a normal Shopify section with a `{% schema %}`, so all copy,
icons, ratings and table rows stay editable in the theme editor after upload.

## Deploying to a store

You need an Admin API access token with `write_themes` and `write_products`.
Create one in the store admin under **Settings → Apps and sales channels →
Develop apps**.

```bash
export SHOPIFY_STORE=your-store.myshopify.com
export SHOPIFY_ADMIN_TOKEN=shpat_xxx

# 1. Create the product (DRAFT unless you pass --publish)
node scripts/seed-product.mjs

# 2. Duplicate the live theme and upload theme/ into the copy
node scripts/push-theme.mjs --name "GlowPulse Pro"
```

`push-theme.mjs` prints a `?preview_theme_id=` URL when it finishes. The new
theme is **unpublished** — publishing is the one step the scripts deliberately
leave to you, because it changes what customers see.

After the upload, open the theme editor and pick the product in the **GP Hero**
and **GP Offer** sections. Both sections fall back to sensible placeholder
states until you do.

Useful flags:

| Command | Effect |
| --- | --- |
| `node scripts/push-theme.mjs --dry-run` | Lists the files that would be sent; no network calls |
| `node scripts/push-theme.mjs --theme-id gid://shopify/OnlineStoreTheme/123` | Uploads into an existing theme instead of duplicating |
| `node scripts/seed-product.mjs --publish` | Creates the product as ACTIVE instead of DRAFT |

## Local preview

```bash
node scripts/build-preview.mjs
# then open preview/index.html
```

The preview mirrors the Liquid markup in plain JS and loads the same stylesheet
and script the theme ships, so it is an accurate check of layout, copy and
responsive behaviour without a store.

## Design system

Tokens live at the top of `theme/assets/glowpulse.css` and are documented in
`design-system/glowpulse-pro/MASTER.md`: a stone-and-gold palette on
`#fafaf9`, Rubik for headings against Nunito Sans for body, and a spacious
8→96px spacing scale.

Accessibility rules the sections hold to: body text at 4.5:1 or better,
44×44px minimum touch targets, visible focus rings, keyboard-operable FAQ,
`prefers-reduced-motion` respected, and no emoji used as icons — the icon set
in `snippets/gp-icon.liquid` is inline SVG.
