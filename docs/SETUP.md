# GlowPulse — setup guide

Everything below takes about 45 minutes end to end. Follow it in order.

---

## 1. Install the theme

### Option A — upload a zip (no tools needed)

```bash
./scripts/package.sh
```

That produces `glowpulse-theme.zip` in the repo root, containing only the folders Shopify accepts.

Then in Shopify admin: **Online Store → Themes → Add theme → Upload zip file**, pick the file, and press **Publish** once you have previewed it.

### Option B — Shopify CLI (better while you are still editing)

```bash
npm install -g @shopify/cli @shopify/theme
shopify theme dev --store fdbp1y-1n.myshopify.com
```

This gives you a live preview URL that reloads as you edit files locally. When you are happy:

```bash
shopify theme push --store fdbp1y-1n.myshopify.com
```

---

## 2. Create the navigation menus

**Online Store → Navigation.**

The theme's header and footer expect two menus:

**Main menu** (handle `main-menu`)
- Shop → `/collections/all`
- Best sellers → your best-sellers collection
- Reviews → `/pages/reviews`
- About → `/pages/about`
- Contact → `/pages/contact`

**Footer menu** (handle `footer`)
- FAQ → `/pages/faq`
- Shipping Policy → `/policies/shipping-policy`
- Refund Policy → `/policies/refund-policy`
- Privacy Policy → `/policies/privacy-policy`
- Terms of Service → `/policies/terms-of-service`
- Contact → `/pages/contact`

---

## 3. Create the pages

**Online Store → Pages → Add page.** For each one, set the **Theme template** dropdown on the right to the template listed here. Leave the page content empty where it says so — the layout comes from the template, not from the page body.

| Page title | Handle | Template | Content |
| --- | --- | --- | --- |
| Reviews | `reviews` | `page.reviews` | Leave empty |
| FAQ | `faq` | `page.faq` | Leave empty |
| About | `about` | `page.about` | Leave empty |
| Contact | `contact` | `page.contact` | Leave empty |
| Shipping Policy | `shipping-policy` | `page.policy` | Paste from `docs/policies/shipping-policy.md` |
| Refund Policy | `refund-policy` | `page.policy` | Paste from `docs/policies/refund-policy.md` |
| Privacy Policy | `privacy-policy` | `page.policy` | Paste from `docs/policies/privacy-policy.md` |
| Terms of Service | `terms-of-service` | `page.policy` | Paste from `docs/policies/terms-of-service.md` |

**Also fill in Settings → Policies.** Shopify links to those versions from the checkout footer, and some payment providers will not approve an account without them. Paste the same text there. The theme styles those built-in pages too.

Every placeholder in the policy files looks like `{{ support@yourstore.com }}`. Search for `{{` before publishing and make sure none are left.

---

## 4. Import your first product with DSers

1. Install **DSers** from the Shopify App Store and connect it to your store.
2. Install the DSers Chrome extension.
3. Find your product on AliExpress, click the DSers button on the listing, and it lands in **DSers → Import List**.
4. In the Import List, before you push it to Shopify:
   - **Rewrite the title.** AliExpress titles are keyword soup. Something like "Ceramic Swirl Mug — 350ml" beats "2024 New Fashion Creative Nordic Ins Ceramic Coffee Mug Cup Gift".
   - **Delete the junk variants.** Suppliers list 40 colours you will never sell. Keep 3 to 6.
   - **Rename the options.** Change `Ships From` to something meaningful or remove it entirely — customers should not see your supplier's logistics fields.
   - **Set your price.** DSers can apply a pricing rule automatically (Settings → Pricing Rule). A 2.5x to 3x multiplier is a common starting point.
   - **Clean the description.** Delete the supplier's HTML block and write two or three real paragraphs. Keep the images that show the product, delete the ones with Chinese text overlays.
5. Push to Shopify.

### After the import

Open the product in Shopify admin and check:

