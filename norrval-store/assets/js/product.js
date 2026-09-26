/* Product page: gallery, variant, quantity, add to cart, buy now, sticky bar. */
(() => {
  'use strict';
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];

  // ---------- gallery ----------
  const g = $('[data-gallery]');
  if (g) {
    const slides = $$('[data-slide]', g);
    const thumbs = $$('[data-thumb]', g);
    let i = 0;
    const go = (n) => {
      i = (n + slides.length) % slides.length;
      slides.forEach((s, k) => {
        s.classList.toggle('is-active', k === i);
        s.setAttribute('aria-hidden', String(k !== i));
      });
      thumbs.forEach((t, k) => t.setAttribute('aria-current', String(k === i)));
      thumbs[i]?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    };
    thumbs.forEach((t) => t.addEventListener('click', () => go(+t.dataset.thumb)));
    $('[data-prev]', g)?.addEventListener('click', () => go(i - 1));
    $('[data-next]', g)?.addEventListener('click', () => go(i + 1));
    // Swipe on touch.
    let x0 = null;
    const main = $('.gallery__main', g);
    main.addEventListener('touchstart', (e) => (x0 = e.touches[0].clientX), { passive: true });
    main.addEventListener('touchend', (e) => {
      if (x0 === null) return;
      const dx = e.changedTouches[0].clientX - x0;
      if (Math.abs(dx) > 40) go(i + (dx < 0 ? 1 : -1));
      x0 = null;
    });
    main.addEventListener('keydown', (e) => {
      if (e.key === 'ArrowRight') go(i + 1);
      if (e.key === 'ArrowLeft') go(i - 1);
    });
  }

  // ---------- buy form ----------
  const form = $('[data-buy]');
  if (!form) return;
  const qty = form.qty;
  let variant = $('[data-variant][aria-checked="true"]', form)?.dataset.variant;
  $$('[data-variant]', form).forEach((b) =>
    b.addEventListener('click', () => {
      $$('[data-variant]', form).forEach((x) => x.setAttribute('aria-checked', String(x === b)));
      variant = b.dataset.variant;
      $('[data-variant-name]').textContent = b.dataset.variantLabel;
    }),
  );
  const clampQty = () => (qty.value = Math.max(1, Math.min(10, parseInt(qty.value, 10) || 1)));
  $$('[data-qty]', form).forEach((b) =>
    b.addEventListener('click', () => {
      qty.value = (parseInt(qty.value, 10) || 1) + +b.dataset.qty;
      clampQty();
    }),
  );
  qty.addEventListener('change', clampQty);
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    window.NorrvalAdd(variant, +clampQty());
  });
  $('[data-buy-now]').addEventListener('click', () => {
    window.NorrvalCart.add(variant, +clampQty());
    location.href = '/checkout/';
  });

  // ---------- sticky mobile add-to-cart ----------
  const bar = $('[data-sticky-atc]');
  const anchor = $('[data-atc-anchor]');
  if (bar && anchor && 'IntersectionObserver' in window) {
    const btn = $('[data-sticky-add]', bar);
    new IntersectionObserver(([en]) => {
      const show = !en.isIntersecting && en.boundingClientRect.top < 0;
      bar.classList.toggle('is-visible', show);
      bar.setAttribute('aria-hidden', String(!show));
      btn.tabIndex = show ? 0 : -1;
    }).observe(anchor);
    btn.addEventListener('click', () => window.NorrvalAdd(variant, 1));
  }
})();
