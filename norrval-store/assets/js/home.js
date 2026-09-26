/* Homepage motion: sticky cinematic showcase + pinned horizontal lifestyle. */
(() => {
  'use strict';
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const clamp = (v) => Math.max(0, Math.min(1, v));

  // ---------- cinematic showcase ----------
  const cine = document.querySelector('[data-cine]');
  const steps = cine ? [...cine.querySelectorAll('[data-step]')] : [];
  const scenes = cine ? [...cine.querySelectorAll('[data-scene]')] : [];
  let curStep = 0;
  let curScene = 0;
  function cineFrame() {
    // Always computed (clamped), so fast scrolls past the section still land
    // on the first or last step.
    const r = cine.getBoundingClientRect();
    const p = clamp(-r.top / (r.height - innerHeight));
    cine.style.setProperty('--p', p.toFixed(4));
    const s = Math.min(steps.length - 1, Math.floor(p * steps.length * 0.999));
    if (s !== curStep) {
      steps[curStep].classList.remove('is-active');
      steps[s].classList.add('is-active');
      curStep = s;
    }
    if (scenes.length > 1) {
      const sc = Math.min(scenes.length - 1, Math.floor(p * scenes.length * 0.999));
      if (sc !== curScene) {
        scenes[curScene].classList.remove('is-active');
        scenes[sc].classList.add('is-active');
        curScene = sc;
      }
    }
  }
  if (cine && reduce) steps.forEach((s) => s.classList.add('is-active'));

  // ---------- horizontal lifestyle ----------
  const hs = document.querySelector('[data-hscroll]');
  const track = hs && hs.querySelector('[data-track]');
  const wide = matchMedia('(min-width: 768px)');
  let hsDist = 0;
  function hsLayout() {
    if (!hs) return;
    const pinned = wide.matches && !reduce;
    hs.classList.toggle('is-static', !pinned);
    if (!pinned) {
      hs.style.height = '';
      track.style.transform = '';
      return;
    }
    hsDist = Math.max(0, track.scrollWidth - innerWidth);
    hs.style.height = `${hsDist + innerHeight}px`;
  }
  function hsFrame() {
    if (!hs || hs.classList.contains('is-static')) return;
    const r = hs.getBoundingClientRect();
    const p = clamp(-r.top / (r.height - innerHeight || 1));
    track.style.transform = `translate3d(${(-p * hsDist).toFixed(1)}px,0,0)`;
  }
  hsLayout();
  addEventListener('resize', hsLayout);
  addEventListener('load', hsLayout);
  wide.addEventListener?.('change', hsLayout);

  if (reduce) return;
  let ticking = false;
  const frame = () => {
    if (cine) cineFrame();
    hsFrame();
    ticking = false;
  };
  addEventListener('scroll', () => {
    if (!ticking) {
      ticking = true;
      requestAnimationFrame(frame);
    }
  }, { passive: true });
  frame();
})();