- **Media** — the first image is the one people see in the grid. Reorder so the best one is first.
- **Variants → images** — if each colour has its own photo, the theme turns the colour option into image swatches automatically. Assign a photo to each colour variant.
- **Inventory** — DSers usually sets "Continue selling when out of stock". Leave it on unless you want the theme to show "Only N left".
- **Product organisation → Collections** — add it to a collection, otherwise the homepage grid and the "You may also like" section have nothing to show.

---

## 5. Set up the homepage

**Online Store → Customize.** The homepage ships with eleven sections in this order:

1. **Hero** — heading, subtitle, two buttons, image, floating badge
2. **Scrolling strip** — the animated trust ticker
3. **Benefits** — four icon columns
4. **Product grid** — pick your collection here, this is the one section that will look empty until you do
5. **Image with text** — your story
6. **How it works** — three steps
7. **Comparison table** — you versus the alternatives
8. **Customer reviews** — a slider
9. **Photo gallery** — add four lifestyle photos
10. **FAQ** — five questions, with search-engine structured data switched on
11. **Newsletter** — email capture

Everything is drag-and-drop reorderable and every section can be deleted or duplicated.

---

## 6. Reviews without paying for an app

The theme has its own review system so you are not forced into a paid app on day one.

- **Homepage and product page:** the "Customer reviews" section — each review is a block with a rating, headline, body, name, location, date, a "verified purchase" tag, and optional photo.
- **Reviews page:** the "Reviews page" section adds an average score and a star distribution, both calculated from the reviews you enter, so the summary always matches what a visitor can read.

**If you later install a review app** (Judge.me, Loox, Okendo, Shopify's own): the theme automatically reads the standard `reviews.rating` and `reviews.rating_count` metafields those apps write, and real ratings replace the fallback values everywhere — product cards, product page, structured data. You do not need to change anything.

A note worth taking seriously: the fallback rating fields exist so a brand new store is not showing an empty space, but publishing review counts you have not earned is a bad trade. Set the fallback count to 0 until you have real ones.

---

## 7. Colours, fonts and animation

**Customize → Theme settings.**

- **Colors** — the whole theme is driven by these. The accent and secondary accent form the gradient used on buttons, the newsletter block and headings.
- **Typography** — heading font, body font, weight, size and letter case.
- **Layout & shape** — page width, section spacing, corner radius, shadow depth. Set corner radius to 0 for a sharp, editorial look.
- **Animations** — reveal style (fade / rise / zoom / blur), speed, hover lift, image zoom, page fade. All of it turns off automatically for visitors who have "reduce motion" enabled on their device.
- **Cart** — drawer or cart page, free shipping progress bar and its threshold, order notes.

---

## 8. Before you launch

- [ ] Every `{{ placeholder }}` in the policy pages is replaced
- [ ] Settings → Policies filled in
- [ ] A real support email address that you actually read
- [ ] Shipping rates configured under **Settings → Shipping and delivery** (free shipping needs a rate set to $0, it is not automatic)
- [ ] Payment provider connected and test order placed
- [ ] Delivery estimates on the product page match your supplier's real times
- [ ] Favicon and logo uploaded under Theme settings → Brand
- [ ] Social links filled in, or the icons stay hidden
- [ ] Test a full checkout on your phone, not just your laptop
- [ ] Remove the password page (**Online Store → Preferences**) when you are ready

---

## Files worth knowing about

| Path | What it does |
| --- | --- |
| `layout/theme.liquid` | Page shell, design tokens generated from theme settings |
| `assets/base.css` | The whole design system |
| `assets/animations.css` | Every animation, plus the reduced-motion overrides |
| `assets/theme.js` | Cart, variant picker, gallery, accordions, countdowns |
| `sections/main-product.liquid` | The product page, built from reorderable blocks |
| `sections/reviews-wall.liquid` | The reviews page with its calculated summary |
| `snippets/product-card.liquid` | The card used in every product grid |
| `snippets/swatch-style.liquid` | Turns variant photos into colour swatches |
| `docs/policies/` | The policy copy to paste into Shopify |
