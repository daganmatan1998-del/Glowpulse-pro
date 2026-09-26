/* Checkout: order summary, validation, hand-off to hosted payment page. */
(() => {
  'use strict';
  const D = JSON.parse(document.getElementById('store-data').textContent);
  const C = window.NorrvalCart;
  const { fmt, esc } = window.NorrvalUtil;
  const lines = document.querySelector('[data-checkout-lines]');
  const totals = document.querySelector('[data-checkout-totals]');
  const form = document.querySelector('[data-checkout]');
  const out = document.querySelector('[data-checkout-msg]');

  function render() {
    if (!C.items.length) {
      lines.innerHTML = `<div class="empty" style="padding:32px 0"><p>Your cart is empty.</p><a class="btn" href="${D.product.url}">Shop Nocturne</a></div>`;
      totals.innerHTML = '';
      return;
    }
    const P = { [D.product.id]: D.product };
    lines.innerHTML = C.items
      .map((i) => {
        const p = P[i.id];
        return `<div class="line" style="grid-template-columns:64px 1fr auto"><div class="line__img" style="width:64px;height:64px">${p.image ? `<img src="${p.image}" alt="" width="64" height="64">` : ''}</div><div><div class="line__name">${esc(p.name)}</div><div class="line__meta">${esc(p.variant)} · Qty ${i.qty}</div></div><div class="line__price">${fmt(p.price * i.qty)}</div></div>`;
      })
      .join('');
    const d = C.discount();
    totals.innerHTML = `<div class="totals" style="margin-top:16px">
      <div><span>Subtotal</span><span>${fmt(C.subtotal())}</span></div>
      ${d ? `<div><span>Bundle saving</span><span>−${fmt(d)}</span></div>` : ''}
      <div><span>Shipping</span><span class="muted">Next step</span></div>
      <div><span>Taxes / duties</span><span class="muted">Next step</span></div>
      <div class="grand"><span>Before shipping &amp; tax</span><span>${fmt(C.total())}</span></div></div>`;
  }
  document.addEventListener('cart:change', render);
  render();

  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const show = (t, ok) => {
      out.hidden = false;
      out.textContent = t;
      out.className = `form-msg ${ok ? 'is-ok' : 'is-err'}`;
    };
    if (!C.items.length) return show('Your cart is empty.', false);
    let first = null;
    [...form.elements].forEach((f) => {
      if (!f.willValidate) return;
      const bad = !f.checkValidity() || (f.required && f.type !== 'checkbox' && !String(f.value).trim());
      f.setAttribute('aria-invalid', String(bad));
      if (bad && !first) first = f;
    });
    if (first) {
      first.focus();
      return show(first.name === 'terms' ? 'Please accept the terms to continue.' : 'Please complete the highlighted fields.', false);
    }
    if (!D.checkoutUrl) return show(`Online payment is not connected yet. Please email ${D.supportEmail} to order.`, false);
    const items = C.items.map((i) => `${i.id}:${i.qty}`).join(',');
    location.href = D.checkoutUrl
      .replace('{items}', encodeURIComponent(items))
      .replace('{email}', encodeURIComponent(form.email.value.trim()))
      .replace('{country}', encodeURIComponent(form.country.value));
  });
})();
