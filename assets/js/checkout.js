/* ==========================================================================
   Kalda — checkout
   ========================================================================== */
(function () {
  'use strict';

  var S = window.KaldaStore;
  var $ = function (sel, root) { return (root || document).querySelector(sel); };
  var $$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };

  var TAX_RATE = 0.086;
  var EXPRESS_COST = 14;
  var finished = false;

  /* ---------- toast ---------- */
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

  /* ---------- money ---------- */
  function isExpress() {
    var picked = $('input[name="ship"]:checked');
    return Boolean(picked) && picked.value === 'express';
  }

  function shippingCost() {
    return isExpress() ? EXPRESS_COST : S.shipping();
  }

  function taxAmount() {
    return Math.round((S.subtotal() - S.discount()) * TAX_RATE * 100) / 100;
  }

  function grandTotal() {
    return Math.max(0, S.subtotal() - S.discount() + shippingCost() + taxAmount());
  }

  /* ---------- delivery estimate ---------- */
  function addBusinessDays(from, days) {
    var d = new Date(from.getTime());
    var added = 0;
    while (added < days) {
      d.setDate(d.getDate() + 1);
      var day = d.getDay();
      if (day !== 0 && day !== 6) added++;
    }
    return d;
  }

  function formatDay(d) {
    return d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
  }

  function etaText() {
    var now = new Date();
    if (isExpress()) return formatDay(addBusinessDays(now, 1));
    return formatDay(addBusinessDays(now, 2)) + ' – ' + formatDay(addBusinessDays(now, 4));
  }

  /* ---------- line rendering ---------- */
  function lineNode(line) {
    var el = document.createElement('article');
    el.className = 'line';
    el.innerHTML =
      '<div class="line__media"><svg viewBox="0 0 800 800" aria-hidden="true"><use href="' + line.symbol + '"/></svg></div>' +
      '<div class="line__info">' +
        '<p class="line__name"></p>' +
        '<p class="line__opt"></p>' +
        '<div class="line__row">' +
          '<div class="line__qty"><span></span></div>' +
          '<span class="line__price"></span>' +
        '</div>' +
      '</div>';
    $('.line__name', el).textContent = line.name;
    $('.line__opt', el).textContent = line.option;
    $('.line__qty span', el).textContent = 'Qty ' + line.qty;
    $('.line__price', el).textContent = S.money(line.price * line.qty);
    return el;
  }

  function render() {
    if (finished) return;

    var lines = S.lines();
    var empty = $('#coEmpty');
    var main = $('#coMain');

    if (lines.length === 0) {
      if (empty) empty.hidden = false;
      if (main) main.hidden = true;
      return;
    }
    if (empty) empty.hidden = true;
    if (main) main.hidden = false;

    var host = $('#coLines');
    if (host) {
      host.textContent = '';
      lines.forEach(function (l) { host.appendChild(lineNode(l)); });
    }

    var disc = S.discount();
    var ship = shippingCost();

    var set = function (id, text) { var el = $(id); if (el) el.textContent = text; };
    set('#coSub', S.money(S.subtotal()));
    set('#coDisc', '−' + S.money(disc));
    set('#coShip', ship === 0 ? 'Free' : S.money(ship));
    set('#coTax', S.money(taxAmount()));
    set('#coTotal', S.money(grandTotal()));
    set('#toggleTotal', S.money(grandTotal()));
    set('#payLabel', 'Pay ' + S.money(grandTotal()));
    set('#stdPrice', S.shipping() === 0 ? 'Free' : S.money(S.shipping()));

    var discRow = $('#coDiscRow');
    if (discRow) discRow.hidden = disc <= 0;

    var applied = $('#coPromoApplied');
    var tag = $('#coPromoTag');
    if (applied && tag) {
      var label = S.promoLabel();
      applied.hidden = !label;
      if (label) tag.textContent = S.promoCode() + ' · ' + label;
    }
  }

  window.addEventListener('kalda:change', render);
  $$('input[name="ship"]').forEach(function (r) { r.addEventListener('change', render); });

  /* ---------- summary toggle ---------- */
  var toggle = $('#summaryToggle');
  var summary = $('#coSummary');
  if (toggle && summary) {
    toggle.addEventListener('click', function () {
      var open = toggle.getAttribute('aria-expanded') === 'true';
      toggle.setAttribute('aria-expanded', String(!open));
      summary.classList.toggle('is-open', !open);
    });
  }

  /* ---------- promo ---------- */
  var promoForm = $('#coPromo');
  if (promoForm) {
    promoForm.addEventListener('submit', function (e) {
      e.preventDefault();
      var input = $('#coPromoInput');
      var msg = $('#coPromoMsg');
      var res = S.applyPromo(input.value);
      if (msg) {
        msg.textContent = res.message;
        msg.classList.toggle('is-bad', !res.ok);
      }
      if (res.ok) { input.value = ''; toast(res.label + ' applied'); }
    });
  }
  var promoRemove = $('#coPromoRemove');
  if (promoRemove) {
    promoRemove.addEventListener('click', function () {
      S.removePromo();
      var msg = $('#coPromoMsg');
      if (msg) { msg.textContent = ''; msg.classList.remove('is-bad'); }
    });
  }

  /* ---------- input masks ---------- */
  var cardInput = $('#fCard');
  if (cardInput) {
    cardInput.addEventListener('input', function () {
      var digits = cardInput.value.replace(/\D/g, '').slice(0, 19);
      cardInput.value = digits.replace(/(.{4})/g, '$1 ').trim();
    });
  }
  var expInput = $('#fExp');
  if (expInput) {
    expInput.addEventListener('input', function () {
      var digits = expInput.value.replace(/\D/g, '').slice(0, 4);
      expInput.value = digits.length > 2 ? digits.slice(0, 2) + '/' + digits.slice(2) : digits;
    });
  }
  var cvcInput = $('#fCvc');
  if (cvcInput) {
    cvcInput.addEventListener('input', function () {
      cvcInput.value = cvcInput.value.replace(/\D/g, '').slice(0, 4);
    });
  }

  /* ---------- validation ---------- */
  function fieldError(input, message) {
    var field = input.closest('.field');
    if (!field) return;
    field.classList.toggle('is-bad', Boolean(message));
    var err = $('[data-err]', field);
    if (err) err.textContent = message || '';
  }

  function luhn(number) {
    var sum = 0;
    var alt = false;
    for (var i = number.length - 1; i >= 0; i--) {
      var n = parseInt(number.charAt(i), 10);
      if (alt) {
        n *= 2;
        if (n > 9) n -= 9;
      }
      sum += n;
      alt = !alt;
    }
    return sum % 10 === 0;
  }

  function validate() {
    var ok = true;
    var required = [
      ['#fFirst', 'Please enter your first name.'],
      ['#fLast', 'Please enter your last name.'],
      ['#fAddr', 'We need a street address to deliver to.'],
      ['#fCity', 'Please enter your city.'],
      ['#fZip', 'Please enter a postal code.'],
      ['#fName', 'Enter the name printed on the card.']
    ];

    required.forEach(function (pair) {
      var el = $(pair[0]);
      if (!el) return;
      if (!el.value.trim()) { fieldError(el, pair[1]); ok = false; }
      else fieldError(el, '');
    });

    var email = $('#fEmail');
    if (email) {
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email.value.trim())) {
        fieldError(email, 'That email address doesn\'t look right.'); ok = false;
      } else fieldError(email, '');
    }

    if (cardInput) {
      var digits = cardInput.value.replace(/\D/g, '');
      if (digits.length < 13 || digits.length > 19 || !luhn(digits)) {
        fieldError(cardInput, 'Check the card number — try 4242 4242 4242 4242.'); ok = false;
      } else fieldError(cardInput, '');
    }

    if (expInput) {
      var m = expInput.value.match(/^(\d{2})\/(\d{2})$/);
      var valid = false;
      if (m) {
        var month = parseInt(m[1], 10);
        var year = 2000 + parseInt(m[2], 10);
        var now = new Date();
        var endOfMonth = new Date(year, month, 0, 23, 59, 59);
        valid = month >= 1 && month <= 12 && endOfMonth >= now;
      }
      if (!valid) { fieldError(expInput, 'Use MM/YY, and a date in the future.'); ok = false; }
      else fieldError(expInput, '');
    }

    if (cvcInput) {
      if (!/^\d{3,4}$/.test(cvcInput.value)) { fieldError(cvcInput, '3 or 4 digits.'); ok = false; }
      else fieldError(cvcInput, '');
    }

    return ok;
  }

  $$('.co__form input').forEach(function (input) {
    input.addEventListener('input', function () {
      var field = input.closest('.field');
      if (field && field.classList.contains('is-bad')) fieldError(input, '');
    });
  });

  /* ---------- place order ---------- */
  function orderNumber() {
    var chars = '0123456789ABCDEFGHJKLMNPQRSTUVWXYZ';
    var out = '';
    for (var i = 0; i < 6; i++) out += chars.charAt(Math.floor(Math.random() * chars.length));
    return 'KAL-' + out;
  }

  var form = $('#coForm');
  if (form) {
    form.addEventListener('submit', function (e) {
      e.preventDefault();

      if (!validate()) {
        var bad = $('.field.is-bad');
        if (bad) {
          bad.scrollIntoView({ behavior: 'smooth', block: 'center' });
          var input = $('input, textarea', bad);
          if (input) input.focus({ preventScroll: true });
        }
        toast('Please check the highlighted fields', true);
        return;
      }

      var payBtn = $('#payBtn');
      var payLabel = $('#payLabel');
      if (payBtn && payLabel) {
        payBtn.classList.add('is-busy');
        payLabel.textContent = 'Authorising…';
      }

      var total = grandTotal();
      var email = $('#fEmail').value.trim();
      var eta = etaText();

      setTimeout(function () {
        finished = true;
        S.clearCart();

        var set = function (id, text) { var el = $(id); if (el) el.textContent = text; };
        set('#doneOrder', orderNumber());
        set('#doneTotal', S.money(total));
        set('#doneEta', eta);
        set('#doneEmail', email);

        var main = $('#coMain');
        var done = $('#coDone');
        if (main) main.hidden = true;
        if (done) done.hidden = false;
        window.scrollTo({ top: 0, behavior: 'smooth' });
      }, 1100);
    });
  }

  render();
})();
