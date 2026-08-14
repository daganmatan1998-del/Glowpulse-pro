/* ==========================================================================
   GlowPulse — theme.js
   No dependencies, no build step. Every widget is initialised from a data
   attribute so sections can be added and removed in the theme editor.
   ========================================================================== */
(function () {
  'use strict';

  var T = window.theme || {};
  var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* ---------------------------------------------------------- helpers -- */

  function $(sel, root) { return (root || document).querySelector(sel); }
  function $$(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

  function on(el, evt, handler, opts) {
    if (el) el.addEventListener(evt, handler, opts);
  }

  function debounce(fn, wait) {
    var t;
    return function () {
      var args = arguments, ctx = this;
      clearTimeout(t);
      t = setTimeout(function () { fn.apply(ctx, args); }, wait);
    };
  }

  function fetchJSON(url, options) {
    return fetch(url, options).then(function (res) {
      return res.json().then(function (data) {
        if (!res.ok) throw data;
        return data;
      });
    });
  }

  var lockCount = 0;
  function lockScroll(lock) {
    lockCount = Math.max(0, lockCount + (lock ? 1 : -1));
    document.body.classList.toggle('is-locked', lockCount > 0);
  }

  function trapFocus(container, evt) {
    if (evt.key !== 'Tab') return;
    var focusables = $$('a[href], button:not([disabled]), input:not([disabled]), select, textarea, [tabindex]:not([tabindex="-1"])', container)
      .filter(function (el) { return el.offsetParent !== null; });
    if (!focusables.length) return;
    var first = focusables[0];
    var last = focusables[focusables.length - 1];
    if (evt.shiftKey && document.activeElement === first) {
      evt.preventDefault();
      last.focus();
    } else if (!evt.shiftKey && document.activeElement === last) {
      evt.preventDefault();
      first.focus();
    }
  }

  /* ------------------------------------------------------------ toasts -- */

  var ICONS = {
    check: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m4 10.5 4 4 8-9"/></svg>',
    alert: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M10 6v5"/><circle cx="10" cy="14.5" r=".8" fill="currentColor" stroke="none"/><circle cx="10" cy="10" r="8"/></svg>'
  };

  function toast(message, type) {
    var stack = $('#ToastStack');
    if (!stack) return;
    var el = document.createElement('div');
    el.className = 'toast' + (type ? ' toast--' + type : '');
    el.innerHTML = (type === 'error' ? ICONS.alert : ICONS.check) + '<span></span>';
    el.querySelector('span').textContent = message;
    stack.appendChild(el);
    requestAnimationFrame(function () { el.classList.add('is-visible'); });
    setTimeout(function () {
      el.classList.remove('is-visible');
      setTimeout(function () { el.remove(); }, 320);
    }, 3200);
  }

  /* ---------------------------------------------------- scroll reveal -- */

  var revealObserver = null;

  function initReveals(root) {
    if (!document.documentElement.classList.contains('anim-on') || prefersReducedMotion) {
      $$('.reveal, .reveal-group', root).forEach(function (el) { el.classList.add('is-visible'); });
      return;
    }

    if (!('IntersectionObserver' in window)) {
      $$('.reveal, .reveal-group', root).forEach(function (el) { el.classList.add('is-visible'); });
      return;
    }

    if (!revealObserver) {
      revealObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          entry.target.classList.add('is-visible');
          revealObserver.unobserve(entry.target);
        });
      }, { rootMargin: '0px 0px -8% 0px', threshold: 0.05 });
    }

    $$('.reveal, .reveal-group', root).forEach(function (el) {
      if (el.classList.contains('is-visible')) return;
      // Anything already in view on load should not wait for a scroll.
      if (el.getBoundingClientRect().top < window.innerHeight * 0.92) {
        el.classList.add('is-visible');
      } else {
        revealObserver.observe(el);
      }
    });
  }

  /* ------------------------------------------------------ sticky header -- */

  function initStickyHeader() {
    var header = $('[data-header]');
    if (!header) return;

    function measure() {
      document.documentElement.style.setProperty('--header-height', header.offsetHeight + 'px');
    }
    measure();
    window.addEventListener('resize', debounce(measure, 150));

    var wrapper = header.closest('[data-header-wrapper]');
    if (!wrapper || !wrapper.classList.contains('header-wrapper--sticky')) return;

    var onScroll = function () {
      header.classList.toggle('is-stuck', window.scrollY > 8);
    };
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
  }

  /* ---------------------------------------------------------- overlays -- */

  var openPanel = null;

  function closePanel() {
    if (!openPanel) return;
    openPanel.el.classList.remove('is-open');
    if (openPanel.el.hasAttribute('aria-hidden')) openPanel.el.setAttribute('aria-hidden', 'true');
    var overlay = $('[data-overlay]');
    if (overlay) overlay.classList.remove('is-open');
    lockScroll(false);
    document.removeEventListener('keydown', openPanel.keyHandler);
    if (openPanel.trigger && document.body.contains(openPanel.trigger)) openPanel.trigger.focus();
    openPanel = null;
  }

  function showPanel(el, trigger) {
    if (!el) return;
    if (openPanel) closePanel();

    el.classList.add('is-open');
    el.setAttribute('aria-hidden', 'false');
    var overlay = $('[data-overlay]');
    if (overlay) overlay.classList.add('is-open');
    lockScroll(true);

    var keyHandler = function (evt) {
      if (evt.key === 'Escape') closePanel();
      trapFocus(el, evt);
    };
    document.addEventListener('keydown', keyHandler);
    openPanel = { el: el, trigger: trigger, keyHandler: keyHandler };

    setTimeout(function () {
      var focusTarget = el.querySelector('[data-autofocus]') || el.querySelector('button, a[href], input');
      if (focusTarget) focusTarget.focus();
    }, 120);
  }

  function initOverlays() {
    document.addEventListener('click', function (evt) {
      var opener = evt.target.closest('[data-panel-open]');
      if (opener) {
        var target = $(opener.getAttribute('data-panel-open'));
        if (target) {
          evt.preventDefault();
          if (openPanel && openPanel.el === target) closePanel();
          else showPanel(target, opener);
        }
        return;
      }

      if (evt.target.closest('[data-panel-close]') || evt.target.closest('[data-overlay]')) {
        evt.preventDefault();
        closePanel();
      }
    });
  }

  window.GlowPulseOpenCart = function () {
    var drawer = $('[data-cart-drawer]');
    if (drawer) showPanel(drawer);
  };

  /* ------------------------------------------------------- mobile menu -- */

  function initMobileMenu(root) {
    $$('[data-mobile-submenu-toggle]', root).forEach(function (btn) {
      if (btn.dataset.bound) return;
      btn.dataset.bound = '1';
      on(btn, 'click', function () {
        var panel = btn.nextElementSibling;
        var open = btn.getAttribute('aria-expanded') === 'true';
        btn.setAttribute('aria-expanded', String(!open));
        if (panel) panel.classList.toggle('is-open', !open);
      });
    });
  }

  /* ------------------------------------------------------- announcement -- */

  function initAnnouncement(root) {
    $$('[data-announcement]', root).forEach(function (el) {
      if (el.dataset.bound) return;
      el.dataset.bound = '1';

      var slides = $$('.announcement__slide', el);
      if (slides.length < 2) return;
      var speed = parseInt(el.getAttribute('data-speed'), 10) || 5000;
      var index = 0;

      setInterval(function () {
        slides[index].classList.remove('is-active');
        index = (index + 1) % slides.length;
        slides[index].classList.add('is-active');
      }, speed);
    });
  }

  /* ------------------------------------------------------------ marquee -- */

  function initMarquee(root) {
    $$('[data-marquee]', root).forEach(function (el) {
      if (el.dataset.bound) return;
      el.dataset.bound = '1';

      var track = $('.marquee__track', el);
      if (!track) return;
      // Duplicate the track so the loop has no visible seam.
      var clone = track.cloneNode(true);
      clone.setAttribute('aria-hidden', 'true');
      el.appendChild(clone);
    });
  }

  /* --------------------------------------------------------- accordions -- */

  function setPanelHeight(panel, open) {
    panel.style.maxHeight = open ? panel.scrollHeight + 'px' : '0px';
  }

  function initAccordions(root) {
    $$('[data-accordion-trigger]', root).forEach(function (trigger) {
      if (trigger.dataset.bound) return;
      trigger.dataset.bound = '1';

      var panel = document.getElementById(trigger.getAttribute('aria-controls'));
      if (!panel) return;

      if (trigger.getAttribute('aria-expanded') === 'true') {
        // Wait for fonts/images so the measured height is right.
        requestAnimationFrame(function () { setPanelHeight(panel, true); });
      }

      on(trigger, 'click', function () {
        var open = trigger.getAttribute('aria-expanded') === 'true';
        var group = trigger.closest('[data-accordion-exclusive]');

        if (group && !open) {
          $$('[data-accordion-trigger][aria-expanded="true"]', group).forEach(function (other) {
            var otherPanel = document.getElementById(other.getAttribute('aria-controls'));
            other.setAttribute('aria-expanded', 'false');
            if (otherPanel) setPanelHeight(otherPanel, false);
          });
        }

        trigger.setAttribute('aria-expanded', String(!open));
        setPanelHeight(panel, !open);
      });

      // Keep an open panel correctly sized when the viewport changes.
      window.addEventListener('resize', debounce(function () {
        if (trigger.getAttribute('aria-expanded') === 'true') setPanelHeight(panel, true);
      }, 200));
    });
  }

  /* ------------------------------------------------- description clamp -- */

  function initDescriptionClamp(root) {
    $$('[data-description-clamp]', root).forEach(function (wrapper) {
      if (wrapper.dataset.bound) return;
      wrapper.dataset.bound = '1';

      var content = $('.description-clamp', wrapper);
      var toggle = $('[data-description-toggle]', wrapper);
      if (!content || !toggle) return;

      // Nothing to collapse — hide the toggle entirely.
      if (content.scrollHeight <= content.clientHeight + 20) {
        content.classList.add('is-expanded');
        toggle.hidden = true;
        return;
      }

      on(toggle, 'click', function () {
        var expanded = content.classList.toggle('is-expanded');
        toggle.textContent = expanded ? toggle.getAttribute('data-less') : toggle.getAttribute('data-more');
      });
    });
  }

  /* ----------------------------------------------------------- quantity -- */

  function initQuantity(root) {
    $$('[data-quantity]', root).forEach(function (wrapper) {
      if (wrapper.dataset.bound) return;
      wrapper.dataset.bound = '1';

      var input = $('input', wrapper);
      if (!input) return;

      $$('[data-quantity-change]', wrapper).forEach(function (btn) {
        on(btn, 'click', function () {
          var step = parseInt(btn.getAttribute('data-quantity-change'), 10);
          var min = parseInt(input.getAttribute('min'), 10) || 1;
          var max = parseInt(input.getAttribute('max'), 10) || Infinity;
          var next = Math.min(max, Math.max(min, (parseInt(input.value, 10) || min) + step));
          if (String(next) === input.value) return;
          input.value = next;
          input.dispatchEvent(new Event('change', { bubbles: true }));
        });
      });
    });
  }

  /* --------------------------------------------------------- countdowns -- */

  function initCountdowns(root) {
    $$('[data-countdown]', root).forEach(function (el) {
      if (el.dataset.bound) return;
      el.dataset.bound = '1';

      var deadline;
      var fixed = el.getAttribute('data-countdown-until');

      if (fixed) {
        deadline = new Date(fixed).getTime();
      } else {
        // Rolling deadline: N hours from the visitor's first view, kept in
        // localStorage so the timer does not reset on every page load.
        var hours = parseFloat(el.getAttribute('data-countdown-hours')) || 12;
        var key = 'gp-countdown-' + (el.getAttribute('data-countdown-key') || 'default');
        var stored = null;
        try { stored = localStorage.getItem(key); } catch (e) { /* private mode */ }
        deadline = stored ? parseInt(stored, 10) : 0;
        if (!deadline || deadline < Date.now()) {
          deadline = Date.now() + hours * 3600 * 1000;
          try { localStorage.setItem(key, String(deadline)); } catch (e) { /* ignore */ }
        }
      }

      if (isNaN(deadline)) return;

      var parts = {
        days: $('[data-countdown-days]', el),
        hours: $('[data-countdown-hours-value]', el),
        minutes: $('[data-countdown-minutes]', el),
        seconds: $('[data-countdown-seconds]', el)
      };

      function pad(n) { return n < 10 ? '0' + n : String(n); }

      function tick() {
        var diff = deadline - Date.now();
        if (diff <= 0) {
          diff = 0;
          clearInterval(timer);
          if (el.hasAttribute('data-countdown-hide-when-done')) el.hidden = true;
        }
        var totalSeconds = Math.floor(diff / 1000);
        var d = Math.floor(totalSeconds / 86400);
        var h = Math.floor((totalSeconds % 86400) / 3600);
        var m = Math.floor((totalSeconds % 3600) / 60);
        var s = totalSeconds % 60;

        if (parts.days) parts.days.textContent = pad(d);
        if (parts.hours) parts.hours.textContent = pad(h);
        if (parts.minutes) parts.minutes.textContent = pad(m);
        if (parts.seconds) parts.seconds.textContent = pad(s);
      }

      tick();
      var timer = setInterval(tick, 1000);
    });
  }

  /* -------------------------------------------------------- rating bars -- */

  function initRatingBars(root) {
    var bars = $$('[data-rating-fill]', root);
    if (!bars.length) return;

    if (!('IntersectionObserver' in window) || prefersReducedMotion) {
      bars.forEach(function (bar) { bar.style.width = bar.getAttribute('data-rating-fill') + '%'; });
      return;
    }

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        entry.target.style.width = entry.target.getAttribute('data-rating-fill') + '%';
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.3 });

    bars.forEach(function (bar) { observer.observe(bar); });
  }

  /* ----------------------------------------------------------- count up -- */

  function initCountUp(root) {
    var els = $$('[data-count-to]', root);
    if (!els.length) return;

    if (!('IntersectionObserver' in window) || prefersReducedMotion) {
      els.forEach(function (el) { el.textContent = el.getAttribute('data-count-to'); });
      return;
    }

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var el = entry.target;
        observer.unobserve(el);

        var target = parseFloat(el.getAttribute('data-count-to'));
        var decimals = (el.getAttribute('data-count-to').split('.')[1] || '').length;
        var duration = 1200;
        var start = performance.now();

        function frame(now) {
          var progress = Math.min(1, (now - start) / duration);
          var eased = 1 - Math.pow(1 - progress, 3);
          el.textContent = (target * eased).toFixed(decimals);
          if (progress < 1) requestAnimationFrame(frame);
          else el.textContent = target.toFixed(decimals);
        }
        requestAnimationFrame(frame);
      });
    }, { threshold: 0.5 });

    els.forEach(function (el) { observer.observe(el); });
  }

  /* ------------------------------------------------------------ sliders -- */

  function initSliders(root) {
    $$('[data-slider]', root).forEach(function (slider) {
      if (slider.dataset.bound) return;
      slider.dataset.bound = '1';

      var track = $('[data-slider-track]', slider);
      var prev = $('[data-slider-prev]', slider);
      var next = $('[data-slider-next]', slider);
      if (!track) return;

      function step() {
        var first = track.firstElementChild;
        if (!first) return track.clientWidth;
        var gap = parseFloat(getComputedStyle(track).columnGap || getComputedStyle(track).gap) || 0;
        return first.getBoundingClientRect().width + gap;
      }

      function updateButtons() {
        if (!prev || !next) return;
        var maxScroll = track.scrollWidth - track.clientWidth - 2;
        prev.disabled = track.scrollLeft <= 2;
        next.disabled = track.scrollLeft >= maxScroll;
      }

      on(prev, 'click', function () { track.scrollBy({ left: -step(), behavior: 'smooth' }); });
      on(next, 'click', function () { track.scrollBy({ left: step(), behavior: 'smooth' }); });
      on(track, 'scroll', debounce(updateButtons, 80), { passive: true });
      window.addEventListener('resize', debounce(updateButtons, 200));
      updateButtons();
    });
  }

  /* ------------------------------------------------------ product media -- */

  function ProductGallery(el) {
    this.el = el;
    this.slides = $$('[data-media-slide]', el);
    this.thumbs = $$('[data-media-thumb]', el);
    this.index = Math.max(0, this.slides.findIndex(function (s) { return s.classList.contains('is-active'); }));

    var self = this;

    this.thumbs.forEach(function (thumb, i) {
      on(thumb, 'click', function () { self.goTo(i); });
    });

    on($('[data-media-prev]', el), 'click', function () {
      self.goTo((self.index - 1 + self.slides.length) % self.slides.length);
    });
    on($('[data-media-next]', el), 'click', function () {
      self.goTo((self.index + 1) % self.slides.length);
    });

    // Swipe on touch devices.
    var startX = null;
    on(el, 'touchstart', function (e) { startX = e.touches[0].clientX; }, { passive: true });
    on(el, 'touchend', function (e) {
      if (startX === null) return;
      var delta = e.changedTouches[0].clientX - startX;
      if (Math.abs(delta) > 45) {
        self.goTo(delta < 0
          ? (self.index + 1) % self.slides.length
          : (self.index - 1 + self.slides.length) % self.slides.length);
      }
      startX = null;
    }, { passive: true });
  }

  ProductGallery.prototype.goTo = function (index) {
    if (index < 0 || index >= this.slides.length) return;
    this.index = index;

    this.slides.forEach(function (slide, i) {
      slide.classList.toggle('is-active', i === index);
    });
    this.thumbs.forEach(function (thumb, i) {
      thumb.classList.toggle('is-active', i === index);
      thumb.setAttribute('aria-selected', String(i === index));
    });

    var activeThumb = this.thumbs[index];
    if (activeThumb && activeThumb.parentElement) {
      var parent = activeThumb.parentElement;
      var offset = activeThumb.offsetLeft - parent.offsetLeft - (parent.clientWidth / 2) + (activeThumb.clientWidth / 2);
      parent.scrollTo({ left: offset, behavior: prefersReducedMotion ? 'auto' : 'smooth' });
    }

    // Lazy images inside the newly shown slide.
    var img = this.slides[index] && this.slides[index].querySelector('img[loading="lazy"]');
    if (img) img.loading = 'eager';
  };

  ProductGallery.prototype.showMedia = function (mediaId) {
    if (!mediaId) return;
    var index = this.slides.findIndex(function (slide) {
      return slide.getAttribute('data-media-id') === String(mediaId);
    });
    if (index > -1) this.goTo(index);
  };

  /* ----------------------------------------------------- variant picker -- */

  function VariantPicker(el) {
    this.el = el;
    this.sectionId = el.getAttribute('data-section-id');
    this.productUrl = el.getAttribute('data-product-url');
    this.updateUrl = el.getAttribute('data-update-url') !== 'false';

    var dataEl = $('[data-variants-json]', el);
    try {
      this.variants = dataEl ? JSON.parse(dataEl.textContent) : [];
    } catch (e) {
      this.variants = [];
    }

    var galleryEl = document.querySelector('[data-gallery][data-section-id="' + this.sectionId + '"]');
    this.gallery = galleryEl ? new ProductGallery(galleryEl) : null;

    this.idInput = $('[data-variant-id]', el);
    this.priceTarget = document.querySelector('[data-price-target][data-section-id="' + this.sectionId + '"]');
    this.addButton = document.querySelector('[data-add-button][data-section-id="' + this.sectionId + '"]');
    this.skuTarget = document.querySelector('[data-sku-target][data-section-id="' + this.sectionId + '"]');
    this.inventoryTarget = document.querySelector('[data-inventory-target][data-section-id="' + this.sectionId + '"]');
    this.stickyPrice = document.querySelector('[data-sticky-price][data-section-id="' + this.sectionId + '"]');

    var self = this;
    on(el, 'change', function () { self.onChange(); });

    this.markUnavailable();
  }

  VariantPicker.prototype.selectedOptions = function () {
    var values = [];

    $$('[data-option-index]', this.el).forEach(function (group) {
      var index = parseInt(group.getAttribute('data-option-index'), 10);
      var checked = group.querySelector('input[type="radio"]:checked');
      if (checked) {
        values[index] = checked.value;
        return;
      }
      var select = group.querySelector('select');
      if (select) values[index] = select.value;
    });

    return values;
  };

  VariantPicker.prototype.findVariant = function (options) {
    return this.variants.find(function (variant) {
      return variant.options.every(function (option, i) { return option === options[i]; });
    });
  };

  // Strike through option values that cannot be combined with the current
  // selection. AliExpress imports often have sparse variant matrices.
  VariantPicker.prototype.markUnavailable = function () {
    var self = this;
    var selected = this.selectedOptions();

    $$('[data-option-index]', this.el).forEach(function (group) {
      var index = parseInt(group.getAttribute('data-option-index'), 10);

      $$('input[type="radio"]', group).forEach(function (input) {
        var candidate = selected.slice();
        candidate[index] = input.value;

        var match = self.variants.find(function (variant) {
          return variant.options.every(function (option, i) {
            return i === index ? option === input.value : (candidate[i] === undefined || option === candidate[i]);
          });
        });

        var label = group.querySelector('label[for="' + input.id + '"]');
        var unavailable = !match || !match.available;
        if (label) label.classList.toggle('is-unavailable', unavailable);
        input.setAttribute('data-unavailable', String(unavailable));
      });
    });
  };

  VariantPicker.prototype.onChange = function () {
    var options = this.selectedOptions();
    var variant = this.findVariant(options);

    this.markUnavailable();
    this.updateOptionLabels(options);

    if (!variant) {
      this.setUnavailable();
      return;
    }

    if (this.idInput) this.idInput.value = variant.id;
    if (this.priceTarget && variant.price_html) this.priceTarget.innerHTML = variant.price_html;
    if (this.stickyPrice && variant.price) this.stickyPrice.textContent = variant.price;

    if (this.skuTarget) {
      this.skuTarget.textContent = variant.sku ? 'SKU: ' + variant.sku : '';
      this.skuTarget.hidden = !variant.sku;
    }

    if (this.inventoryTarget && variant.inventory_html) {
      this.inventoryTarget.innerHTML = variant.inventory_html;
    }

    this.setAvailability(variant);
    if (this.gallery) this.gallery.showMedia(variant.featured_media_id);

    if (this.updateUrl && this.productUrl && window.history.replaceState) {
      window.history.replaceState({}, '', this.productUrl + '?variant=' + variant.id);
    }

    document.dispatchEvent(new CustomEvent('variant:change', { detail: { variant: variant, sectionId: this.sectionId } }));
  };

  VariantPicker.prototype.updateOptionLabels = function (options) {
    $$('[data-option-index]', this.el).forEach(function (group) {
      var index = parseInt(group.getAttribute('data-option-index'), 10);
      var display = group.querySelector('[data-option-selected]');
      if (display) display.textContent = options[index] || '';
    });
  };

  VariantPicker.prototype.setAvailability = function (variant) {
    if (!this.addButton) return;
    var label = $('[data-add-button-label]', this.addButton) || this.addButton;
    this.addButton.disabled = !variant.available;
    this.addButton.classList.toggle('is-disabled', !variant.available);
    label.textContent = variant.available ? T.strings.addToCart : T.strings.soldOut;
  };

  VariantPicker.prototype.setUnavailable = function () {
    if (this.idInput) this.idInput.value = '';
    if (!this.addButton) return;
    var label = $('[data-add-button-label]', this.addButton) || this.addButton;
    this.addButton.disabled = true;
    this.addButton.classList.add('is-disabled');
    label.textContent = T.strings.unavailable;
  };

  function initProduct(root) {
    $$('[data-variant-picker]', root).forEach(function (el) {
      if (el.dataset.bound) return;
      el.dataset.bound = '1';
      new VariantPicker(el);
    });

    // Galleries on products with a single variant still need controls.
    $$('[data-gallery]', root).forEach(function (el) {
      if (el.dataset.bound) return;
      el.dataset.bound = '1';
      new ProductGallery(el);
    });
  }

  /* ---------------------------------------------------------- sticky atc -- */

  function initStickyAtc(root) {
    $$('[data-sticky-atc]', root).forEach(function (bar) {
      if (bar.dataset.bound) return;
      bar.dataset.bound = '1';

      var sentinel = document.querySelector('[data-sticky-atc-sentinel]');
      if (!sentinel || !('IntersectionObserver' in window)) return;

      var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
          bar.classList.toggle('is-visible', !entry.isIntersecting && entry.boundingClientRect.top < 0);
        });
      }, { threshold: 0 });

      observer.observe(sentinel);
    });
  }

  /* --------------------------------------------------------------- cart -- */

  // Every region that can be re-rendered announces its own section id, because
  // sections inside a JSON template get a generated id rather than the filename.
  function cartSectionIds() {
    return $$('[data-cart-replace]')
      .map(function (el) { return el.getAttribute('data-section'); })
      .filter(Boolean);
  }

  // Used when no cart section is on the page (cart set to "page", browsing a
  // collection): the drawer markup is absent, so the badge needs its own source.
  function refreshCartCount() {
    return fetchJSON(T.routes.cartJson)
      .then(function (cart) { setCartCount(cart.item_count); })
      .catch(function () { /* the badge is not worth an error toast */ });
  }

  function renderCartSections(sections) {
    if (!sections) return;

    Object.keys(sections).forEach(function (id) {
      var html = sections[id];
      if (!html) return;

      var parsed = new DOMParser().parseFromString(html, 'text/html');
      var source = parsed.querySelector('[data-cart-replace]');
      var target = document.querySelector('[data-cart-replace][data-section="' + id + '"]');
      if (source && target) target.innerHTML = source.innerHTML;
    });

    syncCartCount();
    initAll(document);
  }

  function setCartCount(count) {
    $$('[data-cart-count]').forEach(function (badge) {
      badge.textContent = count > 99 ? '99+' : String(count);
      badge.hidden = count === 0;
    });

    var icon = $('[data-cart-icon]');
    if (icon && !prefersReducedMotion) {
      icon.classList.remove('cart-bump');
      void icon.offsetWidth;
      icon.classList.add('cart-bump');
    }
  }

  function syncCartCount() {
    var source = document.querySelector('[data-cart-total-count]');
    if (!source) return;
    setCartCount(parseInt(source.getAttribute('data-cart-total-count'), 10) || 0);
  }

  function setButtonLoading(button, loading) {
    if (!button) return;
    button.classList.toggle('is-loading', loading);
    button.disabled = loading;
  }

  function addToCart(formData, button) {
    setButtonLoading(button, true);

    var ids = cartSectionIds();
    if (ids.length) formData.append('sections', ids.join(','));

    return fetch(T.routes.cartAdd, {
      method: 'POST',
      headers: { Accept: 'application/javascript', 'X-Requested-With': 'XMLHttpRequest' },
      body: formData
    })
      .then(function (res) {
        return res.json().then(function (data) {
          if (!res.ok) throw data;
          return data;
        });
      })
      .then(function (data) {
        if (data.sections) renderCartSections(data.sections);
        else refreshCartCount();

        if (T.cartType === 'drawer' && $('[data-cart-drawer]')) {
          window.GlowPulseOpenCart();
        } else {
          toast(T.strings.added, 'success');
        }

        document.dispatchEvent(new CustomEvent('cart:updated', { detail: data }));
      })
      .catch(function (error) {
        var message = (error && (error.description || error.message)) || T.strings.error;
        toast(message, 'error');
      })
      .finally(function () {
        setButtonLoading(button, false);
      });
  }

  function changeCartLine(line, quantity, itemEl) {
    if (itemEl) itemEl.classList.add('is-updating');

    var body = { line: line, quantity: quantity };
    var ids = cartSectionIds();
    if (ids.length) body.sections = ids.join(',');

    return fetchJSON(T.routes.cartChange, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body)
    })
      .then(function (data) {
        renderCartSections(data.sections);
        document.dispatchEvent(new CustomEvent('cart:updated', { detail: data }));
      })
      .catch(function () {
        toast(T.strings.error, 'error');
        if (itemEl) itemEl.classList.remove('is-updating');
      });
  }

  function initCartEvents() {
    // Add to cart — covers the product form and every quick-add button.
    document.addEventListener('submit', function (evt) {
      var form = evt.target.closest('form[data-product-form], form.product-form-ajax');
      if (!form) return;
      evt.preventDefault();

      var button = form.querySelector('[data-add-button]') || form.querySelector('[type="submit"]');
      if (button && button.disabled) return;
      addToCart(new FormData(form), button);
    });

    // Quantity steppers and manual edits inside the cart.
    document.addEventListener('change', function (evt) {
      var input = evt.target.closest('[data-cart-quantity-input]');
      if (!input) return;

      var itemEl = input.closest('[data-cart-item]');
      var line = input.getAttribute('data-line');
      var quantity = Math.max(0, parseInt(input.value, 10) || 0);
      changeCartLine(line, quantity, itemEl);
    });

    document.addEventListener('click', function (evt) {
      var remove = evt.target.closest('[data-cart-remove]');
      if (remove) {
        evt.preventDefault();
        var itemEl = remove.closest('[data-cart-item]');
        changeCartLine(remove.getAttribute('data-line'), 0, itemEl);
        return;
      }

      var noteToggle = evt.target.closest('[data-cart-note-toggle]');
      if (noteToggle) {
        var noteField = document.getElementById(noteToggle.getAttribute('aria-controls'));
        if (noteField) {
          var hidden = noteField.hasAttribute('hidden');
          if (hidden) { noteField.removeAttribute('hidden'); noteField.querySelector('textarea').focus(); }
          else { noteField.setAttribute('hidden', ''); }
          noteToggle.setAttribute('aria-expanded', String(hidden));
        }
      }
    });

    // Persist the order note without blocking checkout.
    document.addEventListener('change', function (evt) {
      var note = evt.target.closest('[data-cart-note]');
      if (!note) return;

      fetch(T.routes.cartUpdate, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ note: note.value })
      }).catch(function () { /* the note is not worth interrupting checkout for */ });
    });
  }

  /* ------------------------------------------------- predictive search -- */

  function initSearch(root) {
    $$('[data-predictive-search]', root).forEach(function (wrapper) {
      if (wrapper.dataset.bound) return;
      wrapper.dataset.bound = '1';

      var input = $('input[type="search"]', wrapper);
      var results = $('[data-predictive-results]', wrapper);
      if (!input || !results || !T.routes.predictiveSearch) return;

      var run = debounce(function () {
        var q = input.value.trim();
        if (q.length < 2) {
          results.innerHTML = '';
          return;
        }

        var url = T.routes.predictiveSearch +
          '?q=' + encodeURIComponent(q) +
          '&resources[type]=product&resources[limit]=6&section_id=predictive-search';

        fetch(url)
          .then(function (res) { return res.text(); })
          .then(function (html) {
            var parsed = new DOMParser().parseFromString(html, 'text/html');
            var source = parsed.querySelector('[data-predictive-results-inner]');
            results.innerHTML = source ? source.innerHTML : '';
          })
          .catch(function () { results.innerHTML = ''; });
      }, 280);

      on(input, 'input', run);
    });
  }

  /* ------------------------------------------------------------- facets -- */

  function initFacets(root) {
    $$('[data-facet-form]', root).forEach(function (form) {
      if (form.dataset.bound) return;
      form.dataset.bound = '1';

      on(form, 'change', function () {
        var params = new URLSearchParams(new FormData(form)).toString();
        window.location.search = params;
      });
    });

    // Close the filter dropdowns when clicking outside of them.
    document.addEventListener('click', function (evt) {
      $$('.facet-details[open]').forEach(function (details) {
        if (!details.contains(evt.target)) details.removeAttribute('open');
      });
    });
  }

  /* ------------------------------------------------------- share button -- */

  function initShare(root) {
    $$('[data-share]', root).forEach(function (btn) {
      if (btn.dataset.bound) return;
      btn.dataset.bound = '1';

      on(btn, 'click', function () {
        var url = btn.getAttribute('data-share-url') || window.location.href;
        if (navigator.share) {
          navigator.share({ title: document.title, url: url }).catch(function () { /* cancelled */ });
        } else if (navigator.clipboard) {
          navigator.clipboard.writeText(url).then(function () { toast('Link copied', 'success'); });
        }
      });
    });
  }

  /* --------------------------------------------------------------- init -- */

  function initAll(root) {
    root = root || document;
    initReveals(root);
    initMobileMenu(root);
    initAnnouncement(root);
    initMarquee(root);
    initAccordions(root);
    initDescriptionClamp(root);
    initQuantity(root);
    initCountdowns(root);
    initRatingBars(root);
    initCountUp(root);
    initSliders(root);
    initProduct(root);
    initStickyAtc(root);
    initSearch(root);
    initFacets(root);
    initShare(root);
  }

  function boot() {
    initStickyHeader();
    initOverlays();
    initCartEvents();
    initAll(document);
    syncCartCount();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }

  /* Theme editor: re-initialise whatever the merchant just changed. */
  document.addEventListener('shopify:section:load', function (evt) {
    initAll(evt.target);
    initStickyHeader();
  });
  document.addEventListener('shopify:section:select', function (evt) {
    initAll(evt.target);
  });
  document.addEventListener('shopify:block:select', function (evt) {
    var slide = evt.target.closest('[data-media-slide]');
    if (slide) slide.scrollIntoView({ block: 'nearest' });
  });
})();
