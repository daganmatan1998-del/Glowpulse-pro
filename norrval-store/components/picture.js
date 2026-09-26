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

export const srcset = (slot, ext) =>
  manifest[slot].widths.map((w) => `/assets/img/${slot}-${w}.${ext} ${w}w`).join(', ');

export const imageUrl = (slot, width = 1080) => {
  const s = resolve(slot);
  if (!s) return null;
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
          .map(
            (ext) =>
              `<source media="(max-width: 767px)" type="image/${ext === 'jpg' ? 'jpeg' : ext}" srcset="${srcset(ms, ext)}" sizes="100vw" width="${manifest[ms].w}" height="${manifest[ms].h}">`,
          )
          .join('')
      : '';
  const pos = IMAGES[s]?.position || '50% 50%';
  const mid = m.widths[Math.min(2, m.widths.length - 1)];
  return `<picture class="${className}">${mobileSources}<source type="image/avif" srcset="${srcset(s, 'avif')}" sizes="${sizes}"><source type="image/webp" srcset="${srcset(s, 'webp')}" sizes="${sizes}"><img src="/assets/img/${s}-${mid}.jpg" srcset="${srcset(s, 'jpg')}" sizes="${sizes}" width="${m.w}" height="${m.h}" alt="${esc(realAlt)}" style="object-position:${pos}" ${priority ? 'fetchpriority="high" loading="eager"' : 'loading="lazy"'} decoding="async"></picture>`;
}
