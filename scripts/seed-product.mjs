#!/usr/bin/env node
/**
 * Create the GlowPulse Pro product the storefront sells.
 *
 * Run this before push-theme.mjs if the store is empty — the hero and offer
 * sections both take a product reference, and you pick it in the theme editor
 * once the product exists.
 *
 * Usage:
 *   SHOPIFY_STORE=r1d1xd.myshopify.com \
 *   SHOPIFY_ADMIN_TOKEN=shpat_xxx \
 *   node scripts/seed-product.mjs [--publish]
 *
 * The token needs the write_products scope. Without --publish the product is
 * created as DRAFT.
 */

const API_VERSION = '2025-07';

const STORE = process.env.SHOPIFY_STORE;
const TOKEN = process.env.SHOPIFY_ADMIN_TOKEN;
const STATUS = process.argv.includes('--publish') ? 'ACTIVE' : 'DRAFT';

if (!STORE || !TOKEN) {
  console.error('Set SHOPIFY_STORE and SHOPIFY_ADMIN_TOKEN.');
  process.exit(1);
}

const PRODUCT = {
  title: 'GlowPulse Pro LED Light Therapy Mask',
  vendor: 'GlowPulse',
  productType: 'Skincare device',
  tags: ['led', 'red-light-therapy', 'skincare', 'anti-aging'],
  status: STATUS,
  descriptionHtml: `
<p>A flexible silicone mask carrying 132 medical-grade LEDs — 633nm red for
collagen, 830nm near-infrared for deeper repair and inflammation. Ten minutes a
night, hands free, cordless.</p>
<h3>What's in the box</h3>
<ul>
  <li>GlowPulse Pro mask with adjustable head strap</li>
  <li>Magnetic controller with session counter</li>
  <li>USB-C cable and travel case</li>
  <li>Quick-start card and 12-week routine guide</li>
</ul>
<h3>Specifications</h3>
<ul>
  <li>Wavelengths: 633nm red, 830nm near-infrared</li>
  <li>Irradiance: 40mW/cm² at surface</li>
  <li>Session: 10 minutes, automatic shut-off</li>
  <li>Battery: ~21 sessions per charge, 90-minute recharge</li>
  <li>Weight: 198g</li>
</ul>
<p><strong>60-night trial.</strong> Use it every evening for two months. If your
skin hasn't changed, send it back for a full refund — we pay return postage.</p>
`.trim(),
  price: '249.00',
  compareAtPrice: '329.00',
};

async function gql(query, variables = {}) {
  const res = await fetch(`https://${STORE}/admin/api/${API_VERSION}/graphql.json`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Shopify-Access-Token': TOKEN,
    },
    body: JSON.stringify({ query, variables }),
  });

  if (!res.ok) {
    throw new Error(`HTTP ${res.status} ${res.statusText}: ${await res.text()}`);
  }

  const json = await res.json();
  if (json.errors?.length) {
    throw new Error(`GraphQL: ${json.errors.map((e) => e.message).join('; ')}`);
  }
  return json.data;
}

function assertNoUserErrors(payload, label) {
  const errors = payload?.userErrors ?? [];
  if (errors.length) {
    const detail = errors
      .map((e) => `${(e.field || []).join('.') || '(root)'}: ${e.message}`)
      .join('\n  ');
    throw new Error(`${label} failed:\n  ${detail}`);
  }
}

async function main() {
  console.log(`Creating "${PRODUCT.title}" as ${STATUS}...`);

  const created = await gql(
    `
    mutation CreateProduct($input: ProductInput!) {
      productCreate(input: $input) {
        product {
          id
          title
          handle
          variants(first: 1) { nodes { id } }
        }
        userErrors { field message }
      }
    }
  `,
    {
      input: {
        title: PRODUCT.title,
        descriptionHtml: PRODUCT.descriptionHtml,
        vendor: PRODUCT.vendor,
        productType: PRODUCT.productType,
        tags: PRODUCT.tags,
        status: PRODUCT.status,
      },
    }
  );
  assertNoUserErrors(created.productCreate, 'productCreate');

  const product = created.productCreate.product;
  const variantId = product.variants.nodes[0]?.id;
  if (!variantId) throw new Error('Product created but it has no default variant to price.');

  console.log(`Created ${product.id} (handle: ${product.handle}). Setting price...`);

  const priced = await gql(
    `
    mutation PriceVariant($productId: ID!, $variants: [ProductVariantsBulkInput!]!) {
      productVariantsBulkUpdate(productId: $productId, variants: $variants) {
        productVariants { id price compareAtPrice }
        userErrors { field message }
      }
    }
  `,
    {
      productId: product.id,
      variants: [
        {
          id: variantId,
          price: PRODUCT.price,
          compareAtPrice: PRODUCT.compareAtPrice,
        },
      ],
    }
  );
  assertNoUserErrors(priced.productVariantsBulkUpdate, 'productVariantsBulkUpdate');

  const variant = priced.productVariantsBulkUpdate.productVariants[0];
  console.log(`\nDone — ${PRODUCT.title}`);
  console.log(`  Price:  ${variant.price} (was ${variant.compareAtPrice})`);
  console.log(`  Admin:  https://${STORE}/admin/products/${product.id.split('/').pop()}`);
  console.log('\nNext: run scripts/push-theme.mjs, then pick this product in the');
  console.log('GP Hero and GP Offer sections in the theme editor.');
}

main().catch((err) => {
  console.error(`\n${err.message}`);
  process.exit(1);
});
