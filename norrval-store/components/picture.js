import { IMAGES } from '../data/images.js';
import { esc } from '../utils/html.js';

let manifest = {};
export const setManifest = (m) => (manifest = m || {});

/** Follow the fallback chain to the first slot that has generated files. */
export function resolve(slot, seen = new Set()) {
  if (!slot || seen.has(slot)) return null;
  seen.add(slot);
  if (manifest[slot]) return slot;
  return resolve(IMAGES[slot]?.fallback, seen);
}

export const isReal = (slot) => Boolean(manifest[slot]);

// A manifest entry is either local ({w, h, widths}: files in assets/img) or
// remote ({w, h, webp}: one full-resolution WebP on a CDN).
const isRemote = (slot) => Boolean(manifest[slot]?.webp);

/** srcset for one format, or '' when that format doesn't exist for the slot. */
export const srcset = (slot, ext) => {
  const m = manifest[slot];
  if (isRemote(slot)) return ext === 'webp' ? `${m.webp} ${m.w}w` : '';
  return m.widths.map((w) => `/assets/img/${slot}-${w}.${ext} ${w}w`).join(', ');
};

/** The best preloadable format for a slot: local AVIF, or the remote WebP. */
export const preloadFormat = (slot) => (isRemote(slot) ? 'webp' : 'avif');

export const imageUrl = (slot, width = 1080) => {
  const s = resolve(slot);
  if (!s) return null;
  if (isRemote(s)) return manifest[s].webp;
  const w = manifest[s].widths.reduce((a, b) => (Math.abs(b - width) < Math.abs(a - width) ? b : a));
  return `/assets/img/${s}-${w}.jpg`;
};

/**
 * <picture> with AVIF → WebP → JPEG, explicit dimensions and an optional
 * separate art-directed mobile image (not a crop of the desktop one).
 */
export function picture({
  slot,
  mobileSlot = null,
  sizes = '100vw',
  priority = false,
  className = '',
  alt = null,
}) {
  const s = resolve(slot);
  const meta = IMAGES[slot] || {};
  const altText = alt ?? meta.alt ?? '';
  if (!s) {
    // Neutral placeholder keeps the layout's ratio until the image exists.
    const [rw, rh] = meta.ratio || [4, 5];
    return `<div class="img-ph ${className}" style="aspect-ratio:${rw}/${rh}" role="img" aria-label="${esc(altText)}"></div>`;
  }
  const m = manifest[s];
  // A fallback image shows something else, so describe what is actually shown.
  const realAlt = alt ?? IMAGES[s]?.alt ?? altText;
  const ms = mobileSlot ? resolve(mobileSlot) : null;
  const mobileSources =
    ms && ms !== s
      ? ['avif', 'webp', 'jpg']
          .filter((ext) => srcset(ms, ext))
          .map(
            (ext) =>
              `<source media="(max-width: 767px)" type="image/${ext === 'jpg' ? 'jpeg' : ext}" srcset="${srcset(ms, ext)}" sizes="100vw" width="${manifest[ms].w}" height="${manifest[ms].h}">`,
          )
          .join('')
      : '';
  const pos = IMAGES[s]?.position || '50% 50%';
  const load = priority ? 'fetchpriority="high" loading="eager"' : 'loading="lazy"';
  const imgAttrs = `width="${m.w}" height="${m.h}" alt="${esc(realAlt)}" style="object-position:${pos}" ${load} decoding="async"`;
  if (isRemote(s)) {
    return `<picture class="${className}">${mobileSources}<img src="${m.webp}" ${imgAttrs}></picture>`;
  }
  const mid = m.widths[Math.min(2, m.widths.length - 1)];
  return `<picture class="${className}">${mobileSources}<source type="image/avif" srcset="${srcset(s, 'avif')}" sizes="${sizes}"><source type="image/webp" srcset="${srcset(s, 'webp')}" sizes="${sizes}"><img src="/assets/img/${s}-${mid}.jpg" srcset="${srcset(s, 'jpg')}" sizes="${sizes}" ${imgAttrs}></picture>`;
}
