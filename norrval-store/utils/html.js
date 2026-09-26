// Build-time helpers shared by components and pages.

export const esc = (s = '') =>
  String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

/** Escape text, then wrap [PLACEHOLDER] tokens so the owner can spot them. */
export const txt = (s = '') => esc(s).replace(/\[([^\]]+)\]/g, '<mark class="ph">[$1]</mark>');

/** Tagged template that joins arrays and drops null/false. */
export const html = (strings, ...vals) =>
  strings.reduce((out, str, i) => {
    let v = vals[i - 1];
    if (Array.isArray(v)) v = v.join('');
    if (v === null || v === undefined || v === false) v = '';
    return out + v + str;
  });

export const money = (amount, currency = 'USD', locale = 'en-US') =>
  new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: Number.isInteger(amount) ? 0 : 2,
  }).format(amount);

export const jsonLd = (obj) =>
  `<script type="application/ld+json">${JSON.stringify(obj).replace(/</g, '\\u003c')}</script>`;
