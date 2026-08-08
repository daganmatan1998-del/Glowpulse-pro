/* ==========================================================================
   Glowpulse Pro — theme JavaScript
   No dependencies. Everything is a small custom element or a delegated
   listener so nothing has to be re-initialised after a section re-render.
   ========================================================================== */

(function () {
  'use strict';

  document.documentElement.classList.remove('no-js');

  /* ------------------------------------------------------------------ */
  /* Utilities                                                          */
  /* ------------------------------------------------------------------ */

  var FOCUSABLE =
    'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])';

  function debounce(fn, wait) {
    var timer;
    return function () {
      var args = arguments;
      var context = this;
      clearTimeout(timer);
      timer = setTimeout(function () {
        fn.apply(context, args);
      }, wait);
    };
  }

  window.themeStrings = window.themeStrings || {};
  window.themeRoutes = window.themeRoutes || {};

  /**
   * Self-contained implementation of Shopify's money filter so variant price
   * updates match the storefront format without loading option_selection.js.
   */
  function formatMoney(cents, format) {
    var moneyFormat = format || window.themeStrings.moneyFormat || '${{amount}}';
    var placeholder = /\{\{\s*(\w+)\s*\}\}/;

    function group(number, precision, thousands, decimal) {
      if (isNaN(number) || number === null) return '0';

      var value = (number / 100).toFixed(precision);
      var parts = value.split('.');
      var whole = parts[0].replace(/(\d)(?=(\d\d\d)+(?!\d))/g, '$1' + thousands);
      var fraction = parts[1] ? decimal + parts[1] : '';

      return whole + fraction;
    }

    var match = moneyFormat.match(placeholder);
    var value = '';

    switch (match && match[1]) {
      case 'amount':
        value = group(cents, 2, ',', '.');
        break;
      case 'amount_no_decimals':
        value = group(cents, 0, ',', '.');
        break;
      case 'amount_with_comma_separator':
        value = group(cents, 2, '.', ',');
        break;
      case 'amount_no_decimals_with_comma_separator':
        value = group(cents, 0, '.', ',');
        break;
      case 'amount_with_space_separator':
        value = group(cents, 2, ' ', ',');
        break;
      case 'amount_no_decimals_with_space_separator':
        value = group(cents, 0, ' ', ',');
        break;
      case 'amount_with_apostrophe_separator':
        value = group(cents, 2, "'", '.');
        break;
      default:
        value = group(cents, 2, ',', '.');
        break;
    }

    return moneyFormat.replace(placeholder, value);
  }

  /* ------------------------------------------------------------------ */
  /* Scroll lock                                                        */
  /* ------------------------------------------------------------------ */

  var scrollLockCount = 0;
  var savedScrollY = 0;

  function lockScroll() {
    if (scrollLockCount === 0) {
      savedScrollY = window.scrollY;
      document.body.style.position = 'fixed';
      document.body.style.top = -savedScrollY + 'px';
      document.body.style.width = '100%';
    }
    scrollLockCount += 1;
  }

  function unlockScroll() {
    scrollLockCount = Math.max(0, scrollLockCount - 1);
    if (scrollLockCount === 0) {
      document.body.style.position = '';
      document.body.style.top = '';
      document.body.style.width = '';
      window.scrollTo(0, savedScrollY);
    }
  }

  /* ------------------------------------------------------------------ */
  /* Drawers                                                            */
  /* ------------------------------------------------------------------ */

  var activeDrawer = null;
  var drawerOpener = null;

  function trapFocus(event) {
    if (!activeDrawer || event.key !== 'Tab') return;

    var focusable = Array.prototype.filter.call(
      activeDrawer.querySelectorAll(FOCUSABLE),
      function (el) {
        return el.offsetParent !== null;
      }
    );
    if (!focusable.length) return;

    var first = focusable[0];
    var last = focusable[focusable.length - 1];

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  function openDrawer(drawer, opener) {
    if (!drawer || activeDrawer === drawer) return;
    if (activeDrawer) closeDrawer(activeDrawer);

    activeDrawer = drawer;
    drawerOpener = opener || null;

    drawer.classList.add('is-open');
    drawer.setAttribute('aria-hidden', 'false');
    lockScroll();

    if (opener) opener.setAttribute('aria-expanded', 'true');

    // Wait for the panel transition to start before moving focus so the
    // browser does not scroll the page to the off-screen panel.
    window.requestAnimationFrame(function () {
      var target =
        drawer.querySelector('[data-drawer-close]:not(.drawer__overlay)') ||
        drawer.querySelector(FOCUSABLE);
      if (target) target.focus();
    });

    document.addEventListener('keydown', trapFocus);
  }

  function closeDrawer(drawer) {
    drawer = drawer || activeDrawer;
    if (!drawer || !drawer.classList.contains('is-open')) return;

    drawer.classList.remove('is-open');
    drawer.setAttribute('aria-hidden', 'true');
    unlockScroll();

    document.removeEventListener('keydown', trapFocus);

    if (drawerOpener && document.body.contains(drawerOpener)) {
      drawerOpener.setAttribute('aria-expanded', 'false');
      drawerOpener.focus();
    }

    activeDrawer = null;
    drawerOpener = null;
  }

  window.themeDrawers = { open: openDrawer, close: closeDrawer };

  document.addEventListener('click', function (event) {
    var opener = event.target.closest('[data-drawer-open]');
    if (opener) {
      var drawer = document.getElementById(opener.getAttribute('data-drawer-open'));
      if (drawer) {
        event.preventDefault();
        openDrawer(drawer, opener);
      }
      return;
    }

    if (event.target.closest('[data-drawer-close]')) {
      event.preventDefault();
      closeDrawer(event.target.closest('[data-drawer]'));
    }
  });

  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape' && activeDrawer) {
      closeDrawer(activeDrawer);
    }
  });

  /* ------------------------------------------------------------------ */
  /* Header height variable (used for sticky offsets)                   */
  /* ------------------------------------------------------------------ */

  function syncHeaderHeight() {
    var header = document.querySelector('[data-header-wrapper]');
    if (!header) return;
    document.documentElement.style.setProperty(
      '--header-offset',
      header.offsetHeight + 'px'
    );
  }

  window.addEventListener('resize', debounce(syncHeaderHeight, 150));
  document.addEventListener('DOMContentLoaded', syncHeaderHeight);
  syncHeaderHeight();

  /* ------------------------------------------------------------------ */
  /* Cart helpers                                                       */
  /* ------------------------------------------------------------------ */

  var CART_SECTIONS = ['cart-drawer', 'header', 'main-cart'];

  function sectionsToRender() {
    return CART_SECTIONS.filter(function (id) {
      return document.getElementById('shopify-section-' + id) !== null;
    });
  }

  function replaceFromSections(sections) {
    if (!sections) return;

    Object.keys(sections).forEach(function (key) {
      var html = sections[key];
      if (!html) return;

      var parsed = new DOMParser().parseFromString(html, 'text/html');

      // Each cart-aware region marks itself with data-cart-region + an id.
      parsed.querySelectorAll('[data-cart-region]').forEach(function (incoming) {
        var current = document.getElementById(incoming.id);
        if (current) current.innerHTML = incoming.innerHTML;
      });
    });
  }

  function publishCartUpdate(cart) {
    document.dispatchEvent(new CustomEvent('cart:updated', { detail: { cart: cart } }));
  }

  function openCartDrawer() {
    var drawer = document.getElementById('CartDrawer');
    if (drawer) {
      openDrawer(drawer, document.querySelector('[data-cart-icon]'));
      return true;
    }
    return false;
  }

  /* ------------------------------------------------------------------ */
  /* <quantity-input>                                                   */
  /* ------------------------------------------------------------------ */

  var QuantityInput = function () {
    return Reflect.construct(HTMLElement, [], QuantityInput);
  };
  QuantityInput.prototype = Object.create(HTMLElement.prototype);
  QuantityInput.prototype.constructor = QuantityInput;
  Object.setPrototypeOf(QuantityInput, HTMLElement);

  QuantityInput.prototype.connectedCallback = function () {
    var self = this;
    this.input = this.querySelector('input');
    if (!this.input) return;

    this.querySelectorAll('button').forEach(function (button) {
      button.addEventListener('click', function (event) {
        event.preventDefault();
        var step = button.dataset.action === 'increase' ? 1 : -1;
        var min = parseInt(self.input.min || '0', 10);
        var max = self.input.max ? parseInt(self.input.max, 10) : Infinity;
        var next = (parseInt(self.input.value, 10) || min) + step;

        next = Math.min(Math.max(next, min), max);
        if (next === parseInt(self.input.value, 10)) return;

        self.input.value = next;
        self.input.dispatchEvent(new Event('change', { bubbles: true }));
        self.refresh();
      });
    });

    this.input.addEventListener('change', function () {
      self.refresh();
    });
    this.refresh();
  };

  QuantityInput.prototype.refresh = function () {
    var value = parseInt(this.input.value, 10) || 0;
    var min = parseInt(this.input.min || '0', 10);
    var max = this.input.max ? parseInt(this.input.max, 10) : Infinity;

    var decrease = this.querySelector('[data-action="decrease"]');
    var increase = this.querySelector('[data-action="increase"]');

    if (decrease) decrease.disabled = value <= min;
    if (increase) increase.disabled = value >= max;
  };

  if (!customElements.get('quantity-input')) {
    customElements.define('quantity-input', QuantityInput);
  }

  /* ------------------------------------------------------------------ */
  /* <product-form> — AJAX add to cart                                  */
  /* ------------------------------------------------------------------ */

  var ProductForm = function () {
    return Reflect.construct(HTMLElement, [], ProductForm);
  };
  ProductForm.prototype = Object.create(HTMLElement.prototype);
  ProductForm.prototype.constructor = ProductForm;
  Object.setPrototypeOf(ProductForm, HTMLElement);

  ProductForm.prototype.connectedCallback = function () {
    var self = this;
    this.form = this.querySelector('form');
    if (!this.form) return;

    this.submitButton = this.querySelector('[type="submit"]');
    this.errorTarget = this.querySelector('[data-product-form-error]');

    this.form.addEventListener('submit', function (event) {
      if (self.dataset.ajax === 'false') return;
      event.preventDefault();
      self.submit();
    });
  };

  ProductForm.prototype.setLoading = function (loading) {
    if (!this.submitButton) return;
    this.submitButton.classList.toggle('button--loading', loading);
    this.submitButton.setAttribute('aria-busy', loading ? 'true' : 'false');
    this.submitButton.disabled = loading;
  };

  ProductForm.prototype.showError = function (message) {
    if (!this.errorTarget) {
      if (message) window.alert(message);
      return;
    }
    this.errorTarget.textContent = message || '';
    this.errorTarget.classList.toggle('is-visible', Boolean(message));
  };

  ProductForm.prototype.submit = function () {
    var self = this;
    var formData = new FormData(this.form);
    var toRender = sectionsToRender();

    formData.append('sections', toRender.join(','));
    formData.append('sections_url', window.location.pathname);

    this.showError('');
    this.setLoading(true);

    fetch(window.themeRoutes.cartAdd, {
      method: 'POST',
      headers: { Accept: 'application/javascript', 'X-Requested-With': 'XMLHttpRequest' },
      body: formData
    })
      .then(function (response) {
        return response.json().then(function (data) {
          return { ok: response.ok, data: data };
        });
      })
      .then(function (result) {
        if (!result.ok || result.data.status) {
          self.showError(result.data.description || result.data.message || '');
          return;
        }

        replaceFromSections(result.data.sections);
        syncHeaderHeight();

        return fetch(window.themeRoutes.cart + '.js')
          .then(function (r) {
            return r.json();
          })
          .then(function (cart) {
            publishCartUpdate(cart);
            if (!openCartDrawer()) {
              window.location.href = window.themeRoutes.cart;
            }
          });
      })
      .catch(function () {
        self.showError(window.themeStrings.cartError || 'Something went wrong. Please try again.');
      })
      .finally(function () {
        self.setLoading(false);
      });
  };

  if (!customElements.get('product-form')) {
    customElements.define('product-form', ProductForm);
  }

  /* ------------------------------------------------------------------ */
  /* <variant-picker>                                                   */
  /* ------------------------------------------------------------------ */

  var VariantPicker = function () {
    return Reflect.construct(HTMLElement, [], VariantPicker);
  };
  VariantPicker.prototype = Object.create(HTMLElement.prototype);
  VariantPicker.prototype.constructor = VariantPicker;
  Object.setPrototypeOf(VariantPicker, HTMLElement);

  VariantPicker.prototype.connectedCallback = function () {
    var self = this;
    var data = this.querySelector('[type="application/json"]');

    this.variants = data ? JSON.parse(data.textContent) : [];
    this.sectionId = this.dataset.section;
    this.productUrl = this.dataset.url;

    this.addEventListener('change', function () {
      self.onVariantChange();
    });
  };

  VariantPicker.prototype.selectedOptions = function () {
    var options = [];

    this.querySelectorAll('select[data-option-index]').forEach(function (select) {
      options[parseInt(select.dataset.optionIndex, 10)] = select.value;
    });

    this.querySelectorAll('input[type="radio"]:checked').forEach(function (input) {
      options[parseInt(input.dataset.optionIndex, 10)] = input.value;
    });

    return options;
  };

  VariantPicker.prototype.onVariantChange = function () {
    var selected = this.selectedOptions();

    var variant = this.variants.find(function (candidate) {
      return candidate.options.every(function (option, index) {
        return option === selected[index];
      });
    });

    this.updateSwatchLabels(selected);
    this.updateAvailability(selected);
    this.updateInput(variant);
    this.updateUrl(variant);
    this.updatePrice(variant);
    this.updateBuyButton(variant);

    document.dispatchEvent(
      new CustomEvent('variant:changed', { detail: { variant: variant, sectionId: this.sectionId } })
    );
  };

  VariantPicker.prototype.updateSwatchLabels = function (selected) {
    this.querySelectorAll('[data-selected-value]').forEach(function (label) {
      var index = parseInt(label.dataset.selectedValue, 10);
      label.textContent = selected[index] || '';
    });
  };

  // Greys out combinations that do not exist so shoppers do not hit dead ends.
  VariantPicker.prototype.updateAvailability = function (selected) {
    var variants = this.variants;

    this.querySelectorAll('input[type="radio"][data-option-index]').forEach(function (input) {
      var index = parseInt(input.dataset.optionIndex, 10);
      var candidate = selected.slice();
      candidate[index] = input.value;

      var match = variants.find(function (variant) {
        return variant.options.every(function (option, i) {
          return candidate[i] === undefined || option === candidate[i];
        });
      });

      var label = input.nextElementSibling;
      if (!label) return;
      label.classList.toggle('variant-option__swatch--unavailable', !match || !match.available);
    });
  };

  VariantPicker.prototype.updateInput = function (variant) {
    var input = document.querySelector('#VariantId-' + this.sectionId);
    if (input && variant) input.value = variant.id;
  };

  VariantPicker.prototype.updateUrl = function (variant) {
    if (!variant || !this.productUrl || this.dataset.updateUrl === 'false') return;
    window.history.replaceState({}, '', this.productUrl + '?variant=' + variant.id);
  };

  VariantPicker.prototype.updatePrice = function (variant) {
    var self = this;
    var targets = document.querySelectorAll(
      '[data-price-block][data-section="' + this.sectionId + '"]'
    );

    targets.forEach(function (target) {
      self.renderPrice(target, variant);
    });
  };

  VariantPicker.prototype.renderPrice = function (target, variant) {
    if (!variant) {
      target.setAttribute('hidden', '');
      return;
    }

    target.removeAttribute('hidden');

    var current = target.querySelector('[data-price-current]');
    var compare = target.querySelector('[data-price-compare]');
    var saving = target.querySelector('[data-price-saving]');

    if (current) current.innerHTML = formatMoney(variant.price);

    var onSale = variant.compare_at_price && variant.compare_at_price > variant.price;
    target.classList.toggle('price--on-sale', Boolean(onSale));

    if (compare) {
      compare.innerHTML = onSale ? formatMoney(variant.compare_at_price) : '';
      compare.hidden = !onSale;
    }

    if (saving) {
      if (onSale) {
        var percent = Math.round(
          ((variant.compare_at_price - variant.price) / variant.compare_at_price) * 100
        );
        saving.textContent = (window.themeStrings.savePercent || 'Save __PCT__%').replace(
          '__PCT__',
          percent
        );
        saving.hidden = false;
      } else {
        saving.hidden = true;
      }
    }
  };

  VariantPicker.prototype.updateBuyButton = function (variant) {
    var self = this;
    var buttons = document.querySelectorAll('[data-add-to-cart][data-section="' + this.sectionId + '"]');

    buttons.forEach(function (button) {
      var label = button.querySelector('.button__label') || button;

      if (!variant) {
        button.disabled = true;
        label.textContent = window.themeStrings.unavailable || 'Unavailable';
      } else if (!variant.available) {
        button.disabled = true;
        label.textContent = window.themeStrings.soldOut || 'Sold out';
      } else {
        button.disabled = false;
        label.textContent = self.dataset.buttonLabel || window.themeStrings.addToCart || 'Add to cart';
      }
    });
  };

  if (!customElements.get('variant-picker')) {
    customElements.define('variant-picker', VariantPicker);
  }

  /* ------------------------------------------------------------------ */
  /* <product-gallery>                                                  */
  /* ------------------------------------------------------------------ */

  var ProductGallery = function () {
    return Reflect.construct(HTMLElement, [], ProductGallery);
  };
  ProductGallery.prototype = Object.create(HTMLElement.prototype);
  ProductGallery.prototype.constructor = ProductGallery;
  Object.setPrototypeOf(ProductGallery, HTMLElement);

  ProductGallery.prototype.connectedCallback = function () {
    var self = this;

    this.track = this.querySelector('[data-gallery-track]');
    if (!this.track) return;

    this.slides = Array.prototype.slice.call(this.track.querySelectorAll('[data-gallery-slide]'));
    this.thumbs = Array.prototype.slice.call(this.querySelectorAll('[data-gallery-thumb]'));
    this.dots = Array.prototype.slice.call(this.querySelectorAll('[data-gallery-dot]'));
    this.counter = this.querySelector('[data-gallery-counter]');
    this.prev = this.querySelector('[data-gallery-prev]');
    this.next = this.querySelector('[data-gallery-next]');
    this.index = 0;

    this.thumbs.concat(this.dots).forEach(function (control, position) {
      control.addEventListener('click', function (event) {
        event.preventDefault();
        self.goTo(parseInt(control.dataset.index, 10) || 0);
      });
    });

    if (this.prev) {
      this.prev.addEventListener('click', function () {
        self.goTo(self.index - 1);
      });
    }
    if (this.next) {
      this.next.addEventListener('click', function () {
        self.goTo(self.index + 1);
      });
    }

    this.track.addEventListener(
      'scroll',
      debounce(function () {
        self.syncFromScroll();
      }, 90)
    );

    // Jump to the image matching the selected variant.
    document.addEventListener('variant:changed', function (event) {
      var variant = event.detail.variant;
      if (!variant || !variant.featured_media || event.detail.sectionId !== self.dataset.section) return;

      var target = self.slides.findIndex(function (slide) {
        return slide.dataset.mediaId === String(variant.featured_media.id);
      });
      if (target > -1) self.goTo(target);
    });

    this.update();
  };

  ProductGallery.prototype.goTo = function (index) {
    if (index < 0 || index >= this.slides.length) return;
    this.index = index;

    this.track.scrollTo({
      left: this.slides[index].offsetLeft - this.track.offsetLeft,
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
    });

    this.update();
  };

  ProductGallery.prototype.syncFromScroll = function () {
    var scrollLeft = this.track.scrollLeft;
    var closest = 0;
    var smallest = Infinity;
    var trackLeft = this.track.offsetLeft;

    this.slides.forEach(function (slide, index) {
      var distance = Math.abs(slide.offsetLeft - trackLeft - scrollLeft);
      if (distance < smallest) {
        smallest = distance;
        closest = index;
      }
    });

    if (closest !== this.index) {
      this.index = closest;
      this.update();
    }
  };

  ProductGallery.prototype.update = function () {
    var self = this;

    this.thumbs.forEach(function (thumb, index) {
      thumb.setAttribute('aria-current', index === self.index ? 'true' : 'false');
    });

    this.dots.forEach(function (dot, index) {
      dot.setAttribute('aria-current', index === self.index ? 'true' : 'false');
    });

    if (this.counter) {
      this.counter.textContent = this.index + 1 + ' / ' + this.slides.length;
    }

    if (this.prev) this.prev.disabled = this.index === 0;
    if (this.next) this.next.disabled = this.index === this.slides.length - 1;
  };

  if (!customElements.get('product-gallery')) {
    customElements.define('product-gallery', ProductGallery);
  }

  /* ------------------------------------------------------------------ */
  /* <cart-items> — quantity changes and removals                       */
  /* ------------------------------------------------------------------ */

  var CartItems = function () {
    return Reflect.construct(HTMLElement, [], CartItems);
  };
  CartItems.prototype = Object.create(HTMLElement.prototype);
  CartItems.prototype.constructor = CartItems;
  Object.setPrototypeOf(CartItems, HTMLElement);

  CartItems.prototype.connectedCallback = function () {
    var self = this;

    this.addEventListener(
      'change',
      debounce(function (event) {
        var input = event.target.closest('[data-cart-quantity]');
        if (!input) return;
        self.change(input.dataset.line, input.value);
      }, 250)
    );

    this.addEventListener('click', function (event) {
      var remove = event.target.closest('[data-cart-remove]');
      if (!remove) return;
      event.preventDefault();
      self.change(remove.dataset.line, 0);
    });
  };

  CartItems.prototype.change = function (line, quantity) {
    var self = this;
    this.classList.add('is-loading');

    fetch(window.themeRoutes.cartChange, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({
        line: line,
        quantity: parseInt(quantity, 10),
        sections: sectionsToRender().join(','),
        sections_url: window.location.pathname
      })
    })
      .then(function (response) {
        return response.json();
      })
      .then(function (cart) {
        replaceFromSections(cart.sections);
        publishCartUpdate(cart);
        syncHeaderHeight();
      })
      .catch(function () {
        window.location.reload();
      })
      .finally(function () {
        self.classList.remove('is-loading');
      });
  };

  if (!customElements.get('cart-items')) {
    customElements.define('cart-items', CartItems);
  }

  /* ------------------------------------------------------------------ */
  /* Sticky add to cart                                                 */
  /* ------------------------------------------------------------------ */

  function initStickyAtc() {
    var sticky = document.querySelector('[data-sticky-atc]');
    if (!sticky || !('IntersectionObserver' in window)) return;
    if (sticky.dataset.observed === 'true') return;

    var anchor = document.querySelector('[data-sticky-atc-anchor]');
    if (!anchor) return;

    sticky.dataset.observed = 'true';

    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          // Show the bar only once the real buy button has scrolled away.
          var passed = entry.boundingClientRect.top < 0 && !entry.isIntersecting;
          sticky.classList.toggle('is-visible', passed);
        });
      },
      { threshold: 0, rootMargin: '0px' }
    );

    observer.observe(anchor);
  }

  /* ------------------------------------------------------------------ */
  /* Reveal on scroll                                                   */
  /* ------------------------------------------------------------------ */

  function initReveal(root) {
    var targets = (root || document).querySelectorAll('.animate-in:not(.is-visible)');
    if (!targets.length) return;

    if (!('IntersectionObserver' in window)) {
      targets.forEach(function (el) {
        el.classList.add('is-visible');
      });
      return;
    }

    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        });
      },
      { rootMargin: '0px 0px -40px 0px', threshold: 0.06 }
    );

    targets.forEach(function (el) {
      observer.observe(el);
    });
  }

  /* ------------------------------------------------------------------ */
  /* Accordions — close siblings when "one open at a time" is set       */
  /* ------------------------------------------------------------------ */

  document.addEventListener('toggle', function (event) {
    var details = event.target;
    if (!details.matches || !details.matches('[data-accordion-exclusive]') || !details.open) return;

    var group = details.closest('[data-accordion-group]');
    if (!group) return;

    group.querySelectorAll('[data-accordion-exclusive][open]').forEach(function (other) {
      if (other !== details) other.open = false;
    });
  }, true);

  /* ------------------------------------------------------------------ */
  /* Boot + Theme editor re-initialisation                              */
  /* ------------------------------------------------------------------ */

  function boot(root) {
    initReveal(root);
    initStickyAtc();
    syncHeaderHeight();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      boot(document);
    });
  } else {
    boot(document);
  }

  document.addEventListener('shopify:section:load', function (event) {
    boot(event.target);
  });

  // Only drawers that opt in pop open when their section is selected in the
  // theme editor — the header's menu drawer would otherwise hide the header.
  document.addEventListener('shopify:section:select', function (event) {
    var drawer = event.target.querySelector('[data-drawer][data-drawer-editor-open]');
    if (drawer) openDrawer(drawer, null);
  });

  document.addEventListener('shopify:section:deselect', function (event) {
    var drawer = event.target.querySelector('[data-drawer][data-drawer-editor-open]');
    if (drawer) closeDrawer(drawer);
  });

  document.addEventListener('shopify:block:select', function (event) {
    var slide = event.target.closest('[data-gallery-slide]');
    if (slide) {
      var gallery = slide.closest('product-gallery');
      if (gallery && gallery.goTo) {
        gallery.goTo(Array.prototype.indexOf.call(gallery.slides, slide));
      }
    }
  });
})();
