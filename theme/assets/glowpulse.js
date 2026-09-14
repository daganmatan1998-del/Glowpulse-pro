/* GlowPulse Pro — storefront behaviour. No dependencies, no layout thrash. */
(function () {
  'use strict';

  var reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* Scroll reveal: staggered within each section, opacity/transform only. */
  function initReveal() {
    var items = document.querySelectorAll('.gp-reveal:not([data-gp-observed])');
    if (!items.length) return;

    if (reduced || !('IntersectionObserver' in window)) {
      items.forEach(function (el) {
        el.setAttribute('data-gp-observed', '');
        el.classList.add('is-in');
      });
      return;
    }

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var el = entry.target;
        var delay = parseInt(el.getAttribute('data-gp-delay') || '0', 10);
        window.setTimeout(function () { el.classList.add('is-in'); }, delay);
        observer.unobserve(el);
      });
    }, { rootMargin: '0px 0px -10% 0px', threshold: 0.1 });

    items.forEach(function (el) {
      el.setAttribute('data-gp-observed', '');
      observer.observe(el);
    });
  }

  /* FAQ accordion: one open at a time, fully keyboard operable. */
  function initFaq() {
    document.querySelectorAll('.gp-faq:not([data-gp-bound])').forEach(function (faq) {
      faq.setAttribute('data-gp-bound', '');
      faq.addEventListener('click', function (event) {
        var button = event.target.closest('.gp-faq__q');
        if (!button || !faq.contains(button)) return;

        var open = button.getAttribute('aria-expanded') === 'true';

        faq.querySelectorAll('.gp-faq__q').forEach(function (other) {
          other.setAttribute('aria-expanded', 'false');
          document.getElementById(other.getAttribute('aria-controls')).hidden = true;
        });

        if (!open) {
          button.setAttribute('aria-expanded', 'true');
          document.getElementById(button.getAttribute('aria-controls')).hidden = false;
        }
      });
    });
  }

  function init() {
    initReveal();
    initFaq();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  /* Re-bind when the theme editor swaps a section in. */
  document.addEventListener('shopify:section:load', init);
})();
