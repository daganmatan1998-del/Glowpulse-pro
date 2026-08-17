/* ==========================================================================
   Kalda — storefront interactions
   ========================================================================== */
(function () {
  'use strict';

  var S = window.KaldaStore;
  var $ = function (sel, root) { return (root || document).querySelector(sel); };
  var $$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };
  var reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* ====================== toasts ====================== */
  var toastHost = $('#toasts');
  function toast(message, isError) {
    if (!toastHost) return;
    var icon = isError
      ? '<circle cx="12" cy="12" r="9"/><path d="M12 7.5v5M12 16h.01"/>'
      : '<path d="M5 12.5l4.5 4.5L19 7.5"/>';
    var el = document.createElement('div');
    el.className = 'toast' + (isError ? ' toast--bad' : '');
    el.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true">' + icon + '</svg><span></span>';
    $('span', el).textContent = message;
    toastHost.appendChild(el);
    setTimeout(function () {
      el.classList.add('is-out');
      setTimeout(function () { el.remove(); }, 320);
    }, 2600);
  }

  /* ====================== announcement ====================== */
  var announce = $('#announce');
  var announceClose = $('#announceClose');
  if (announce && announceClose) {
    if (sessionStorage.getItem('kalda.announce') === 'closed') announce.classList.add('is-hidden');
    announceClose.addEventListener('click', function () {
      announce.classList.add('is-hidden');
      try { sessionStorage.setItem('kalda.announce', 'closed'); } catch (e) {}
    });
  }

  $$('[data-copy-code]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var code = btn.getAttribute('data-copy-code');
      var done = function () { toast('Code ' + code + ' copied — paste it in the cart.'); };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(code).then(done, done);
      } else {
        done();
      }
    });
  });

  /* ====================== nav ====================== */
  var nav = $('#nav');
  var menuBtn = $('#menuBtn');
  var mobileMenu = $('#mobileMenu');

  var onScroll = function () {
    if (nav) nav.classList.toggle('is-stuck', window.scrollY > 8);
  };
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  function setMenu(open) {
    if (!mobileMenu || !menuBtn) return;
    mobileMenu.hidden = false;
    mobileMenu.classList.toggle('is-open', open);
    menuBtn.setAttribute('aria-expanded', String(open));
    menuBtn.setAttribute('aria-label', open ? 'Close menu' : 'Open menu');
  }
  if (menuBtn) {
    menuBtn.addEventListener('click', function () {
      setMenu(menuBtn.getAttribute('aria-expanded') !== 'true');
    });
  }
  $$('.mobile-menu__link, [data-close-menu]').forEach(function (a) {
    a.addEventListener('click', function () { setMenu(false); });
  });

  /* scroll spy */
  var spyLinks = $$('.nav__link');
  var spyTargets = spyLinks
    .map(function (a) { return document.querySelector(a.getAttribute('href')); })
    .filter(Boolean);

  if (spyTargets.length && 'IntersectionObserver' in window) {
    var spy = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        spyLinks.forEach(function (a) {
          a.classList.toggle('is-current', a.getAttribute('href') === '#' + entry.target.id);
        });
      });
    }, { rootMargin: '-45% 0px -50% 0px' });
    spyTargets.forEach(function (t) { spy.observe(t); });
  }

  /* ====================== scroll reveal ====================== */
  var revealables = $$('.reveal, .reveal-shot');
  revealables.forEach(function (el) {
    if (el.dataset.d) el.style.setProperty('--d', el.dataset.d);
  });

  if (reduced || !('IntersectionObserver' in window)) {
    revealables.forEach(function (el) { el.classList.add('is-in'); });
  } else {
    var revealObserver = new IntersectionObserver(function (entries, obs) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-in');
        obs.unobserve(entry.target);
      });
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 });
    revealables.forEach(function (el) { revealObserver.observe(el); });
  }

  /* ====================== buy box ====================== */
  var variant = 'clay';
  var qtyInput = $('#qtyInput');
  var addBtn = $('#addToCart');

  function selectedAddons() {
    return $$('[data-addon]').filter(function (i) { return i.checked; })
      .map(function (i) { return i.getAttribute('data-addon'); });
  }

  function selectionValue() {
    var qty = currentQty();
    var sum = S.PRODUCT.price * qty;
    selectedAddons().forEach(function (k) { sum += S.ADDONS[k].price; });
    return sum;
  }

  function currentQty() {
    if (!qtyInput) return 1;
    var n = parseInt(qtyInput.value, 10);
    if (isNaN(n) || n < 1) n = 1;
    if (n > S.MAX_PER_LINE) n = S.MAX_PER_LINE;
    return n;
  }

  function renderStock() {
    var box = $('#stock');
    var fill = $('#stockFill');
    var text = $('#stockText');
    if (!box || !fill || !text) return;

    var left = S.stockFor(variant);
    var pct = Math.max(4, Math.min(100, (left / 90) * 100));
    fill.style.width = pct + '%';

    var low = left <= 15;
    box.classList.toggle('is-low', low);

    if (left === 0) {
      text.innerHTML = '<strong>Sold out in ' + S.PRODUCT.variants[variant].label + '</strong> — back on 4 September';
    } else if (low) {
      text.innerHTML = 'Only <strong>' + left + ' left</strong> in ' + S.PRODUCT.variants[variant].label + ' — restocks 4 September';
    } else {
      text.textContent = left + ' in stock — order before 2 pm and it ships today';
    }

    if (addBtn) {
      addBtn.disabled = left === 0;
      addBtn.style.opacity = left === 0 ? '.5' : '';
      addBtn.style.pointerEvents = left === 0 ? 'none' : '';
    }
  }

  function renderPrice() {
    var label = $('#addPrice');
    if (label) label.textContent = S.money(selectionValue());
  }

  function renderShipMeter() {
    var meter = $('#shipMeter');
    var fill = $('#shipFill');
    var text = $('#shipText');
    if (!meter || !fill || !text) return;

    var prospective = S.subtotal() - S.discount() + selectionValue();
    var gap = Math.max(0, S.FREE_SHIPPING_AT - prospective);
    var pct = Math.min(100, (prospective / S.FREE_SHIPPING_AT) * 100);

    fill.style.width = pct + '%';
    meter.classList.toggle('is-free', gap === 0);
    text.innerHTML = gap === 0
      ? 'Free carbon-neutral shipping unlocked'
      : 'You\'re <strong>' + S.money(gap) + '</strong> away from free shipping';
  }

  function renderWish() {
    var btn = $('#wishBtn');
    if (!btn) return;
    var on = S.inWish(variant);
    btn.setAttribute('aria-pressed', String(on));
    btn.setAttribute('aria-label', on ? 'Remove from wishlist' : 'Save to wishlist');
  }

  function setVariant(key) {
    if (!S.PRODUCT.variants[key]) return;
    variant = key;
    document.body.setAttribute('data-variant', key);

    $$('.swatch').forEach(function (sw) {
      var on = sw.getAttribute('data-variant') === key;
      sw.classList.toggle('is-active', on);
      sw.setAttribute('aria-checked', String(on));
    });
    var name = $('#variantName');
    if (name) name.textContent = S.PRODUCT.variants[key].label;

    renderStock();
    renderWish();
  }

  $$('.swatch').forEach(function (sw) {
    sw.addEventListener('click', function () { setVariant(sw.getAttribute('data-variant')); });
  });

  function setQtyValue(n) {
    if (!qtyInput) return;
    var v = Math.max(1, Math.min(S.MAX_PER_LINE, n));
    qtyInput.value = v;
    var minus = $('#qtyMinus');
    var plus = $('#qtyPlus');
    if (minus) minus.disabled = v <= 1;
    if (plus) plus.disabled = v >= S.MAX_PER_LINE;
    renderPrice();
    renderShipMeter();
  }

  var qtyMinus = $('#qtyMinus');
  var qtyPlus = $('#qtyPlus');
  if (qtyMinus) qtyMinus.addEventListener('click', function () { setQtyValue(currentQty() - 1); });
  if (qtyPlus) qtyPlus.addEventListener('click', function () { setQtyValue(currentQty() + 1); });
  if (qtyInput) qtyInput.addEventListener('change', function () { setQtyValue(currentQty()); });

  $$('[data-addon]').forEach(function (input) {
    input.addEventListener('change', function () {
      renderPrice();
      renderShipMeter();
    });
  });

  function bumpCartIcon() {
    var btn = $('#cartBtn');
    if (!btn || reduced) return;
    btn.classList.remove('is-bump');
    void btn.offsetWidth;
    btn.classList.add('is-bump');
  }

  function addCurrentSelection() {
    var qty = currentQty();
    S.addProduct(variant, qty);
    selectedAddons().forEach(function (k) {
      S.addAddon(k);
      var box = $('[data-addon="' + k + '"]');
      if (box) box.checked = false;
    });
    bumpCartIcon();
    renderPrice();
    return qty;
  }

  if (addBtn) {
    addBtn.addEventListener('click', function () {
      var qty = addCurrentSelection();
      toast(qty + ' × ' + S.PRODUCT.variants[variant].label + ' set added to your cart');
      openCart();
    });
  }

  var buyNow = $('#buyNow');
  if (buyNow) {
    buyNow.addEventListener('click', function () {
      addCurrentSelection();
      window.location.href = 'checkout.html';
    });
  }

  $$('[data-quick-add]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      S.addProduct(variant, 1);
      bumpCartIcon();
      toast('Bloom set in ' + S.PRODUCT.variants[variant].label + ' added to your cart');
      openCart();
    });
  });

  var wishBtn = $('#wishBtn');
  if (wishBtn) {
    wishBtn.addEventListener('click', function () {
      var added = S.toggleWish(variant);
      renderWish();
      if (!reduced) {
        wishBtn.classList.remove('is-pop');
        void wishBtn.offsetWidth;
        wishBtn.classList.add('is-pop');
      }
      toast(added
        ? 'Saved the ' + S.PRODUCT.variants[variant].label + ' set to your wishlist'
        : 'Removed from your wishlist');
    });
  }

  var wishNav = $('#wishBtnNav');
  if (wishNav) {
    wishNav.addEventListener('click', function () {
      var n = S.wishCount();
      if (n === 0) {
        toast('Your wishlist is empty — tap the heart to save a colourway');
      } else {
        toast(n === 1 ? '1 colourway saved for later' : n + ' colourways saved for later');
      }
      var product = $('#product');
      if (product) product.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'start' });
    });
  }

  /* ====================== cart drawer ====================== */
  var drawer = $('#cartDrawer');
  var scrim = $('#scrim');
  var cartBtn = $('#cartBtn');
  var lastFocus = null;

  function openCart() {
    if (!drawer || !scrim) return;
    lastFocus = document.activeElement;
    scrim.hidden = false;
    requestAnimationFrame(function () { scrim.classList.add('is-open'); });
    drawer.classList.add('is-open');
    drawer.setAttribute('aria-hidden', 'false');
    if (cartBtn) cartBtn.setAttribute('aria-expanded', 'true');
    document.body.classList.add('is-locked');
    var close = $('#cartClose');
    if (close) close.focus();
  }

  function closeCart() {
    if (!drawer || !scrim) return;
    drawer.classList.remove('is-open');
    drawer.setAttribute('aria-hidden', 'true');
    scrim.classList.remove('is-open');
    if (cartBtn) cartBtn.setAttribute('aria-expanded', 'false');
    document.body.classList.remove('is-locked');
    setTimeout(function () { scrim.hidden = true; }, 320);
    if (lastFocus && lastFocus.focus) lastFocus.focus();
  }

  if (cartBtn) cartBtn.addEventListener('click', openCart);
  var cartClose = $('#cartClose');
  if (cartClose) cartClose.addEventListener('click', closeCart);
  if (scrim) scrim.addEventListener('click', closeCart);
  $$('[data-close-cart]').forEach(function (b) {
    b.addEventListener('click', function () {
      closeCart();
      var product = $('#product');
      if (product) product.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth' });
    });
  });

  function lineNode(line) {
    var el = document.createElement('article');
    el.className = 'line';
    el.innerHTML =
      '<div class="line__media"><svg viewBox="0 0 800 800" aria-hidden="true"><use href="' + line.symbol + '"/></svg></div>' +
      '<div class="line__info">' +
        '<p class="line__name"></p>' +
        '<p class="line__opt"></p>' +
        '<div class="line__row">' +
          '<div class="line__qty">' +
            '<button type="button" data-dec aria-label="Decrease quantity"><svg viewBox="0 0 24 24"><path d="M6 12h12"/></svg></button>' +
            '<span></span>' +
            '<button type="button" data-inc aria-label="Increase quantity"><svg viewBox="0 0 24 24"><path d="M12 6v12M6 12h12"/></svg></button>' +
          '</div>' +
          '<span class="line__price"></span>' +
        '</div>' +
        '<button class="line__remove" type="button" data-remove>Remove</button>' +
      '</div>';

    $('.line__name', el).textContent = line.name;
    $('.line__opt', el).textContent = line.option;
    $('.line__qty span', el).textContent = line.qty;
    $('.line__price', el).textContent = S.money(line.price * line.qty);

    $('[data-dec]', el).addEventListener('click', function () { S.setQty(line.id, line.qty - 1); });
    $('[data-inc]', el).addEventListener('click', function () { S.setQty(line.id, line.qty + 1); });
    $('[data-remove]', el).addEventListener('click', function () {
      el.classList.add('is-leaving');
      setTimeout(function () { S.removeLine(line.id); }, reduced ? 0 : 220);
    });
    return el;
  }

  function renderCart() {
    var lines = S.lines();
    var n = S.count();

    [['#cartCount', n], ['#wishCount', S.wishCount()]].forEach(function (pair) {
      var badge = $(pair[0]);
      if (!badge) return;
      badge.textContent = pair[1];
      badge.hidden = pair[1] === 0;
    });

    var drawerCount = $('#drawerCount');
    if (drawerCount) drawerCount.textContent = n;

    if (drawer) drawer.classList.toggle('is-empty', lines.length === 0);

    var host = $('#cartLines');
    if (host) {
      host.textContent = '';
      lines.forEach(function (line) { host.appendChild(lineNode(line)); });
    }

    var sub = S.subtotal();
    var disc = S.discount();
    var ship = S.shipping();

    var tSub = $('#tSub'); if (tSub) tSub.textContent = S.money(sub);
    var tDisc = $('#tDisc'); if (tDisc) tDisc.textContent = '−' + S.money(disc);
    var tDiscRow = $('#tDiscRow'); if (tDiscRow) tDiscRow.hidden = disc <= 0;
    var tShip = $('#tShip'); if (tShip) tShip.textContent = lines.length === 0 ? '—' : (ship === 0 ? 'Free' : S.money(ship));
    var tTotal = $('#tTotal'); if (tTotal) tTotal.textContent = S.money(S.total());

    var applied = $('#promoApplied');
    var tag = $('#promoTag');
    if (applied && tag) {
      var label = S.promoLabel();
      applied.hidden = !label;
      if (label) tag.textContent = S.promoCode() + ' · ' + label;
    }

    var ds = $('.drawer__ship');
    var dsFill = $('#drawerShipFill');
    var dsText = $('#drawerShipText');
    if (ds && dsFill && dsText) {
      var gap = S.shippingGap();
      dsFill.style.width = Math.min(100, ((sub - disc) / S.FREE_SHIPPING_AT) * 100) + '%';
      ds.classList.toggle('is-free', gap === 0 && lines.length > 0);
      dsText.innerHTML = (gap === 0 && lines.length > 0)
        ? 'Free carbon-neutral shipping unlocked'
        : 'Add <strong>' + S.money(gap) + '</strong> for free shipping';
    }

    renderStock();
    renderShipMeter();
  }

  /* promo */
  var promoForm = $('#promoForm');
  if (promoForm) {
    promoForm.addEventListener('submit', function (e) {
      e.preventDefault();
      var input = $('#promoInput');
      var msg = $('#promoMsg');
      var res = S.applyPromo(input.value);
      if (msg) {
        msg.textContent = res.message;
        msg.classList.toggle('is-bad', !res.ok);
      }
      if (res.ok) {
        input.value = '';
        toast(res.label + ' applied to your order');
      }
    });
  }
  var promoRemove = $('#promoRemove');
  if (promoRemove) {
    promoRemove.addEventListener('click', function () {
      S.removePromo();
      var msg = $('#promoMsg');
      if (msg) { msg.textContent = ''; msg.classList.remove('is-bad'); }
    });
  }

  window.addEventListener('kalda:change', renderCart);

  /* ====================== gallery ====================== */
  var symbols = ['#shotSet', '#shotDripper', '#shotCarafe', '#shotFilter', '#shotScene', '#shotColors'];
  var galleryIndex = 0;
  var viewport = $('#galleryViewport');
  var slides = $$('.gallery__img');
  var thumbs = $$('.thumb');

  function showSlide(i) {
    galleryIndex = (i + slides.length) % slides.length;
    slides.forEach(function (s, k) { s.classList.toggle('is-active', k === galleryIndex); });
    thumbs.forEach(function (t, k) {
      t.classList.toggle('is-active', k === galleryIndex);
      t.setAttribute('aria-selected', String(k === galleryIndex));
    });
  }

  thumbs.forEach(function (t) {
    t.addEventListener('click', function () { showSlide(parseInt(t.getAttribute('data-i'), 10)); });
  });

  if (viewport) {
    viewport.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowRight') { e.preventDefault(); showSlide(galleryIndex + 1); }
      if (e.key === 'ArrowLeft') { e.preventDefault(); showSlide(galleryIndex - 1); }
    });

    /* pointer-tracked magnifier, desktop only */
    if (window.matchMedia('(hover: hover) and (pointer: fine)').matches && !reduced) {
      viewport.addEventListener('mouseenter', function () { viewport.classList.add('is-zoom'); });
      viewport.addEventListener('mouseleave', function () {
        viewport.classList.remove('is-zoom');
        slides.forEach(function (s) { s.style.transformOrigin = 'center center'; });
      });
      viewport.addEventListener('mousemove', function (e) {
        var r = viewport.getBoundingClientRect();
        var x = ((e.clientX - r.left) / r.width) * 100;
        var y = ((e.clientY - r.top) / r.height) * 100;
        var active = slides[galleryIndex];
        if (active) active.style.transformOrigin = x + '% ' + y + '%';
      });
    }

    /* on mobile the viewport is a snap rail — keep the index in sync */
    var syncTimer;
    viewport.addEventListener('scroll', function () {
      if (viewport.scrollWidth <= viewport.clientWidth + 4) return;
      clearTimeout(syncTimer);
      syncTimer = setTimeout(function () {
        var i = Math.round(viewport.scrollLeft / (viewport.scrollWidth / slides.length));
        galleryIndex = Math.max(0, Math.min(slides.length - 1, i));
      }, 90);
    }, { passive: true });
  }

  /* ====================== lightbox ====================== */
  var lightbox = $('#lightbox');
  var lbUse = $('#lightboxUse');
  var lbImg = $('#lightboxImg');
  var lbStage = $('#lightboxStage');
  var lbScale = 1;
  var lbX = 0, lbY = 0;
  var dragging = false, dragStartX = 0, dragStartY = 0;

  function applyLbTransform() {
    if (!lbImg) return;
    lbImg.style.transform = 'translate(' + lbX + 'px,' + lbY + 'px) scale(' + lbScale + ')';
    if (lbStage) lbStage.classList.toggle('is-zoomed', lbScale > 1.02);
  }

  function setLbImage(i) {
    galleryIndex = (i + symbols.length) % symbols.length;
    if (lbUse) {
      lbUse.setAttribute('href', symbols[galleryIndex]);
      lbUse.setAttribute('xlink:href', symbols[galleryIndex]);
    }
    lbScale = 1; lbX = 0; lbY = 0;
    applyLbTransform();
    showSlide(galleryIndex);
  }

  function openLightbox() {
    if (!lightbox) return;
    lastFocus = document.activeElement;
    lightbox.hidden = false;
    requestAnimationFrame(function () { lightbox.classList.add('is-open'); });
    document.body.classList.add('is-locked');
    setLbImage(galleryIndex);
    var close = $('#lightboxClose');
    if (close) close.focus();
  }

  function closeLightbox() {
    if (!lightbox) return;
    lightbox.classList.remove('is-open');
    document.body.classList.remove('is-locked');
    setTimeout(function () { lightbox.hidden = true; }, 300);
    if (lastFocus && lastFocus.focus) lastFocus.focus();
  }

  var lbOpen = $('#lightboxOpen');
  if (lbOpen) lbOpen.addEventListener('click', openLightbox);
  var lbClose = $('#lightboxClose');
  if (lbClose) lbClose.addEventListener('click', closeLightbox);
  var lbPrev = $('#lbPrev');
  if (lbPrev) lbPrev.addEventListener('click', function () { setLbImage(galleryIndex - 1); });
  var lbNext = $('#lbNext');
  if (lbNext) lbNext.addEventListener('click', function () { setLbImage(galleryIndex + 1); });

  if (lbStage) {
    lbStage.addEventListener('click', function (e) {
      if (dragging) return;
      lbScale = lbScale > 1.02 ? 1 : 2.4;
      if (lbScale === 1) { lbX = 0; lbY = 0; }
      else {
        var r = lbStage.getBoundingClientRect();
        lbX = (r.width / 2 - (e.clientX - r.left)) * (lbScale - 1) / lbScale;
        lbY = (r.height / 2 - (e.clientY - r.top)) * (lbScale - 1) / lbScale;
      }
      applyLbTransform();
    });

    lbStage.addEventListener('wheel', function (e) {
      e.preventDefault();
      lbScale = Math.max(1, Math.min(4, lbScale - e.deltaY * 0.0022));
      if (lbScale === 1) { lbX = 0; lbY = 0; }
      applyLbTransform();
    }, { passive: false });

    lbStage.addEventListener('pointerdown', function (e) {
      if (lbScale <= 1.02) return;
      dragging = true;
      dragStartX = e.clientX - lbX;
      dragStartY = e.clientY - lbY;
      lbStage.setPointerCapture(e.pointerId);
    });
    lbStage.addEventListener('pointermove', function (e) {
      if (!dragging) return;
      lbX = e.clientX - dragStartX;
      lbY = e.clientY - dragStartY;
      applyLbTransform();
    });
    ['pointerup', 'pointercancel'].forEach(function (evt) {
      lbStage.addEventListener(evt, function () {
        setTimeout(function () { dragging = false; }, 0);
      });
    });
  }

  /* ====================== accordion ====================== */
  $$('.acc').forEach(function (item) {
    var btn = $('.acc__q', item);
    if (!btn) return;
    btn.addEventListener('click', function () {
      var open = item.classList.contains('is-open');
      $$('.acc').forEach(function (other) {
        other.classList.remove('is-open');
        var b = $('.acc__q', other);
        if (b) b.setAttribute('aria-expanded', 'false');
      });
      if (!open) {
        item.classList.add('is-open');
        btn.setAttribute('aria-expanded', 'true');
      }
    });
  });

  /* ====================== global keys ====================== */
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
      if (lightbox && !lightbox.hidden) return closeLightbox();
      if (drawer && drawer.classList.contains('is-open')) return closeCart();
      if (menuBtn && menuBtn.getAttribute('aria-expanded') === 'true') return setMenu(false);
    }
    if (lightbox && !lightbox.hidden) {
      if (e.key === 'ArrowRight') setLbImage(galleryIndex + 1);
      if (e.key === 'ArrowLeft') setLbImage(galleryIndex - 1);
    }
  });

  /* ====================== forms ====================== */
  function fieldError(input, message) {
    var field = input.closest('.field');
    if (!field) return;
    field.classList.toggle('is-bad', Boolean(message));
    var err = $('[data-err]', field);
    if (err) err.textContent = message || '';
  }

  var contactForm = $('#contactForm');
  if (contactForm) {
    contactForm.addEventListener('submit', function (e) {
      e.preventDefault();
      var name = $('#cName');
      var email = $('#cEmail');
      var msg = $('#cMsg');
      var ok = true;

      if (!name.value.trim()) { fieldError(name, 'Please tell us your name.'); ok = false; }
      else fieldError(name, '');

      if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email.value.trim())) {
        fieldError(email, 'That email address doesn\'t look right.'); ok = false;
      } else fieldError(email, '');

      if (msg.value.trim().length < 10) {
        fieldError(msg, 'A little more detail helps us answer properly.'); ok = false;
      } else fieldError(msg, '');

      var note = $('#contactNote');
      if (!ok) {
        if (note) note.textContent = '';
        return;
      }
      if (note) note.textContent = 'Thanks — we\'ve got it. Expect a reply within one business day.';
      contactForm.reset();
      toast('Message sent to hello@kalda.coffee');
    });

    $$('#contactForm input, #contactForm textarea').forEach(function (input) {
      input.addEventListener('input', function () {
        if (input.closest('.field').classList.contains('is-bad')) fieldError(input, '');
      });
    });
  }

  var newsForm = $('#newsForm');
  if (newsForm) {
    newsForm.addEventListener('submit', function (e) {
      e.preventDefault();
      var input = $('#newsEmail');
      var note = $('#newsNote');
      var valid = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(input.value.trim());
      if (note) note.textContent = valid
        ? 'You\'re on the list. First brew note lands on the 1st.'
        : 'Please enter a valid email address.';
      if (valid) { newsForm.reset(); toast('Subscribed to Brew Notes'); }
    });
  }

  /* ====================== boot ====================== */
  setVariant('clay');
  setQtyValue(1);
  renderCart();
  showSlide(0);
})();
