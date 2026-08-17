/* ==========================================================================
   Kalda — store engine
   Cart, wishlist and discounts, persisted to localStorage and shared by
   index.html and checkout.html. Emits "kalda:change" on window after any
   mutation so views can re-render.
   ========================================================================== */
(function (global) {
  'use strict';

  var KEY_CART = 'kalda.cart.v2';
  var KEY_WISH = 'kalda.wish.v2';
  var KEY_PROMO = 'kalda.promo.v2';

  var FREE_SHIPPING_AT = 95;
  var FLAT_SHIPPING = 6;
  var MAX_PER_LINE = 10;

  var PRODUCT = {
    id: 'bloom-set',
    name: 'Bloom Thermal Pour-Over Set',
    price: 89,
    compareAt: 124,
    symbol: '#shotSet',
    variants: {
      clay:   { label: 'Clay',   stock: 37 },
      bone:   { label: 'Bone',   stock: 61 },
      basalt: { label: 'Basalt', stock: 9  },
      sage:   { label: 'Sage',   stock: 84 }
    }
  };

  var ADDONS = {
    stand: { id: 'walnut-stand', name: 'Walnut brew stand',   price: 28, symbol: '#shotCarafe' },
    cups:  { id: 'cup-set',      name: 'Matching cup set (2)', price: 34, symbol: '#shotScene' }
  };

  var PROMOS = {
    BLOOM10:   { type: 'percent', value: 10, label: '10% off',        min: 0 },
    MORNING15: { type: 'percent', value: 15, label: '15% off $150+',  min: 150 },
    FREESHIP:  { type: 'shipping', value: 0, label: 'Free shipping',  min: 0 }
  };

  /* ---------- persistence ---------- */
  function read(key, fallback) {
    try {
      var raw = global.localStorage.getItem(key);
      return raw ? JSON.parse(raw) : fallback;
    } catch (e) {
      return fallback;
    }
  }

  function write(key, value) {
    try {
      global.localStorage.setItem(key, JSON.stringify(value));
    } catch (e) {
      /* private mode or quota — the session still works, it just won't persist */
    }
  }

  var cart = read(KEY_CART, []);
  var wish = read(KEY_WISH, []);
  var promo = read(KEY_PROMO, null);

  if (!Array.isArray(cart)) cart = [];
  if (!Array.isArray(wish)) wish = [];

  function commit() {
    write(KEY_CART, cart);
    write(KEY_WISH, wish);
    write(KEY_PROMO, promo);
    global.dispatchEvent(new CustomEvent('kalda:change'));
  }

  /* ---------- helpers ---------- */
  function money(n) {
    var rounded = Math.round(n * 100) / 100;
    return '$' + (rounded % 1 === 0 ? rounded.toFixed(0) : rounded.toFixed(2));
  }

  function findLine(id) {
    for (var i = 0; i < cart.length; i++) {
      if (cart[i].id === id) return cart[i];
    }
    return null;
  }

  /* ---------- cart ---------- */
  function addProduct(variantKey, qty) {
    var variant = PRODUCT.variants[variantKey] || PRODUCT.variants.clay;
    var id = PRODUCT.id + ':' + variantKey;
    var line = findLine(id);
    var want = Math.max(1, qty || 1);

    if (line) {
      line.qty = Math.min(MAX_PER_LINE, line.qty + want);
    } else {
      cart.push({
        id: id,
        sku: PRODUCT.id,
        name: PRODUCT.name,
        option: variant.label,
        variant: variantKey,
        price: PRODUCT.price,
        compareAt: PRODUCT.compareAt,
        symbol: PRODUCT.symbol,
        qty: Math.min(MAX_PER_LINE, want)
      });
    }
    commit();
    return id;
  }

  function addAddon(key) {
    var a = ADDONS[key];
    if (!a) return null;
    var line = findLine(a.id);
    if (line) {
      line.qty = Math.min(MAX_PER_LINE, line.qty + 1);
    } else {
      cart.push({
        id: a.id, sku: a.id, name: a.name, option: 'Add-on',
        variant: null, price: a.price, compareAt: null,
        symbol: a.symbol, qty: 1
      });
    }
    commit();
    return a.id;
  }

  function setQty(id, qty) {
    var line = findLine(id);
    if (!line) return;
    var n = Math.max(0, Math.min(MAX_PER_LINE, qty));
    if (n === 0) {
      removeLine(id);
      return;
    }
    line.qty = n;
    commit();
  }

  function removeLine(id) {
    cart = cart.filter(function (l) { return l.id !== id; });
    commit();
  }

  function clearCart() {
    cart = [];
    promo = null;
    commit();
  }

  function count() {
    return cart.reduce(function (n, l) { return n + l.qty; }, 0);
  }

  /* ---------- money ---------- */
  function subtotal() {
    return cart.reduce(function (n, l) { return n + l.price * l.qty; }, 0);
  }

  function discount() {
    if (!promo) return 0;
    var rule = PROMOS[promo];
    if (!rule || rule.type !== 'percent') return 0;
    var sub = subtotal();
    if (sub < rule.min) return 0;
    return Math.round(sub * rule.value) / 100;
  }

  function shipping() {
    var sub = subtotal() - discount();
    if (cart.length === 0) return 0;
    if (promo === 'FREESHIP') return 0;
    return sub >= FREE_SHIPPING_AT ? 0 : FLAT_SHIPPING;
  }

  function total() {
    return Math.max(0, subtotal() - discount() + shipping());
  }

  function shippingGap() {
    return Math.max(0, FREE_SHIPPING_AT - (subtotal() - discount()));
  }

  /* ---------- promo ---------- */
  function applyPromo(codeRaw) {
    var code = String(codeRaw || '').trim().toUpperCase();
    if (!code) return { ok: false, message: 'Enter a code first.' };

    var rule = PROMOS[code];
    if (!rule) return { ok: false, message: 'That code isn\'t valid — try BLOOM10.' };
    if (subtotal() < rule.min) {
      return { ok: false, message: 'Spend ' + money(rule.min) + ' or more to use ' + code + '.' };
    }
    if (promo === code) return { ok: false, message: code + ' is already applied.' };

    promo = code;
    commit();
    return { ok: true, message: rule.label + ' applied.', code: code, label: rule.label };
  }

  function removePromo() {
    promo = null;
    commit();
  }

  function promoLabel() {
    return promo && PROMOS[promo] ? PROMOS[promo].label : null;
  }

  /* ---------- wishlist ---------- */
  function toggleWish(variantKey) {
    var id = PRODUCT.id + ':' + variantKey;
    var was = wish.indexOf(id) !== -1;
    wish = was ? wish.filter(function (w) { return w !== id; }) : wish.concat([id]);
    commit();
    return !was;
  }

  function inWish(variantKey) {
    return wish.indexOf(PRODUCT.id + ':' + variantKey) !== -1;
  }

  /* ---------- stock ---------- */
  function stockFor(variantKey) {
    var v = PRODUCT.variants[variantKey];
    if (!v) return 0;
    var line = findLine(PRODUCT.id + ':' + variantKey);
    return Math.max(0, v.stock - (line ? line.qty : 0));
  }

  global.KaldaStore = {
    PRODUCT: PRODUCT,
    ADDONS: ADDONS,
    PROMOS: PROMOS,
    FREE_SHIPPING_AT: FREE_SHIPPING_AT,
    FLAT_SHIPPING: FLAT_SHIPPING,
    MAX_PER_LINE: MAX_PER_LINE,

    lines: function () { return cart.slice(); },
    count: count,
    addProduct: addProduct,
    addAddon: addAddon,
    setQty: setQty,
    removeLine: removeLine,
    clearCart: clearCart,

    subtotal: subtotal,
    discount: discount,
    shipping: shipping,
    total: total,
    shippingGap: shippingGap,

    applyPromo: applyPromo,
    removePromo: removePromo,
    promoCode: function () { return promo; },
    promoLabel: promoLabel,

    toggleWish: toggleWish,
    inWish: inWish,
    wishCount: function () { return wish.length; },

    stockFor: stockFor,
    money: money
  };
})(window);
