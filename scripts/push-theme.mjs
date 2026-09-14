#!/usr/bin/env node
/**
 * Push the GlowPulse Pro theme straight into a Shopify store — no ZIP, no
 * manual "Add theme" step in the admin.
 *
 * Flow:
 *   1. themeDuplicate the current live theme  -> a new UNPUBLISHED theme
 *   2. themeFilesUpsert every file under theme/ in batches of 50
 *   3. print the preview URL
 *
 * Publishing is deliberately NOT automated. Preview first, then hit Publish
 * in the admin — that is the one step that changes what customers see.
 *
 * Usage:
 *   SHOPIFY_STORE=r1d1xd.myshopify.com \
 *   SHOPIFY_ADMIN_TOKEN=shpat_xxx \
 *   node scripts/push-theme.mjs [--name "GlowPulse Pro"] [--theme-id gid://...] [--dry-run]
 *
 * The token needs the write_themes scope.
 */

import { readdir, readFile } from 'node:fs/promises';
import { join, relative, sep } from 'node:path';

const API_VERSION = '2025-07';
const BATCH_SIZE = 50; // themeFilesUpsert hard limit
const THEME_DIR = new URL('../theme/', import.meta.url).pathname;

const args = process.argv.slice(2);
const flag = (name, fallback = null) => {
  const i = args.indexOf(`--${name}`);
  return i === -1 ? fallback : args[i + 1];
};
const has = (name) => args.includes(`--${name}`);

const STORE = process.env.SHOPIFY_STORE;
const TOKEN = process.env.SHOPIFY_ADMIN_TOKEN;
const THEME_NAME = flag('name', 'GlowPulse Pro');
const DRY_RUN = has('dry-run');

if (!DRY_RUN && (!STORE || !TOKEN)) {
  console.error('Set SHOPIFY_STORE and SHOPIFY_ADMIN_TOKEN (or pass --dry-run).');
  process.exit(1);
}

/* ------------------------------------------------------------------ GraphQL */

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

/** Throws on the userErrors array every Shopify mutation returns. */
function assertNoUserErrors(payload, label) {
  const errors = payload?.userErrors ?? [];
  if (errors.length) {
    const detail = errors
      .map((e) => `${(e.field || []).join('.') || '(root)'}: ${e.message}`)
      .join('\n  ');
    throw new Error(`${label} failed:\n  ${detail}`);
  }
}

/* --------------------------------------------------------------- file walk */

/** Theme file extensions Shopify accepts as plain text. */
const TEXT_EXT = new Set(['.liquid', '.json', '.css', '.js', '.svg', '.md', '.txt']);

async function collectThemeFiles(dir, root = dir) {
  const out = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      out.push(...(await collectThemeFiles(full, root)));
      continue;
    }
    const ext = entry.name.slice(entry.name.lastIndexOf('.'));
    if (!TEXT_EXT.has(ext)) {
      console.warn(`  skipping (binary/unknown type): ${relative(root, full)}`);
      continue;
    }
    out.push({
      // Shopify always uses forward slashes, regardless of host OS
      filename: relative(root, full).split(sep).join('/'),
      body: { type: 'TEXT', value: await readFile(full, 'utf8') },
    });
  }
  return out.sort((a, b) => a.filename.localeCompare(b.filename));
}

/* ------------------------------------------------------------------- steps */

async function findLiveTheme() {
  const data = await gql(`
    query LiveTheme {
      themes(first: 20, roles: [MAIN]) {
        nodes { id name role }
      }
    }
  `);
  const live = data.themes.nodes[0];
  if (!live) throw new Error('No live (MAIN) theme found on this store.');
  return live;
}

async function duplicateTheme(sourceId, name) {
  const data = await gql(
    `
    mutation DuplicateTheme($id: ID!, $name: String) {
      themeDuplicate(id: $id, name: $name) {
        theme { id name role }
        userErrors { field message }
      }
    }
  `,
    { id: sourceId, name }
  );
  assertNoUserErrors(data.themeDuplicate, 'themeDuplicate');

  const theme = data.themeDuplicate.theme;
  if (!theme) {
    throw new Error(
      'themeDuplicate returned no theme. Duplication can be asynchronous — ' +
        'check Online Store > Themes, then re-run with --theme-id <gid>.'
    );
  }
  return theme;
}

async function upsertFiles(themeId, files) {
  const mutation = `
    mutation UpsertThemeFiles($themeId: ID!, $files: [OnlineStoreThemeFilesUpsertFileInput!]!) {
      themeFilesUpsert(themeId: $themeId, files: $files) {
        upsertedThemeFiles { filename }
        userErrors { field message }
      }
    }
  `;

  let written = 0;
  for (let i = 0; i < files.length; i += BATCH_SIZE) {
    const batch = files.slice(i, i + BATCH_SIZE);
    const data = await gql(mutation, { themeId, files: batch });
    assertNoUserErrors(data.themeFilesUpsert, 'themeFilesUpsert');

    written += data.themeFilesUpsert.upsertedThemeFiles.length;
    for (const f of data.themeFilesUpsert.upsertedThemeFiles) {
      console.log(`  + ${f.filename}`);
    }
  }
  return written;
}

/* -------------------------------------------------------------------- main */

async function main() {
  console.log(`Reading theme files from ${THEME_DIR}`);
  const files = await collectThemeFiles(THEME_DIR);
  console.log(`Found ${files.length} file(s).\n`);

  if (DRY_RUN) {
    for (const f of files) {
      console.log(`  ${f.filename.padEnd(40)} ${f.body.value.length} bytes`);
    }
    console.log('\nDry run — nothing was sent to Shopify.');
    return;
  }

  let themeId = flag('theme-id');

  if (themeId) {
    console.log(`Using existing theme ${themeId}`);
  } else {
    const live = await findLiveTheme();
    console.log(`Live theme: ${live.name} (${live.id})`);
    console.log(`Duplicating it as "${THEME_NAME}"...`);
    const theme = await duplicateTheme(live.id, THEME_NAME);
    themeId = theme.id;
    console.log(`Created ${theme.name} (${theme.id}) with role ${theme.role}.\n`);
  }

  console.log('Uploading files...');
  const written = await upsertFiles(themeId, files);

  const numericId = themeId.split('/').pop();
  console.log(`\nDone — ${written} file(s) written.`);
  console.log(`Preview:  https://${STORE}/?preview_theme_id=${numericId}`);
  console.log(`Admin:    https://${STORE}/admin/themes/${numericId}/editor`);
  console.log('\nThe theme is UNPUBLISHED. Preview it, then publish from the admin.');
}

main().catch((err) => {
  console.error(`\n${err.message}`);
  process.exit(1);
});
